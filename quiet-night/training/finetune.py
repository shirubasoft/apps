"""Fine-tunes YAMNet's later layers on the app's task and exports the app's sound model.

Each example is a 0.96 s window of a cat, motorcycle, car or harmless sound from the training split, mixed into a
training room at night levels, often only partly filling the window, and judged next to another window of the same
room. The head mirrors the app's: a linear softmax over the embedding and AudioSet log-odds, each next to its
difference from the room's. The validation split is only reported on. The defaults built the bundled model:

    uv run --with-requirements training/requirements.txt python training/finetune.py \
        reference-sounds/manifest.json <mp3 folder> models/yamnet.onnx <output folder>
"""
import argparse, json, os, subprocess, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, torch, torch.nn as nn, torch.nn.functional as F
from sklearn.metrics import roc_auc_score
from yamnet_torch import YamnetTorch, YamnetExport, WINDOW

SR, HOP, HOPS = 16000, 160, 96
GROUPS = {"other": 0, "cat": 1, "motorcycle": 2, "car": 3}
HUM_SHARE = 0.0


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "quiet", "-i", path, "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).copy()


def loudest(x, seconds):
    n = int(seconds * SR)
    if len(x) <= n: return x
    e = np.convolve(x ** 2, np.ones(SR // 10), "valid")[:: SR // 10]
    k = n // (SR // 10)
    start = int(np.convolve(e, np.ones(k), "valid").argmax()) * (SR // 10)
    return x[start:start + n]


def hop_db(x):
    n = len(x) // HOP
    return 10 * np.log10((x[: n * HOP].reshape(n, HOP) ** 2).mean(1) + 1e-12)


class Clip:
    def __init__(self, x):
        self.x = x.astype(np.float16)
        db = hop_db(x)
        floor = min(np.percentile(db, 10) + 6, np.percentile(db, 90) - 10)
        self.active = db > floor
        act = np.repeat(self.active, HOP)[: len(x)]
        self.rms_db = 10 * np.log10(np.mean(x[: len(act)][act] ** 2) + 1e-12) if act.any() else hop_db(x).max()
        self.ends = np.flatnonzero(self.active)


def load(manifest, audio, splits, cache):
    if os.path.exists(cache):
        return torch.load(cache, weights_only=False)
    data = {s: {"rooms": [], **{g: [] for g in GROUPS}} for s in splits}
    for e in json.load(open(manifest)):
        if e["split"] not in splits: continue
        path = f"{audio}/{e['id']}.mp3"
        if not os.path.exists(path): continue
        x = decode(path)
        if e["group"] == "background":
            x = loudest(x, 30)
            if len(x) >= SR: data[e["split"]]["rooms"].append(x.astype(np.float16))
        elif e["group"] in GROUPS:
            x = loudest(x, 10)
            if len(x) >= SR // 2:
                c = Clip(x)
                if len(c.ends): data[e["split"]][e["group"]].append(c)
    torch.save(data, cache)
    return data


def rms_db(x):
    return 10 * np.log10(np.mean(x.astype(np.float32) ** 2) + 1e-12)


def segment(rng, room, n):
    start = rng.integers(len(room))
    return np.resize(np.roll(room, -start), n).astype(np.float32)


def hum_room(rng, seconds=8):
    n = seconds * SR
    white = rng.standard_normal(n)
    spectrum = np.fft.rfft(white)
    f = np.fft.rfftfreq(n, 1 / SR)
    tilt = rng.uniform(0.5, 1.5)  # between pink and brown
    spectrum /= np.maximum(f, 20) ** (tilt / 2)
    x = np.fft.irfft(spectrum, n)
    x /= np.std(x) + 1e-9
    t = np.arange(n) / SR
    base = rng.uniform(40, 180)
    hum = sum(rng.uniform(0.2, 1.0) / k * np.sin(2 * np.pi * base * k * t + rng.uniform(0, 6.28)) for k in range(1, rng.integers(2, 7)))
    hum *= 1 + 0.1 * np.sin(2 * np.pi * rng.uniform(0.1, 2) * t)
    x = x + rng.uniform(0, 2) * hum
    return x.astype(np.float32)


HUM_ROOMS = []


def example(rng, data):
    room = HUM_ROOMS[rng.integers(len(HUM_ROOMS))] if HUM_ROOMS and rng.random() < HUM_SHARE else data["rooms"][rng.integers(len(data["rooms"]))]
    gain = 10 ** ((-50 + rng.uniform(-6, 6) - rms_db(room)) / 20)
    bed, ref = segment(rng, room, WINDOW) * gain, segment(rng, room, WINDOW) * gain
    kind = rng.choice(5, p=[0.12, 0.28, 0.24, 0.18, 0.18])  # room only, other, cat, motorcycle, car
    label = 0
    if kind > 0:
        group = ["other", "cat", "motorcycle", "car"][kind - 1]
        clip = data[group][rng.integers(len(data[group]))]
        end = (clip.ends[rng.integers(len(clip.ends))] + 1) * HOP
        fill = 1.0 if rng.random() < 0.5 else rng.uniform(0.1, 1.0)
        n = min(int(fill * WINDOW), end)
        event = clip.x[end - n:end].astype(np.float32)
        level = rng.uniform(8, 26) if kind > 1 else rng.uniform(6, 22)
        scale = 10 ** ((rms_db(bed) + level - clip.rms_db) / 20)
        bed[WINDOW - n:] += event * scale
        share = clip.active[(end - n) // HOP:end // HOP].sum() / HOPS
        if kind > 1 and share >= 0.15: label = kind - 1
    noise = 10 ** (-90 / 20)
    bed += rng.standard_normal(WINDOW).astype(np.float32) * noise
    ref += rng.standard_normal(WINDOW).astype(np.float32) * noise
    return bed, ref, label


def batch(rng, data, size):
    xs, rs, ys = zip(*(example(rng, data) for _ in range(size)))
    return torch.from_numpy(np.stack(xs)), torch.from_numpy(np.stack(rs)), torch.tensor(ys)


class Detector(nn.Module):
    def __init__(self, onnx_path):
        super().__init__()
        self.yamnet = YamnetTorch(onnx_path)
        self.norm = nn.BatchNorm1d(2 * 1545, affine=False)
        self.head = nn.Linear(2 * 1545, 4)

    def features(self, wave):
        scores, emb, _ = self.yamnet(wave)
        p = scores.clamp(1e-4, 1 - 1e-4)
        return torch.cat([emb, torch.log(p / (1 - p))], 1)

    def forward(self, wave, room):
        f = self.features(wave)
        r = self.features(room)
        return self.head(self.norm(torch.cat([f, f - r], 1)))


def evaluate(model, fixed, device):
    model.eval()
    probs = []
    with torch.no_grad():
        for x, r, _ in fixed:
            probs.append(F.softmax(model(x.to(device), r.to(device)), 1).cpu())
    model.train()
    P = torch.cat(probs).numpy(); y = torch.cat([b[2] for b in fixed]).numpy()
    out = {"target_auc": roc_auc_score(y > 0, 1 - P[:, 0])}
    for c, name in ((1, "cat"), (2, "moto"), (3, "car")):
        out[f"{name}_auc"] = roc_auc_score(y == c, P[:, c])
    cat = y == 1
    thr = np.quantile(P[cat, 1], 0.1)
    out["cat_fa@90"] = float(np.mean(P[~cat, 1] >= thr))
    out["balanced_acc"] = float(np.mean([np.mean(P[y == c].argmax(1) == c) for c in range(4)]))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("manifest"); ap.add_argument("audio"); ap.add_argument("onnx"); ap.add_argument("out")
    ap.add_argument("--head-steps", type=int, default=1500)
    ap.add_argument("--tune-steps", type=int, default=6000)
    ap.add_argument("--unfreeze-from", type=int, default=15)
    ap.add_argument("--lr", type=float, default=3e-5)
    ap.add_argument("--batch", type=int, default=128)
    ap.add_argument("--hum-share", type=float, default=0.0)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    device = "cuda"
    data = load(args.manifest, args.audio, ("train", "validation"), os.path.join(args.out, "clips.pt"))
    print({s: {k: len(v) for k, v in d.items()} for s, d in data.items()}, flush=True)
    rng_val = np.random.default_rng(7)
    fixed = [batch(rng_val, data["validation"], 256) for _ in range(24)]
    global HUM_SHARE
    HUM_SHARE = args.hum_share  # training only: validation keeps the real rooms
    if HUM_SHARE:
        hum_rng = np.random.default_rng(11)
        HUM_ROOMS.extend(hum_room(hum_rng) for _ in range(300))
    model = Detector(args.onnx).to(device)
    rng = np.random.default_rng(1)

    def run(steps, params, lr, tag):
        opt = torch.optim.AdamW(params, lr=lr, weight_decay=1e-4)
        sched = torch.optim.lr_scheduler.OneCycleLR(opt, max_lr=lr, total_steps=steps, pct_start=0.1)
        clock = time.time()
        for step in range(1, steps + 1):
            x, r, y = batch(rng, data["train"], args.batch)
            logits = model(x.to(device), r.to(device))
            loss = F.cross_entropy(logits, y.to(device))
            opt.zero_grad(); loss.backward(); opt.step(); sched.step()
            if step % 500 == 0 or step == steps:
                m = evaluate(model, fixed, device)
                print(f"{tag} step {step} loss {loss.item():.3f} " + " ".join(f"{k} {v:.3f}" for k, v in m.items()) + f" {time.time() - clock:.0f}s", flush=True)

    for p in model.yamnet.parameters(): p.requires_grad = False
    run(args.head_steps, list(model.head.parameters()), 1e-3, "head")
    for i, layer in enumerate(model.yamnet.layers):
        for p in layer.parameters(): p.requires_grad = i >= args.unfreeze_from
    tuned = [p for l in model.yamnet.layers for p in l.parameters() if p.requires_grad]
    run(args.tune_steps, [{"params": tuned, "lr": args.lr}, {"params": model.head.parameters(), "lr": args.lr * 10}], args.lr, "tune")
    torch.save(model.state_dict(), os.path.join(args.out, "detector.pt"))
    export = YamnetExport(model.yamnet.cpu().eval())
    path = os.path.join(args.out, "yamnet-quiet-night.onnx")
    torch.onnx.export(export, torch.zeros(WINDOW), path, input_names=["waveform"], output_names=["output_0", "output_1", "output_2"],
                      opset_version=17, dynamo=False)
    print("exported", path, flush=True)


if __name__ == "__main__":
    main()

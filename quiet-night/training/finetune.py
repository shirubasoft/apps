"""Fine-tunes YAMNet's later layers on the app's task and exports the app's sound model.

Examples follow the C# trainer (`ExampleBuilder`): a clip from the training split is mixed into a training room at a
level above the room measured as in `Loudness.ActiveRmsDb`, and 0.96 s windows ending anywhere from its onset, through
its pauses, to a second after it ends are judged next to the same room. A hop holds the clip when, as in
`ExampleBuilder`, the clip alone is louder than the room alone in some masking band where the mixture rises at least
6 dB above the room's median over the 4 s before the clip began, as a listener holding the room through the clip
sees it, and the clip is above its own quiet floor; band levels come from the same analysis as `HopAnalyzer`. A
window holds the clip's class when the clip holds at least 30 % of its hops and holds nothing at 2 % or less; windows
in between are left out, and harmless clips are always Other. The head mirrors the app's: a linear softmax over the
embedding and AudioSet log-odds, each next to its difference from the room's. The validation split is only reported
on. `docs/evaluation-history.md` names the revision of this script that built the bundled model. Run it as:

    uv run --with-requirements training/requirements.txt python training/finetune.py \\
        reference-sounds/manifest.json <mp3 folder> models/yamnet.onnx <output folder>
"""
import argparse, hashlib, json, os, subprocess, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, torch, torch.nn as nn, torch.nn.functional as F
from scipy.signal import lfilter
from sklearn.metrics import roc_auc_score
from yamnet_torch import YamnetTorch, YamnetExport, WINDOW

SR, HOP, HOPS = 16000, 160, 96
GROUPS = {"other": 0, "cat": 1, "motorcycle": 2, "car": 3}
# Levels above the room, in dB: the C# trainer's and the evaluation's ranges for each kind of sound, slightly widened.
LEVELS = {"other": (4, 24), "cat": (6, 27), "motorcycle": (8, 32), "car": (6, 27)}
ACTIVE_SHARE, QUIET_SHARE = 0.30, 0.02
ROOM_DB = -50
ACTIVITY_DB = 6
PREPROCESSING = ("loudest 10 s clips, 30 s rooms; active RMS over 100 ms blocks within 20 dB; own floor min(p10+6, p90-10); "
                 "HopAnalyzer band powers, 40 ms smoothing; band dominance with 6 dB rise over the room median of the 4 s before onset")
WARMUP_HOPS = 400  # ExampleBuilder.Warmup: the listener hears 4 s of room before the clip begins
# HopAnalyzer: a 400-sample symmetric Hann window ending at each hop, a 512-point FFT scaled so a band's bins add up to
# its mean square, the seven octave masking bands, and power smoothing over 40 ms.
HANN = 0.5 - 0.5 * np.cos(2 * np.pi * np.arange(400) / 399)
POWER_SCALE = 2 / (512 * np.sum(HANN ** 2))
BAND_EDGES = [(1, 4), (4, 8), (8, 16), (16, 32), (32, 64), (64, 128), (128, 257)]
SMOOTH_ALPHA = 1 - np.exp(-0.01 / 0.04)
CONTEXT_HOPS = 20


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
    return 10 * np.log10((x[: n * HOP].reshape(n, HOP).astype(np.float32) ** 2).mean(1) + 1e-12)


def band_power(x):
    """Smoothed masking-band power for each hop of x, as HopAnalyzer computes it once primed: (hops, 7)."""
    x = np.concatenate([np.zeros(400 - HOP, np.float32), x.astype(np.float32)])
    frames = np.lib.stride_tricks.sliding_window_view(x, 400)[::HOP]
    power = np.abs(np.fft.rfft(frames * HANN, 512)) ** 2 * POWER_SCALE
    bands = np.stack([power[:, a:b].sum(1) for a, b in BAND_EDGES], 1)
    return lfilter([SMOOTH_ALPHA], [1, SMOOTH_ALPHA - 1], bands, axis=0, zi=(1 - SMOOTH_ALPHA) * bands[:1])[0]


def to_db(power):
    return 10 * np.log10(power + 1e-12)


def stands_out(sound, room, room_median_db, floor_db):
    """For each hop: the sound alone is louder than the room alone in a band where the mixture rises at least
    ACTIVITY_DB above the room's median, and the sound alone is above its own floor, as in ExampleBuilder."""
    s, r, m = band_power(sound), band_power(room), band_power(sound + room)
    dominates = ((s > r) & (to_db(m) - room_median_db >= ACTIVITY_DB)).any(1)
    return dominates & (to_db(s.sum(1)) > floor_db)


def active_rms_db(x):
    """As Loudness.ActiveRmsDb: the RMS over the 100 ms blocks within 20 dB of the loudest one."""
    n = max(1, len(x) // (SR // 10))
    blocks = np.array([np.mean(x[b * SR // 10:(b + 1) * SR // 10].astype(np.float32) ** 2) for b in range(n)])
    return 10 * np.log10(blocks[blocks >= blocks.max() * 0.01].mean() + 1e-12)


def rms_db(x):
    return 10 * np.log10(np.mean(x.astype(np.float32) ** 2) + 1e-12)


class Clip:
    def __init__(self, x):
        self.x = x.astype(np.float16)
        db = hop_db(x)
        self.floor = min(np.percentile(db, 10) + 6, np.percentile(db, 90) - 10)
        self.hop_db = db
        self.active_db = active_rms_db(x)


class Room:
    def __init__(self, x):
        self.x = x.astype(np.float16)
        self.band_db = to_db(band_power(x))
        self.rms_db = rms_db(x)

    def median_before(self, sample):
        """The room's median band level over the 4 s of the loop before <sample>, at unit gain."""
        hops = len(self.band_db)
        end = (sample // HOP) % hops
        index = (end - 1 - np.arange(min(WARMUP_HOPS, hops))) % hops
        return np.median(self.band_db[index], 0)


def audio_digest(manifest, audio, splits):
    """A digest of the audio files the splits would use, so a re-downloaded or replaced file invalidates the cache."""
    h = hashlib.sha256()
    for e in json.load(open(manifest)):
        if e["split"] not in splits: continue
        path = f"{audio}/{e['id']}.mp3"
        h.update(f"{e['id']}:".encode())
        h.update(hashlib.sha256(open(path, "rb").read()).digest() if os.path.exists(path) else b"missing")
    return h.hexdigest()


def fingerprint(manifest, splits, audio=None):
    h = hashlib.sha256(open(manifest, "rb").read())
    h.update(json.dumps([sorted(splits), PREPROCESSING, sorted(GROUPS)]).encode())
    if audio is not None: h.update(audio_digest(manifest, audio, splits).encode())
    return h.hexdigest()


def load(manifest, audio, splits, cache):
    """Decodes the splits' clips, reusing a cache only when it was made from this manifest and preprocessing."""
    key = fingerprint(manifest, splits, audio)
    if os.path.exists(cache):
        saved = torch.load(cache, weights_only=False)
        if saved.get("fingerprint") == key:
            return saved["data"], saved["inventory"]
        print("cache was made from another manifest or preprocessing; rebuilding", flush=True)
    data = {s: {"rooms": [], **{g: [] for g in GROUPS}} for s in splits}
    inventory = {"used": [], "missing": []}
    for e in json.load(open(manifest)):
        if e["split"] not in splits or (e["group"] not in GROUPS and e["group"] != "background"): continue
        path = f"{audio}/{e['id']}.mp3"
        if not os.path.exists(path):
            inventory["missing"].append(e["id"]); continue
        x = decode(path)
        if e["group"] == "background":
            x = loudest(x, 30)
            if len(x) < SR: inventory["missing"].append(e["id"]); continue
            data[e["split"]]["rooms"].append(Room(x))
        else:
            x = loudest(x, 10)
            if len(x) < SR // 2: inventory["missing"].append(e["id"]); continue
            data[e["split"]][e["group"]].append(Clip(x))
        inventory["used"].append({"id": e["id"], "split": e["split"], "group": e["group"],
                                  "sha256": hashlib.sha256(open(path, "rb").read()).hexdigest()})
    torch.save({"fingerprint": key, "data": data, "inventory": inventory}, cache)
    return data, inventory


def segment(rng, room, n, gain, start=None):
    start = rng.integers(len(room)) if start is None else start
    return np.resize(np.roll(room, -start), n).astype(np.float32) * gain


def example(rng, data):
    """One window and two windows of the same room, with the window's class, or None when it is left out."""
    room = data["rooms"][rng.integers(len(data["rooms"]))]
    gain_db = ROOM_DB + rng.uniform(-6, 6) - room.rms_db
    gain = 10 ** (gain_db / 20)
    refs = [segment(rng, room.x, WINDOW, gain) for _ in range(2)]
    group = rng.choice(["room", "other", "cat", "motorcycle", "car"], p=[0.10, 0.30, 0.24, 0.18, 0.18])
    if group == "room":
        window, label = segment(rng, room.x, WINDOW, gain), 0
    else:
        clip = data[group][rng.integers(len(data[group]))]
        length = len(clip.x)
        # Window ends: a third at the onset, the rest anywhere through the clip and a second past its end.
        end = int(rng.uniform(0, 1.0) * SR) if rng.random() < 0.35 else int(rng.uniform(0.1 * SR, length + SR))
        context = CONTEXT_HOPS * HOP
        bed_start = int(rng.integers(len(room.x)))
        bed = segment(rng, room.x, WINDOW + context, gain, bed_start)
        level = rng.uniform(*LEVELS[group])
        scale = 10 ** ((rms_db(bed[context:]) + level - clip.active_db) / 20)
        start = end - WINDOW - context
        lo, hi = max(0, start), min(length, end)
        placed = np.zeros(WINDOW + context, np.float32)
        if hi > lo:
            placed[lo - start:hi - start] = clip.x[lo:hi].astype(np.float32) * scale
        window = (bed + placed)[context:]
        # The clip began at bed sample -start, which lies (bed_start - start) into the room's loop.
        median_db = room.median_before(bed_start - start) + gain_db
        active = stands_out(placed, bed, median_db, clip.floor + 20 * np.log10(scale))[CONTEXT_HOPS:]
        share = active.mean()
        if group == "other":
            label = 0
        elif share >= ACTIVE_SHARE:
            label = GROUPS[group]
        elif share <= QUIET_SHARE:
            label = 0
        else:
            return None
    noise = 10 ** (-90 / 20) * np.sqrt(3)
    window += rng.uniform(-noise, noise, WINDOW).astype(np.float32)
    refs = [r + rng.uniform(-noise, noise, WINDOW).astype(np.float32) for r in refs]
    return window, np.stack(refs), label


def batch(rng, data, size):
    items = []
    while len(items) < size:
        e = example(rng, data)
        if e is not None: items.append(e)
    xs, rs, ys = zip(*items)
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

    def forward(self, wave, rooms):
        f = self.features(wave)
        r = self.features(rooms.flatten(0, 1)).view(rooms.shape[0], rooms.shape[1], -1).mean(1)
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
    out["cat_fa@90"] = float(np.mean(P[~cat, 1] >= np.quantile(P[cat, 1], 0.1)))
    out["balanced_acc"] = float(np.mean([np.mean(P[y == c].argmax(1) == c) for c in range(4)]))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("manifest"); ap.add_argument("audio"); ap.add_argument("onnx"); ap.add_argument("out")
    ap.add_argument("--head-steps", type=int, default=1500)
    ap.add_argument("--tune-steps", type=int, default=6000)
    ap.add_argument("--unfreeze-from", type=int, default=15)
    ap.add_argument("--lr", type=float, default=3e-5)
    ap.add_argument("--head-lr", type=float, default=3e-4)
    ap.add_argument("--batch", type=int, default=128)
    ap.add_argument("--seed", type=int, default=1)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    torch.manual_seed(args.seed)
    device = "cuda"
    data, inventory = load(args.manifest, args.audio, ("train", "validation"), os.path.join(args.out, "clips.pt"))
    json.dump({"args": vars(args), "fingerprint": fingerprint(args.manifest, ("train", "validation"), args.audio), **inventory},
              open(os.path.join(args.out, "inputs.json"), "w"), indent=1)
    print({s: {k: len(v) for k, v in d.items()} for s, d in data.items()}, "missing", len(inventory["missing"]), flush=True)
    fixed = [batch(np.random.default_rng(7), data["validation"], 256)]
    rng_val = np.random.default_rng(8)
    fixed += [batch(rng_val, data["validation"], 256) for _ in range(23)]
    model = Detector(args.onnx).to(device)
    rng = np.random.default_rng(args.seed)

    def run(steps, groups, tag):
        opt = torch.optim.AdamW(groups, weight_decay=1e-4)
        sched = torch.optim.lr_scheduler.OneCycleLR(opt, max_lr=[g["lr"] for g in groups], total_steps=steps, pct_start=0.1)
        clock = time.time()
        for step in range(1, steps + 1):
            x, r, y = batch(rng, data["train"], args.batch)
            loss = F.cross_entropy(model(x.to(device), r.to(device)), y.to(device))
            opt.zero_grad(); loss.backward(); opt.step(); sched.step()
            if step % 500 == 0 or step == steps:
                m = evaluate(model, fixed, device)
                print(f"{tag} step {step} loss {loss.item():.3f} " + " ".join(f"{k} {v:.3f}" for k, v in m.items()) + f" {time.time() - clock:.0f}s", flush=True)

    for p in model.yamnet.parameters(): p.requires_grad = False
    run(args.head_steps, [{"params": list(model.head.parameters()), "lr": 1e-3}], "head")
    for i, layer in enumerate(model.yamnet.layers):
        for p in layer.parameters(): p.requires_grad = i >= args.unfreeze_from
    tuned = [p for l in model.yamnet.layers for p in l.parameters() if p.requires_grad]
    run(args.tune_steps, [{"params": tuned, "lr": args.lr}, {"params": list(model.head.parameters()), "lr": args.head_lr}], "tune")
    torch.save(model.state_dict(), os.path.join(args.out, "detector.pt"))
    export = YamnetExport(model.yamnet.cpu().eval())
    path = os.path.join(args.out, "sound-model.onnx")
    torch.onnx.export(export, torch.zeros(WINDOW), path, input_names=["waveform"], output_names=["output_0", "output_1", "output_2"],
                      opset_version=17, dynamo=False)
    print("exported", path, flush=True)


if __name__ == "__main__":
    main()

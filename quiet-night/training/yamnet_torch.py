"""YAMNet in PyTorch with the published weights, batched over 0.96 s windows, and exportable with the same inputs and
outputs as the prebuilt ONNX model (waveform -> AudioSet scores, embedding, log-mel patch)."""
import numpy as np, onnx, torch, torch.nn as nn, torch.nn.functional as F
from onnx import numpy_helper

WINDOW = 15360  # 0.96 s at 16 kHz


def _initializers(path):
    m = onnx.load(path)
    inits = {i.name: numpy_helper.to_array(i) for i in m.graph.initializer}
    convs = []
    for n in m.graph.node:
        if n.op_type == "Conv":
            attrs = {a.name: onnx.helper.get_attribute_value(a) for a in n.attribute}
            convs.append((inits[n.input[1]], inits[n.input[2]], attrs["strides"][0], attrs["group"], attrs["kernel_shape"][0]))
        if n.op_type == "MatMul":
            for x in n.input:
                if x in inits and inits[x].shape == (257, 64): mel = inits[x]
                if x in inits and inits[x].shape == (1024, 521): dense_w = inits[x]
                if x in inits and inits[x].shape == (2, 257, 512): dft = inits[x]
        if n.op_type == "Add":
            for x in n.input:
                if x in inits and inits[x].shape == (521,): dense_b = inits[x]
    return convs, mel, dense_w, dense_b, dft


class YamnetTorch(nn.Module):
    def __init__(self, onnx_path):
        super().__init__()
        convs, mel, dense_w, dense_b, dft = _initializers(onnx_path)
        self.register_buffer("mel", torch.from_numpy(mel.copy()))
        self.register_buffer("window", torch.hann_window(400, periodic=True))
        self.register_buffer("frame_index", torch.arange(96)[:, None] * 160 + torch.arange(400)[None, :])
        n, k = torch.arange(400, dtype=torch.float64)[:, None], torch.arange(257, dtype=torch.float64)[None, :]
        self.register_buffer("dft_cos", torch.cos(2 * torch.pi * n * k / 512).float())
        self.register_buffer("dft_sin", -torch.sin(2 * torch.pi * n * k / 512).float())
        self.layers = nn.ModuleList()
        self.strides = []
        for w, b, stride, group, k in convs:
            conv = nn.Conv2d(w.shape[1] * group, w.shape[0], k, stride=stride, groups=group, padding=0)
            conv.weight.data = torch.from_numpy(w.copy())
            conv.bias.data = torch.from_numpy(b.copy())
            self.layers.append(conv)
            self.strides.append((stride, k))
        self.dense = nn.Linear(1024, 521)
        self.dense.weight.data = torch.from_numpy(dense_w.T.copy())
        self.dense.bias.data = torch.from_numpy(dense_b.copy())

    def spectrogram(self, wave):
        """wave: (B, 15360) -> (B, 96, 64) log-mel, as YAMNet computes it: 25 ms periodic-Hann frames every 10 ms,
        |rfft| at 512 points, 64 mel bands, log(mel + 0.001). Framing is a gather and the DFT a matrix product, so the
        model exports to ONNX as plain operators."""
        x = F.pad(wave, (0, 15600 - wave.shape[1]))  # YAMNet pads the end to 0.975 s so 96 frames fit
        frames = x[:, self.frame_index] * self.window
        real, imag = frames @ self.dft_cos, frames @ self.dft_sin
        return torch.log(torch.sqrt(real * real + imag * imag) @ self.mel + 0.001)

    def embed(self, patch):
        x = patch.unsqueeze(1)
        for conv, (stride, k) in zip(self.layers, self.strides):
            if k == 3:
                x = F.pad(x, (0, 1, 0, 1)) if stride == 2 else F.pad(x, (1, 1, 1, 1))
            x = F.relu(conv(x))
        return x.mean(dim=(2, 3))

    def forward(self, wave):
        patch = self.spectrogram(wave)
        embedding = self.embed(patch)
        scores = torch.sigmoid(self.dense(embedding))
        return scores, embedding, patch


class YamnetExport(nn.Module):
    """The single-window interface the app loads: a 1-D 0.96 s waveform in, scores (1, 521), embedding (1, 1024) and
    the log-mel patch (96, 64) out."""

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, waveform):
        scores, embedding, patch = self.model(waveform[:WINDOW].unsqueeze(0))
        return scores, embedding, patch[0]


if __name__ == "__main__":
    import sys, onnxruntime as ort
    path = sys.argv[1]
    model = YamnetTorch(path).eval()
    rng = np.random.default_rng(0)
    session = ort.InferenceSession(path)
    worst = [0, 0, 0]
    for _ in range(5):
        x = (rng.standard_normal(WINDOW) * rng.uniform(0.001, 0.3)).astype(np.float32)
        ref = session.run(None, {"waveform": x})
        with torch.no_grad():
            out = model(torch.from_numpy(x)[None])
        for i, (r, o) in enumerate(zip(ref, out)):
            worst[i] = max(worst[i], float(np.abs(r.reshape(-1) - o.numpy().reshape(-1)).max()))
    print("max abs difference scores/embedding/patch:", worst)

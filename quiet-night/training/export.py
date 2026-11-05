"""Exports a saved fine-tuned detector's YAMNet as the app's sound model and checks the ONNX file against PyTorch:

    python training/export.py models/yamnet.onnx <output folder>/detector.pt sound-model.onnx
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, onnxruntime as ort, torch
from finetune import Detector
from yamnet_torch import YamnetExport, WINDOW
onnx_path, state, out = sys.argv[1:4]
model = Detector(onnx_path)
model.load_state_dict(torch.load(state, map_location="cpu"))
export = YamnetExport(model.yamnet.eval())
torch.onnx.export(export, torch.zeros(WINDOW), out, input_names=["waveform"], output_names=["output_0", "output_1", "output_2"], opset_version=17, dynamo=False)
session = ort.InferenceSession(out)
rng = np.random.default_rng(3)
worst = [0.0, 0.0, 0.0]
for _ in range(5):
    x = (rng.standard_normal(WINDOW) * rng.uniform(0.001, 0.3)).astype(np.float32)
    ref = session.run(None, {"waveform": x})
    with torch.no_grad():
        got = export(torch.from_numpy(x))
    for i in range(3):
        worst[i] = max(worst[i], float(np.abs(ref[i] - got[i].numpy()).max()))
assert all(np.isfinite(ref[i]).all() for i in range(3)), "the exported model produced non-finite outputs"
assert ref[0].shape == (1, 521) and ref[1].shape == (1, 1024) and ref[2].shape == (96, 64), "unexpected output shapes"
assert max(worst) < 1e-4, f"ONNX and PyTorch disagree by {max(worst)}"
print("exported", out, os.path.getsize(out), "bytes; max abs difference ONNX vs PyTorch", worst)

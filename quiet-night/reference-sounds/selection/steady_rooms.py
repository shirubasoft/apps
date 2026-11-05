"""Room recordings steady enough to be a night room: the spread between the 90th and 10th percentile of their 1 s
levels, leaving out the first and last half second, is at most 6 dB. Prints the ids that pass."""
import json, subprocess, sys
import numpy as np
fetched, audio = sys.argv[1], sys.argv[2]
steady = []
for item in json.load(open(fetched)):
    if item.get("group") != "background" or "preview" not in item: continue
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", f"{audio}/{item['id']}.mp3", "-ac", "1", "-ar", "16000", "-f", "f32le", "-"], capture_output=True).stdout
    x = np.frombuffer(raw, np.float32)[8000:-8000]
    seconds = len(x) // 16000
    if seconds < 3: continue
    db = np.array([10 * np.log10(np.mean(x[s * 16000:(s + 1) * 16000] ** 2) + 1e-12) for s in range(seconds)])
    if np.percentile(db, 90) - np.percentile(db, 10) <= 6: steady.append(item["id"])
print(json.dumps(steady))

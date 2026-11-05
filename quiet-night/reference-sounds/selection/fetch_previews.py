"""Download Freesound HQ previews for the selected clips, saving metadata after every clip so a restart resumes."""
import json, os, re, sys, time, urllib.request, hashlib
sel, outdir = sys.argv[1], sys.argv[2]
done_path = sel.replace(".json", ".fetched.json")
items = json.load(open(sel))
known = {it["id"]: it for it in json.load(open(done_path))} if os.path.exists(done_path) else {}
UA = {"User-Agent": "Mozilla/5.0 (X11; Linux x86_64) sleep-noise-reference-fetch"}
def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return r.read(), r.geturl()
out = []
for it in items:
    if it["id"] in known and ("preview" in known[it["id"]] or "error" in known[it["id"]]):
        out.append(known[it["id"]]); continue
    path = f"{outdir}/{it['id']}.mp3"
    try:
        html, final = get(f"https://freesound.org/s/{it['id']}/")
        m = re.search(rb"https://cdn\.freesound\.org/previews/\d+/(\d+)_(\d+)-lq\.mp3", html)
        if not m:
            it["error"] = "no preview"
        else:
            url = m.group(0).decode().replace("-lq.mp3", "-hq.mp3")
            if not (os.path.exists(path) and os.path.getsize(path) > 0):
                data, _ = get(url)
                open(path, "wb").write(data)
                time.sleep(0.4)
            it["preview"] = url; it["page"] = final; it["sha256"] = hashlib.sha256(open(path, "rb").read()).hexdigest()
    except Exception as e:
        it["error"] = str(e)
    out.append(it)
    json.dump(out + [x for x in items if x["id"] not in {o["id"] for o in out}], open(done_path, "w"), indent=1)
    time.sleep(0.4)
print("done", sum("preview" in o for o in out), "of", len(items), flush=True)

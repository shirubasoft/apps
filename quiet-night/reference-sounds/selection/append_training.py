"""Appends fetched training-only clips to the committed manifest, keeping its order and fields."""
import hashlib, json, os, sys
manifest, fsdir = sys.argv[1], sys.argv[2]
items = json.load(open(manifest))
have = {i["id"] for i in items}
added = 0
for path in sys.argv[3:]:
    for it in json.load(open(path)):
        if "preview" not in it or it["id"] in have: continue
        mp3 = f"{fsdir}/{it['id']}.mp3"
        if not os.path.exists(mp3) or os.path.getsize(mp3) == 0: continue
        items.append({"id": it["id"], "group": it["group"], "split": it["split"], "license": it["license"], "uploader": it["uploader"],
                      "title": it["title"], "page": it["page"], "preview": it["preview"],
                      "sha256": hashlib.sha256(open(mp3, "rb").read()).hexdigest(), "labels": it["labels"]})
        have.add(it["id"]); added += 1
items.sort(key=lambda x: (x["split"], x["group"], x["id"]))
with open(manifest, "w") as fh:
    json.dump(items, fh, indent=1); fh.write("\n")
from collections import Counter
print("added", added, sorted(Counter((i["group"], i["split"]) for i in items).items()))

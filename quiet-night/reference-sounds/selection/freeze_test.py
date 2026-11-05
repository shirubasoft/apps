"""Freeze test split 2: the used test clips join validation, cat targets become vocal cats only, and fresh clips from
uploaders absent from all other splits become the test split."""
import hashlib, json, random, re, sys
manifest, fsdir, steady_rooms, steady_fans = sys.argv[1:5]
VOCAL = {"Meow", "Caterwaul", "Hiss", "Growling"}
NOT_VOCAL_TITLE = re.compile(r"purr|eating|snor|whirr", re.I)
items = json.load(open(manifest))
for i in items:
    if i["split"] == "test": i["split"] = "validation"
    labels = set(i["labels"])
    if i["group"] == "cat" and not labels & VOCAL and ("Purr" in labels or NOT_VOCAL_TITLE.search(i["title"])):
        i["group"] = "other"
fetched = {}
for path in sys.argv[5:]:
    for it in json.load(open(path)):
        if "preview" in it: fetched[it["id"]] = it
rng = random.Random(20261105)
rooms = json.load(open(steady_rooms)); fans = json.load(open(steady_fans))
rain = sorted(r for r in rooms if "Rain" in fetched[r]["labels"])
crickets = sorted(r for r in rooms if "Cricket" in fetched[r]["labels"] or "Insect" in fetched[r]["labels"])
test_rooms = set(rng.sample(sorted(fans), 4)) | set(rng.sample(rain, 5)) | set(crickets)
def entry(it, group):
    mp3 = f"{fsdir}/{it['id']}.mp3"
    return {"id": it["id"], "group": group, "split": "test", "license": it["license"], "uploader": it["uploader"],
            "title": it["title"], "page": it["page"], "preview": it["preview"],
            "sha256": hashlib.sha256(open(mp3, "rb").read()).hexdigest(), "labels": it["labels"]}
have = {i["id"] for i in items}
for id_, it in fetched.items():
    if id_ in have: continue
    if it["group"] == "background":
        if id_ in test_rooms: items.append(entry(it, "background"))
    else:
        items.append(entry(it, it["group"]))
items.sort(key=lambda x: (x["split"], x["group"], x["id"]))
with open(manifest, "w") as fh:
    json.dump(items, fh, indent=1); fh.write("\n")
from collections import Counter
print(len(items), sorted(Counter((i["group"], i["split"]) for i in items).items()))
test_uploaders = {i["uploader"] for i in items if i["split"] == "test"}
other_uploaders = {i["uploader"] for i in items if i["split"] != "test"}
print("uploader overlap between test and the rest:", len(test_uploaders & other_uploaders))

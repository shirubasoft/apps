"""Fan rooms for the test split: FSD50K clips labelled Mechanical_fan (CC0/CC-BY), from uploaders absent from the
manifest and from select_test.py's output, at most two per uploader. Titles that are not a steady fan (a wind machine,
a laser cutter, a jet engine, a blacksmith, a fan slowing down or ventilation shutting down) were left out by id."""
import csv, json, sys
root, manifest, picked, out = sys.argv[1:5]
gt = root + "/FSD50K.ground_truth/"; md = root + "/FSD50K.metadata/"
current = json.load(open(manifest)); test = json.load(open(picked))
known = {e["uploader"] for e in current} | {e["uploader"] for e in test}
used = {e["id"] for e in current} | {e["id"] for e in test}
OK = {"http://creativecommons.org/publicdomain/zero/1.0/": "CC0-1.0", "http://creativecommons.org/licenses/by/3.0/": "CC-BY-3.0"}
info = {**json.load(open(md + "dev_clips_info_FSD50K.json")), **json.load(open(md + "eval_clips_info_FSD50K.json"))}
BAD = {"Speech", "Music", "Human_voice", "Thunder", "Thunderstorm", "Bird", "Bird_vocalization_and_bird_call_and_bird_song", "Dog", "Vehicle",
       "Car", "Motor_vehicle_(road)", "Engine", "Traffic_noise_and_roadway_noise", "Explosion", "Boom", "Cat", "Meow", "Motorcycle"}
NOT_A_STEADY_FAN = {174916, 328757, 414483, 201183, 17301, 271658, 395881}
candidates, per = [], {}
for split in ("eval", "dev"):
    for r in csv.DictReader(open(gt + f"{split}.csv")):
        labels = set(r["labels"].split(",")); f = r["fname"]; i = info[f]
        if "Mechanical_fan" not in labels or labels & BAD or i["license"] not in OK or int(f) in used or i["uploader"] in known: continue
        if per.get(i["uploader"], 0) >= 2: continue
        per[i["uploader"]] = per.get(i["uploader"], 0) + 1
        if int(f) in NOT_A_STEADY_FAN: continue
        candidates.append({"id": int(f), "group": "background", "split": "test", "labels": sorted(labels), "license": OK[i["license"]],
                           "uploader": i["uploader"], "title": i["title"], "source": split})
json.dump(candidates, open(out, "w"), indent=1)
print(len(candidates))

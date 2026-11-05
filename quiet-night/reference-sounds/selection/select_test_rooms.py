"""Night-time room recordings: fans, rain and crickets (CC0/CC-BY), for train/validation/test."""
import csv, json, random, sys
root, manifest, out = sys.argv[1:4]
gt = root + "/FSD50K.ground_truth/"; md = root + "/FSD50K.metadata/"
voc = {r[2]: r[1] for r in csv.reader(open(gt + "vocabulary.csv"))}; mid = {n: m for m, n in voc.items()}
pp = json.load(open(md + "pp_pnp_ratings_FSD50K.json"))
info = {**json.load(open(md + "dev_clips_info_FSD50K.json")), **json.load(open(md + "eval_clips_info_FSD50K.json"))}
OK = {"http://creativecommons.org/publicdomain/zero/1.0/": "CC0-1.0", "http://creativecommons.org/licenses/by/3.0/": "CC-BY-3.0"}
current = json.load(open(manifest))
taken = {i["id"] for i in current}
known_uploaders = {i["uploader"] for i in current}
def pred(f, n): r = pp.get(f, {}); return mid.get(n) in r and sum(r[mid[n]]) / len(r[mid[n]]) >= 0.99
BAD = {"Speech", "Music", "Human_voice", "Thunder", "Thunderstorm", "Wind", "Bird", "Bird_vocalization_and_bird_call_and_bird_song", "Dog", "Vehicle", "Car", "Motor_vehicle_(road)", "Engine", "Traffic_noise_and_roadway_noise", "Stream", "Waves_and_surf", "Ocean", "Splash_and_splatter", "Explosion", "Boom", "Gurgling"}
rng = random.Random(11)
picked = []
def pick(split, fsdsplit, kind, n):
    rows = {r["fname"]: set(r["labels"].split(",")) for r in csv.DictReader(open(gt + fsdsplit + ".csv"))}
    names = {"fan": ["Mechanical_fan"], "rain": ["Rain"], "crickets": ["Cricket", "Insect"]}[kind]
    c = sorted(f for f, l in rows.items() if any(x in l for x in names) and not l & BAD and info[f]["license"] in OK
               and any(pred(f, x) for x in names) and int(f) not in taken and int(f) not in {p["id"] for p in picked}
               and info[f]["uploader"] not in known_uploaders)
    rng.shuffle(c)
    per = {}
    count = 0
    for f in c:
        up = info[f]["uploader"]
        if per.get(up, 0) >= 2: continue
        per[up] = per.get(up, 0) + 1
        picked.append({"id": int(f), "group": "background", "split": split, "labels": sorted(rows[f]), "license": OK[info[f]["license"]], "uploader": up, "title": info[f]["title"]})
        count += 1
        if count >= n: break
    print(split, kind, count, "of", len(c))
pick("test", "eval", "fan", 10); pick("test", "eval", "rain", 8); pick("test", "eval", "crickets", 8)
json.dump(picked, open(out, "w"), indent=1)

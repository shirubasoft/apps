"""A fresh, frozen test set: unused FSD50K eval clips first, then dev clips from uploaders absent from train/validation."""
import csv, json, random, sys
root, manifest, out = sys.argv[1:4]
gt = root + "/FSD50K.ground_truth/"; md = root + "/FSD50K.metadata/"
voc = {r[2]: r[1] for r in csv.reader(open(gt + "vocabulary.csv"))}; mid = {n: m for m, n in voc.items()}
pp = json.load(open(md + "pp_pnp_ratings_FSD50K.json"))
info = {**json.load(open(md + "dev_clips_info_FSD50K.json")), **json.load(open(md + "eval_clips_info_FSD50K.json"))}
OK = {"http://creativecommons.org/publicdomain/zero/1.0/": "CC0-1.0", "http://creativecommons.org/licenses/by/3.0/": "CC-BY-3.0"}
m = json.load(open(manifest))
used = {i["id"] for i in m}
for f in sys.argv[4:]:
    used |= {x["id"] for x in json.load(open(f))}
known_uploaders = {i["uploader"] for i in m}
rows = {}
for split in ("eval", "dev"):
    for r in csv.DictReader(open(gt + split + ".csv")):
        rows[r["fname"]] = (split, set(r["labels"].split(",")))
def pred(f, names): r = pp.get(f, {}); return any(mid.get(x) in r and sum(r[mid[x]]) / len(r[mid[x]]) >= 0.99 for x in names)
STREET = {"Car_passing_by", "Traffic_noise_and_roadway_noise", "Vehicle_horn_and_car_horn_and_honking", "Engine", "Idling", "Accelerating_and_revving_and_vroom", "Engine_starting"}
KEEP = {"Speech", "Male_speech_and_man_speaking", "Female_speech_and_woman_speaking", "Child_speech_and_kid_speaking", "Conversation", "Crying_and_sobbing", "Alarm", "Telephone", "Ringtone", "Doorbell", "Knock", "Door", "Clock", "Tick-tock", "Rain", "Raindrop", "Wind", "Mechanical_fan", "Water_tap_and_faucet", "Toilet_flush", "Sink_(filling_or_washing)", "Typing", "Computer_keyboard", "Cough", "Sneeze", "Breathing", "Walk_and_footsteps", "Piano", "Guitar", "Music", "Microwave_oven", "Dishes_and_pots_and_pans", "Keys_jangling", "Laughter", "Television", "Cricket", "Insect"}
DIST = {"Dog", "Bark", "Thunder", "Thunderstorm", "Siren", "Train", "Aircraft", "Fixed-wing_aircraft_and_airplane", "Slam", "Chicken_and_rooster", "Crow", "Applause", "Bird", "Bird_vocalization_and_bird_call_and_bird_song", "Explosion", "Gunshot_and_gunfire", "Fireworks", "Screaming", "Shout", "Yell", "Bus", "Truck", "Rail_transport", "Boom", "Power_tool", "Drill", "Sawing", "Hammer", "Crash_cymbal", "Drum", "Growling"}
TARGET = {"Cat", "Meow", "Purr", "Hiss", "Motorcycle", "Car", "Car_passing_by", "Traffic_noise_and_roadway_noise", "Vehicle_horn_and_car_horn_and_honking", "Motor_vehicle_(road)", "Vehicle", "Engine", "Idling", "Accelerating_and_revving_and_vroom", "Race_car_and_auto_racing", "Truck", "Bus", "Engine_starting", "Skateboard"}
GROUPS = {
    "cat": (lambda l: bool(l & {"Meow", "Caterwaul", "Hiss"}) and not l & {"Speech", "Music", "Human_voice", "Motor_vehicle_(road)"}, {"Meow", "Cat"}, 22),
    "motorcycle": (lambda l: "Motorcycle" in l and not l & {"Speech", "Music", "Human_voice", "Cat", "Meow"}, {"Motorcycle"}, 22),
    "car": (lambda l: bool(l & {"Car", "Car_passing_by", "Traffic_noise_and_roadway_noise", "Vehicle_horn_and_car_horn_and_honking"}) and bool(l & STREET) and not l & {"Speech", "Music", "Human_voice", "Motorcycle", "Cat", "Meow", "Race_car_and_auto_racing"}, {"Car", "Car_passing_by", "Traffic_noise_and_roadway_noise", "Vehicle_horn_and_car_horn_and_honking"}, 22),
    "other": (lambda l: bool(l & KEEP) and not l & DIST and not l & TARGET, None, 40),
}
rng = random.Random(1106); picked = []
for group, (ok, names, n) in GROUPS.items():
    cands = []
    for f, (split, l) in rows.items():
        i = info[f]
        if int(f) in used or i["license"] not in OK or not ok(l): continue
        if split != "eval" or i["uploader"] in known_uploaders: continue
        strength = pred(f, names if names else (l & KEEP))
        cands.append((0 if split == "eval" else 1, 0 if strength else 1, rng.random(), f))
    cands.sort()
    per = {}
    for _, _, _, f in cands:
        up = info[f]["uploader"]
        if per.get(up, 0) >= 2: continue
        per[up] = per.get(up, 0) + 1
        picked.append({"id": int(f), "group": group, "split": "test", "labels": sorted(rows[f][1]), "license": OK[info[f]["license"]], "uploader": up, "title": info[f]["title"], "source": rows[f][0], "predominant": pred(f, names if names else (rows[f][1] & KEEP))})
        if sum(1 for p in picked if p["group"] == group) >= n: break
json.dump(picked, open(out, "w"), indent=1)
from collections import Counter
print(Counter((p["group"], p["source"], p["predominant"]) for p in picked))

"""More training-only 'other' clips from FSD50K dev (CC0/CC-BY), spread over kinds of harmless or must-stay-audible sounds.
Clips already in the manifest and uploaders who appear in the validation or test splits are left out."""
import csv, json, random, sys
root, manifest, out = sys.argv[1:4]
gt = root + "/FSD50K.ground_truth/"; md = root + "/FSD50K.metadata/"
info = json.load(open(md + "dev_clips_info_FSD50K.json"))
OK = {"http://creativecommons.org/publicdomain/zero/1.0/": "CC0-1.0", "http://creativecommons.org/licenses/by/3.0/": "CC-BY-3.0"}
current = json.load(open(manifest))
taken = {x["id"] for x in current}
held_uploaders = {x["uploader"] for x in current if x["split"] in ("validation", "test")}
rows = {r["fname"]: set(r["labels"].split(",")) for r in csv.DictReader(open(gt + "dev.csv"))}
DIST = {"Dog", "Bark", "Thunder", "Thunderstorm", "Siren", "Train", "Aircraft", "Fixed-wing_aircraft_and_airplane", "Slam",
        "Chicken_and_rooster", "Crow", "Applause", "Bird", "Bird_vocalization_and_bird_call_and_bird_song", "Explosion",
        "Gunshot_and_gunfire", "Fireworks", "Screaming", "Shout", "Yell", "Bus", "Truck", "Rail_transport", "Boom", "Power_tool",
        "Drill", "Sawing", "Hammer", "Crash_cymbal", "Drum", "Growling"}
TARGET = {"Cat", "Meow", "Purr", "Hiss", "Motorcycle", "Car", "Car_passing_by", "Traffic_noise_and_roadway_noise",
          "Vehicle_horn_and_car_horn_and_honking", "Motor_vehicle_(road)", "Vehicle", "Engine", "Idling",
          "Accelerating_and_revving_and_vroom", "Race_car_and_auto_racing", "Truck", "Bus", "Engine_starting", "Skateboard"}
KINDS = [
    ("voice", 160, {"Speech", "Male_speech_and_man_speaking", "Female_speech_and_woman_speaking", "Child_speech_and_kid_speaking",
                    "Conversation", "Laughter", "Giggle", "Chuckle_and_chortle", "Cough", "Sneeze", "Breathing", "Crying_and_sobbing", "Whispering", "Sigh"}),
    ("appliance", 110, {"Mechanical_fan", "Microwave_oven", "Clock", "Tick-tock", "Typing", "Computer_keyboard", "Printer", "Television", "Blender"}),
    ("water", 100, {"Rain", "Raindrop", "Water_tap_and_faucet", "Toilet_flush", "Sink_(filling_or_washing)", "Drip", "Pour", "Stream", "Wind"}),
    ("household", 120, {"Door", "Knock", "Dishes_and_pots_and_pans", "Walk_and_footsteps", "Keys_jangling", "Cupboard_open_or_close",
                        "Drawer_open_or_close", "Cutlery_and_silverware", "Zipper_(clothing)", "Scissors", "Writing", "Packing_tape_and_duct_tape"}),
    ("music", 70, {"Piano", "Guitar", "Music", "Singing", "Bowed_string_instrument"}),
    ("nature", 50, {"Cricket", "Insect"}),
]
KEEP = set().union(*(k[2] for k in KINDS))
rng = random.Random(11)
picked, per = [], {}
for kind, n, names in KINDS:
    cands = [f for f, l in rows.items()
             if info[f]["license"] in OK and int(f) not in taken and l & names and not l & DIST and not l & TARGET
             and info[f]["uploader"] not in held_uploaders]
    rng.shuffle(cands)
    got = 0
    for f in cands:
        up = info[f]["uploader"]
        if per.get(up, 0) >= 3 or int(f) in taken: continue
        per[up] = per.get(up, 0) + 1
        taken.add(int(f))
        i = info[f]
        picked.append({"id": int(f), "group": "other", "split": "train", "labels": sorted(rows[f]), "license": OK[i["license"]],
                       "uploader": up, "title": i["title"]})
        got += 1
        if got >= n: break
    print(kind, got, "of", len(cands), "candidates")
for w in range(3):
    json.dump(picked[w::3], open(out.replace(".json", f"-{w}.json"), "w"), indent=1)
print(len(picked))

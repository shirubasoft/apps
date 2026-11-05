# Selecting the reference sounds

These scripts chose the clips in `../manifest.json` from [FSD50K](https://zenodo.org/records/4060432) version 1.0: its ground truth (`FSD50K.ground_truth/`) and clip metadata (`FSD50K.metadata/`). They choose by labels, licences, uploaders, Freesound's predominance ratings and level steadiness, never by how the app performs on a clip.

The test split was frozen in these steps:

1. `select_test.py` takes unused CC0 and CC-BY clips from FSD50K's evaluation half, from uploaders absent from the manifest, at most two per uploader, seed 1106:
   - 22 cats whose labels include Meow, Caterwaul or Hiss;
   - cars with street labels;
   - motorcycles;
   - 40 sounds that must stay audible or are harmless.

   The cat rule accepts Hiss without a Cat label and leaves out cats labelled only Growling; every cat it chose is labelled Cat.
2. `select_test_rooms.py` and `select_test_fans.py` propose rain, cricket and fan rooms from unseen uploaders.
3. `fetch_previews.py` downloads Freesound's HQ previews, resumably, and records each preview's address and checksum.
4. `steady_rooms.py` keeps the rooms whose 1 s levels spread at most 6 dB.
5. `freeze_test.py` writes the manifest:
   - the earlier test split joins validation;
   - cat targets become vocal cats;
   - 4 fans, 5 rain rooms and every steady cricket room are drawn with seed 20261105;
   - the fresh clips become the test split.

`select_more_others.py` and `append_training.py` added training-only harmless sounds from uploaders absent from validation and test.

# Evaluation history

This page records each frozen test split, what it was judged against, and its results. A test split is run once for a fixed candidate; after that it is spent, and a later claim needs a fresh split. Results from different splits or target definitions are not a controlled comparison of the app's versions.

## Test split 1, quiet-night-v0.1.0

Cats were any cat sound, purring included. The criteria were version 3 in `tests/QuietNight.Tests/Evaluation/PassResult.cs`. The split was run on 5 November 2026 for commit `371e1e2` and again for the masking choices on `901cab3`:

| Noises masked | Passes |
| --- | --- |
| All three | 1 of 15 |
| Cars switched off | 1 of 10 |
| Cats only | 2 of 5 |

The goal of five passes per category was not met. Most failures came from rooms with a fan running, which the prebuilt YAMNet hears much like an engine. The split then joined validation.

## Change of scope: vocal cats

The user, asked after v0.1.0, chose to mask vocal cats only. A cat recording is a target when its labels include Meow, Caterwaul, Hiss or Growling. Recordings without those labels that are labelled Purr, or titled as purring, eating, snoring or whirring, count as harmless in every split. Purring is quiet and rarely what wakes a sleeper, and it sounds much like the engine and fan hums the model confuses.

## Test split 2

Frozen in commit `ecfc688`. It holds clips from FSD50K's evaluation half, from uploaders found nowhere else in the reference sounds; `reference-sounds/selection` holds the scripts and seeds that chose them:

- 22 vocal cats;
- 21 cars;
- 8 motorcycles, enough for two passes;
- 37 harmless sounds;
- 11 steady rooms: 4 fans, 4 rain and 3 crickets.

Development since the freeze used the validation split only. Validation clips are held out from training; their uploaders and the trainer's own validation rooms partly overlap with training's.

- **Sound model:** YAMNet's later layers were fine-tuned (`training/finetune.py`), and its heads retrained.
- **Harmless prior:** set to 0.5.

A paired comparison isolates the fine-tuning. It ran both sound models on the same validation scenarios, with heads trained by the same procedure and identical settings: a harmless prior of 0.5, a bedtime cover that ignores hops louder than it covers, and confirmed sounds that their pauses cannot argue out.

| Noises masked | Prebuilt YAMNet | Fine-tuned YAMNet |
| --- | --- | --- |
| Only cats, cat passes | 3 of 11 | 8 of 11 |
| All three, every pass | 2 of 35 | 11 of 35 |

The bundled sound model was produced by `training/finetune.py` as of commit `fbb25bd`, against manifest fingerprint `17b5a08e32b0b172`. On the same scenarios it passes 8 of 11 cat passes with only cats masked, and 9 of 35 passes with all three noises masked. Two later candidates were not adopted:

- With more development cats in training, the script passed 7 of 11 cat passes.
- The script's later labels, ported from the app's trainer, passed 6 of 11 cat passes but 13 of 35 with all three noises.

Cats are the user's main concern, so the cats-only result chose the candidate.

### Result

Frozen test split 2 was evaluated once on 5 November 2026 using candidate `8e36550`, with the bundled run-3 sound model (`sound-model.onnx`, SHA-256 `137fcc3e…5428`) and its heads (`sound-head.bin` `576c9ff5…29d`, `frame-head.bin` `2b8c8435…a6a7`). Run 3 was selected before the test was opened, on cats-only validation. Its producing script revision was `fbb25bd`, with manifest fingerprint `17b5a08e32b0b172`; the current training script produces a different candidate. Acceptance is judged by criteria version 3; versions 2 and 1 are diagnostics. All three masking choices were specified before the run and are reported in full. The cat passes of the different choices share scenarios, so they are not independent observations.

| Noises masked | Cat passes | Motorcycle passes | Car passes |
| --- | --- | --- | --- |
| All three | 0 of 5 | 0 of 2 | 0 of 5 |
| Cars switched off | 0 of 5 | 0 of 2 | — |
| Only cats | 0 of 5 | — | — |

The goal of five cat passes was not met; the test split holds enough unseen motorcycles for only two passes. With only cats masked, the detector recognised 2 of the 16 test cats that needed masking for at least half a second, so most cat passes covered 7–62 % of the cats against the oracle's 100 %. The other failures were protected sounds boosted for up to 1.8 s and quiet nights boosted up to 4.3 % of the time.

The validation results above do not carry over to this split. Its cats came from uploaders found nowhere else, and they were selected by label. FSD50K rates whether a labelled sound is predominant in its clip, and the splits differ sharply:

- validation: all 46 cats rated predominant;
- training: 101 of 151 cats rated predominant;
- test split 2: 3 of 22 cats rated at all, and only 1 of those predominant.

Many test cats are likely faint or mixed with other sounds.

Coverage is controller coverage with an assumed 250 ms Bluetooth delay; what reaches the ear also depends on the earbuds' volume and seal. Split 2 is now spent, and a further held-out claim needs fresh test data.


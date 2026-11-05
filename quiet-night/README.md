# Quiet Night

An Android app for sleeping through cats, motorcycles and cars on the street. Wear Bluetooth or wired earbuds, start the night, and lock the phone. A soft noise plays in your ears all night and, the moment the phone's microphone hears a disturbing sound, the parts of the spectrum that sound occupies rise just enough to cover it, then settle again.

## Why masking, not noise cancellation

Active noise cancellation plays an inverted copy of a sound into the ear within a fraction of a millisecond. A phone app cannot: Bluetooth adds roughly 150–300 ms between the phone and the earbuds, and apps cannot reach the earbuds' own noise-cancelling microphones. Quiet Night therefore masks. Earbuds with their own noise cancellation help further, especially against engine rumble; turn it on if yours have it.

"Low latency" here means how fast the masker reacts. The microphone path decides within one or two 10 ms hops; Android's mixer applies the new volume on its next cycle; the earbuds then add their own delay. A sustained rise, such as an approaching motorcycle, is anticipated by a quarter second so the masker is already loud enough when the sound reaches the ear. The first quarter second of a sudden, isolated sound cannot be covered through Bluetooth; wired earbuds remove most of that delay.

## Use it

1. Install `QuietNight.apk` from the latest `quiet-night-v` release on Android 11 or later.
2. Put your earbuds in. The app refuses to start without them, because played from the speaker the masker would be heard by the microphone and chase itself.
3. On **Night**, choose which noises to mask: cats, motorcycles, cars, or any of them. Set the masker volume so you can no longer hear the room, choose how loud it may get for a noise (**Strongest boost**) and how readily it reacts, then tap **Start**. Allow microphone and notification access when asked, and allow the app to run all night if Android offers to restrict it. Keep the phone charging.
4. If a noise wakes you, tap **Save the last 20 seconds** and say what it was.
5. On **Train**, record your own cat, your street, or harmless sounds such as your fan or rain (**Not a disturbance**), then tap **Train**. Training runs on the phone in about a minute. The new detector is kept only when it recognises the held-back end of your recordings better than the current one without losing more than three points on reference sounds it never trained on. **Use the prebuilt model again** undoes it.

The night stops by itself if the earbuds disconnect or Android would route the masker to the speaker. A phone call or another app taking audio focus mutes the masker until it is back.

## Known limitations

YAMNet hears a steady fan hum, and some appliances, much like an idling engine. In a room with a fan running the masker may treat the fan as traffic and rise with sounds it should leave alone. Recording the fan on **Train** as **Not a disturbance** gives the detector examples of it to learn from.

## How it works

[docs/architecture.md](docs/architecture.md) describes the listening path, the two detection stages, the masker and training.

- **Sound model:** Google's prebuilt [YAMNet](https://github.com/tensorflow/models/tree/master/research/audioset/yamnet) (AudioSet, Apache-2.0), in the [ONNX conversion by Audio Magic](https://huggingface.co/audiomagic/yamnet-onnx), with its later layers fine-tuned by `training/finetune.py` on the training sounds mixed into night rooms. The result, `src/QuietNight/Resources/Raw/sound-model.onnx`, runs with ONNX Runtime and is attached to each release.
- **Trained heads:** a fast per-10 ms detector and a head on YAMNet's features, trained by `src/QuietNight.Trainer` on the reference sounds and shipped in `src/QuietNight/Resources/Raw`. Each release attaches them too.
- **Reference sounds:** CC0 and CC-BY clips from [FSD50K](https://zenodo.org/records/4060432), previewed on Freesound. A cat counts as a target when it meows, yowls, hisses or growls; purring is quiet and counts as harmless. `reference-sounds/manifest.json` lists every clip with its licence, uploader, page and checksum, and its split: training, validation (for reporting and tuning), or test (only for the final evaluation). The held-out test audio is committed under `tests/QuietNight.Tests/ReferenceSounds` as 16 kHz Ogg Opus.

## Evaluation

`./test.sh` runs the unit tests and the five-pass evaluation and writes `artifacts/evaluation/five-pass-evaluation.md`; each release attaches that report. Each pass mixes four held-out cat, motorcycle or car recordings and two held-out sounds that must stay audible (speech, alarms, phones, doors, rain and the like) into a held-out night room with phone-microphone self-noise, and runs the app's exact listening path with the bundled models. Cats and cars play 10–25 dB above the room, motorcycles 15–30 dB, and the other sounds 8–22 dB. The report lists every pass and whether it meets the criteria documented in `tests/QuietNight.Tests/Evaluation/PassResult.cs`; the goal is five passes per category. The test split's recordings come from uploaders found nowhere else in the reference sounds, which leaves enough motorcycles for two passes. [docs/evaluation-history.md](docs/evaluation-history.md) records each frozen test split and its results.

Coverage in the report is controller coverage: it assumes the bedtime masker, at the volume the user chose, just covers the room's usual peaks. What reaches the ear also depends on the earbuds' seal and volume, which the app cannot measure.

## Build and run

Install the .NET SDK pinned by `global.json`, Java 17, the Android SDK and the MAUI Android workload, then:

```bash
cd quiet-night
dotnet workload install maui-android --skip-manifest-update
./build.sh
```

The build runs the tests and the evaluation, builds the AppHost and publishes `artifacts/QuietNight.apk` with `arm64-v8a` and `x86_64` libraries. The APK is signed with the developer key .NET Android generates; upgrading between builds from different machines needs an uninstall first.

With an emulator or USB-debugging phone connected:

```bash
./run-android.sh
```

or through Aspire:

```bash
aspire start --non-interactive
aspire resource quiet-night-android start --non-interactive
```

To rebuild the models, download the training and validation clips listed in the manifest into one folder as `<id>.mp3`:

1. `scripts/fetch-models.sh` downloads the prebuilt YAMNet into `models/` at a pinned revision and checksum.
2. `training/finetune.py`, on a CUDA GPU, fine-tunes it into the sound model; its docstring gives the command.
3. `dotnet run --project src/QuietNight.Trainer -- train reference-sounds/manifest.json <folder> src/QuietNight/Resources/Raw/sound-model.onnx src/QuietNight/Resources/Raw` trains the heads on it.

`prepare <split>` encodes a split's clips as Opus.

App-specific CI builds and tests on every change; releases publish the APK, the models and the evaluation report from successful `main` builds under `quiet-night-v` tags.

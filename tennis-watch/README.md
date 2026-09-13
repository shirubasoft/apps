# Tennis Watch

A single-screen tennis scorer for Samsung Galaxy Watch 5 Pro, built with .NET MAUI for Wear OS.

Tap either **+** to award that side a point. The arrow undoes the last point, including a point that ended a game or set. Swipe left to open the next match, or create one when viewing the newest match. Swipe right to open the previous match. The score follows the drag and slides into place when the swipe completes.

Swipe up to reveal the red trash icon. Release after dragging at least 45% of the display height, with a minimum of 96dp, to delete the current match. Shorter swipes snap back. Deletion opens the previous match, or the next when deleting the first. Deleting the only match creates a fresh one.

The header shows the match number and creation date. Numbers remain stable after deletion. Matches saved by the first version retain their scores and show "Date unavailable", because that version did not record dates.

Points use 0, 15, 30, 40 and AD. Games require a two-point lead. Sets require six games with a two-game lead, with a first-to-seven, win-by-two tiebreak at 6–6. The small row shows current games in white and the two most recent completed sets in gray. Scoring continues after each set. Every match and its undo history are saved locally after each action.

## Build and install

Install the .NET SDKs pinned by `global.json` and `src/TennisWatch/global.json`, Java 17, the Android SDK, and the MAUI Android workload. The watch project uses .NET 10 to include `armeabi-v7a`, the [Watch 5 Pro ABI measured in this device study](https://arxiv.org/abs/2308.09092). .NET 11 no longer builds Android apps with the Mono runtime needed for that ABI.

```sh
cd src/TennisWatch
dotnet workload install maui-android --skip-manifest-update
cd ../..
./build.sh
```

The installable package is `artifacts/TennisWatch.apk`. It contains 32-bit ARM, 64-bit ARM, and x86-64 emulator libraries. Enable ADB debugging and wireless debugging on the watch, then use the pairing and connection addresses shown by the watch. [Samsung's connection guide](https://developer.samsung.com/sdp/blog/en/2024/04/30/connect-galaxy-watch-to-android-studio-over-wi-fi) explains where to find them.

```sh
adb pair WATCH_IP:PAIRING_PORT
adb connect WATCH_IP:DEBUG_PORT
adb -s WATCH_IP:DEBUG_PORT install -r artifacts/TennisWatch.apk
```

Open **Tennis** from the watch's app list. No phone companion is needed. The hardware Back button exits the app; horizontal swipes stay inside match history.

## Development

`./test.sh` runs the same scoring, persistence and gesture tests used by CI, then writes the repository's CRAP report to `artifacts/coverage/crap-score.md`.

```sh
aspire start --apphost src/TennisWatch.AppHost/TennisWatch.AppHost.csproj
aspire resource tennis-watch start
```

The AppHost launches `run-android.sh`, which builds, installs, opens the app and streams its logs from a connected watch or emulator. Set `ANDROID_SERIAL` before starting Aspire when more than one device is connected. Use `aspire stop` when finished.

App-specific CI builds the APK and runs tests. Releases publish the artifact from successful main CI under `tennis-watch-v` tags.

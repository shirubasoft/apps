# Verification

The release APK was installed in a Wear OS 5 emulator with a round 450 × 450 display at 320 dpi. The screenshots use controlled match data and come directly from that installed APK.

| State | Capture |
| --- | --- |
| 15–30, games 0–1 | [Score](screenshots/score.png) |
| Advantage with two completed sets, system font scale 1.3 | [Large text](screenshots/large-text.png) |
| 11–10 tiebreak, games 6–6 | [Tiebreak](screenshots/tiebreak.png) |
| New match, undo disabled | [Empty](screenshots/empty.png) |

On the installed release build, ADB touch input verified adding a point, undoing it, starting a match with a right swipe, returning with a left swipe, the oldest-match boundary, and cancellation of horizontal and vertical drags that start on a plus button. Restarting the app restored both the selected match and the ability to undo.

The 54 data-driven unit tests cover advantage scoring, set boundaries, extended tiebreaks, continued scoring, undo across boundaries, independent match histories, swipe thresholds, file persistence, and an interrupted temporary write. Coverage excludes generated JSON serialization code.

The APK contains `armeabi-v7a`, `arm64-v8a`, and `x86_64` native libraries. Physical Galaxy Watch 5 Pro testing remains to be done.

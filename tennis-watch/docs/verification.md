# Verification

`./build.sh` builds the development AppHost, runs 91 data-driven tests and packages the signed Release APK. The APK was installed in a Wear OS 5 emulator with a round 450 × 450 display at 320 dpi. These screenshots and the recording use controlled match data and come directly from the installed Release app.

| State | Capture |
| --- | --- |
| Match number and creation date, 15–30, games 0–1 | [Score](screenshots/score.png) |
| Advantage with two completed sets, system font scale 1.3 | [Large text](screenshots/large-text.png) |
| 11–10 tiebreak, games 6–6 | [Tiebreak](screenshots/tiebreak.png) |
| New match, undo disabled | [Empty](screenshots/empty.png) |
| Legacy match without a recorded creation date | [Migrated match](screenshots/legacy.png) |
| Held leftward drag revealing the next match | [Navigation](screenshots/swipe-next.png) |
| Held upward drag revealing the red trash icon | [Delete preview](screenshots/delete-preview.png) |
| Approved tennis-ball icon in the Wear OS app grid | [Launcher](screenshots/launcher.png) |
| Left/right navigation, new match, canceled deletion and full deletion | [Gesture recording](screenshots/gestures.mp4) |

ADB touch input verified navigation through existing matches, creation at the newest end, resistance at the oldest end, and point/undo controls after switching the score views. Short horizontal swipes starting on a plus button did not award points. Partial upward swipes, downward and diagonal gestures, and crossing the deletion threshold then returning below it left the match unchanged. Backgrounding the app during a held deletion canceled the gesture and restored the controls.

Full upward swipes deleted only the selected match. Tests covered the first, middle, last, and only remaining match, stable match numbers after deletion, restored selection and undo after restart, and navigation/deletion with system animations disabled. Release smoke checks repeated point, undo, navigation, fresh-match creation, canceled/full deletion, restart, and legacy migration.

The unit tests cover advantage scoring, set boundaries, extended tiebreaks, continued scoring, undo across boundaries, independent match histories, metadata validation, stable numbering, deletion selection, nonmutating previews, gesture axes and thresholds, file persistence, legacy migration, and an interrupted temporary write. Coverage excludes generated JSON serialization code.

The APK contains `armeabi-v7a`, `arm64-v8a`, and `x86_64` native libraries. Physical Galaxy Watch 5 Pro testing remains to be done.

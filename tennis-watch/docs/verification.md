# Verification

`./build.sh` builds the development AppHost, runs 104 data-driven tests and packages the signed Release APK. The APK was installed in a Wear OS 5 emulator with a round 450 × 450 display at 320 dpi. The deletion screenshot and recording were refreshed for the one-second hold; unchanged scoring and navigation captures are retained. All captures use controlled match data and come directly from an installed Release build.

| State | Capture |
| --- | --- |
| Match number and creation date, 15–30, games 0–1 | [Score](screenshots/score.png) |
| Advantage with two completed sets, system font scale 1.3 | [Large text](screenshots/large-text.png) |
| 11–10 tiebreak, games 6–6 | [Tiebreak](screenshots/tiebreak.png) |
| New match, undo disabled | [Empty](screenshots/empty.png) |
| Legacy match without a recorded creation date | [Migrated match](screenshots/legacy.png) |
| Held leftward drag revealing the next match | [Navigation](screenshots/swipe-next.png) |
| Red semicircle at the screen midpoint with a dark trash icon | [Delete preview](screenshots/delete-preview.png) |
| Approved tennis-ball icon in the Wear OS app grid | [Launcher](screenshots/launcher.png) |
| Early release cancels; a full one-second hold deletes before release | [Deletion recording](https://github.com/user-attachments/assets/6a3e000b-4be0-4ba0-a191-993f106e2a76) |

ADB touch input verified that a fast full upward swipe and a partial drag held for more than one second leave the match unchanged. No deletion occurs before the full second in the deletion zone. Pulling back resets the deadline, and reentering requires a fresh one-second hold. Releasing early or backgrounding the app cancels the pending deletion and restores the controls.

A completed hold deletes while the finger is still down. Continuing contact for another 1.4 seconds and then releasing does not delete a second match. A new gesture can delete again. Checks also covered left/right navigation after confirmation, point/undo, deletion of the only match, persistence after restart, and the full confirmation delay with system animations disabled. Android's vibrator service recorded the app's 80ms confirmation request.

The unit tests cover advantage scoring, set boundaries, extended tiebreaks, continued scoring, undo across boundaries, independent match histories, metadata validation, stable numbering, deletion selection, nonmutating previews, gesture axes and thresholds, the one-second deadline, cancellation and reentry, one confirmation per gesture, file persistence, legacy migration, and an interrupted temporary write. Coverage excludes generated JSON serialization code. The CRAP gate's maximum improves from 12 on main to 10.

The APK contains `armeabi-v7a`, `arm64-v8a`, and `x86_64` native libraries. Physical Galaxy Watch 5 Pro testing, including the feel of the vibration, remains to be done.

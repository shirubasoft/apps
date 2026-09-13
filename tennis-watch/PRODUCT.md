# Tennis Watch
<!-- impeccable:product-schema: 1 -->

## Platform
Native Android app for Wear OS. Target device: Samsung Galaxy Watch 5 Pro.

## Purpose
A tennis player records points during a match by tapping the left or right plus button on their watch.

## Scope
The single screen contains a match number and creation date, two point scores, one plus button per side, a divider, a small games/set score, and undo beneath. Swipe left opens the next match and creates one at the end of history. Swipe right opens the previous match. Swipe up raises a red semicircle with a dark trash icon to the middle of the screen. A continuous one-second hold there confirms deletion with a short vibration. Releasing early or pulling back cancels. Match scores, dates, numbers, selection, and undo history persist locally. Player names, settings, and match completion screens are outside this app's scope.

## Appearance
Preserve the sketch’s composition with a minimal black-and-white finish. Points and the plus controls lead; match metadata, the games row, and undo have less visual emphasis. Red identifies the deletion gesture. The approved yellow-green tennis-ball icon identifies the app in the launcher. The scoring screen must remain legible during a quick glance on court.

## Scoring assumption
Standard advantage games and a seven-point tiebreak at six games each. The small row shows games in the current set alongside completed set scores. Matches continue until the user starts another one. The curved arrow undoes the last point.

## Stack
.NET 10 MAUI for the watch’s 32-bit Android runtime, .NET 11 for tests and the development host, a pure C# scoring library, xUnit tests, and an Aspire AppHost for development launching.

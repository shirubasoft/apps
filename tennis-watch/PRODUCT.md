# Tennis Watch
<!-- impeccable:product-schema: 1 -->

## Platform
Native Android app for Wear OS. Target device: Samsung Galaxy Watch 5 Pro.

## Purpose
A tennis player records points during a match by tapping the left or right plus button on their watch.

## Scope
The supplied sketch defines the single screen: two point scores, one plus button per side, a divider, a small games/set score, and undo beneath. No player names, match completion screen, menus, settings, or other features. Swipe right creates a new match; swipe left opens the previous match. Match scoring and undo history persist locally.

## Appearance
Preserve the sketch’s composition with a minimal black-and-white finish. Points and the plus controls lead; the games row and undo have less visual emphasis. The scoring screen must remain legible during a quick glance on court.

## Scoring assumption
Standard advantage games and a seven-point tiebreak at six games each. The small row shows games in the current set alongside completed set scores. Matches continue until the user starts another one. The curved arrow undoes the last point.

## Stack
.NET 10 MAUI for the watch’s 32-bit Android runtime, .NET 11 for tests and the development host, a pure C# scoring library, xUnit tests, and an Aspire AppHost for development launching.

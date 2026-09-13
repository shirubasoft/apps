---
name: Tennis Watch
description: Bold, glanceable tennis scoring for a round Wear OS watch.
colors:
  background: "#000000"
  score: "#F4F6F2"
  control-fill: "#252B27"
  secondary: "#929B94"
  separator: "#8B938D"
  divider: "#343936"
  deletion: "#FF6B6B"
typography:
  display:
    fontFamily: "sans-serif-condensed"
    fontSize: "48sp"
    fontWeight: 700
    fontFeature: "tnum"
  match-number:
    fontFamily: "sans-serif-medium"
    fontSize: "14sp"
    fontWeight: 500
  match-date:
    fontFamily: "sans-serif"
    fontSize: "11sp"
    fontWeight: 400
  games:
    fontFamily: "sans-serif-medium"
    fontSize: "18sp"
    fontWeight: 500
    fontFeature: "tnum"
  separator:
    fontFamily: "sans-serif"
    fontSize: "13sp"
    fontWeight: 400
    fontFeature: "tnum"
  completed-set:
    fontFamily: "sans-serif"
    fontSize: "12sp"
    fontWeight: 400
    fontFeature: "tnum"
rounded:
  point-fill: "16dp"
spacing:
  score-entry: "8dp"
components:
  point-control:
    textColor: "{colors.score}"
    width: "48dp"
    height: "48dp"
    padding: "0dp"
  undo-control:
    textColor: "{colors.secondary}"
    width: "48dp"
    height: "48dp"
    padding: "0dp"
---

# Design System: Tennis Watch

## Overview

**Creative North Star: "Monochrome sports instrument"**

Condensed bold point scores and filled graphite plus controls lead a black watch face. A small match number and creation date sit above; current games, completed sets, and a gray undo arrow sit below. The composition supports a quick glance and a tap on court.

**Key Characteristics:**

- Condensed point numerals hold the strongest visual emphasis.
- Fixed touch areas surround compact artwork. The match number and date occupy the space above the points.

## Colors

The palette uses off-white and green-tinted grays on an OLED black ground.

### Neutral

| Token | Use |
| --- | --- |
| `background` | Watch face and surrounding screen |
| `score` | Point numerals, current games, and plus strokes |
| `control-fill` | Filled graphite shape behind each plus |
| `secondary` | Completed sets and undo stroke |
| `separator` | Small multiplication sign between point scores |
| `divider` | Thin line between points and games |
| `deletion` | Trash icon revealed by an upward drag |

## Typography

Android's condensed bold display face gives points their width and weight. The match number and current games use the medium face; the date, separator, and completed sets use regular sans-serif. The frontmatter defines each role's size and weight.

Labels are centered, single-line, and support system font scaling. All labels request tabular figures and disable native font padding. Point labels use native uniform text fitting from 24sp to the display size in 1sp increments, so advantage text and multi-digit tiebreak scores fit their fixed bounds. Match metadata fits from 9sp to its specified size. The date uses the current locale and stays secondary gray.

**The score hierarchy rule.** Points lead; current games are larger and brighter than completed sets. Keep the multiplication separator smaller and dimmer than the points.

## Layout

The layout centers a square whose side is the shorter display dimension. Coordinates below are relative to that square. Geometry uses dp; type uses sp.

The match number is centered at 18.5% height in a box 65% wide and 22dp high. The creation date is centered at 27.5% height in a box 70% wide and 20dp high.

The point row shares a center at 43% height. Plus controls sit at 12.5% and 87.5% width. Point labels sit at 33.2% and 66.8%, each in a box 25% of the square's width and 66dp high. The separator sits at 50% width in a box 7.5% wide and 40dp high.

The centered divider spans 50% of the square at 57.5% height and is 0.75dp thick. The games row sits at 65.5% height in a box 76% wide and 32dp high, with the score-entry gap between labels. Undo sits at 50% width and 82.5% height.

**The touch area rule.** Keep the fixed control dimensions in the frontmatter when adjusting screen proportions. The artwork is smaller than its touch area.

## Elevation & Depth

The screen is flat, without shadows. Graphite fills give the plus controls a visible body; text brightness and spacing separate points from match history.

## Shapes

Each plus sits on a filled rounded rectangle, 38dp wide and 42dp high, centered inside its control with the point-fill radius. The plus uses rounded 2.3dp strokes. Undo is an open curved arrow with rounded 1.8dp strokes and joins.

## Components

- The left and right plus controls share the filled SVG and have separate accessible descriptions. Pressing sets opacity to 0.55; release restores it to 1. Native button backgrounds are removed so the SVG defines the visible control.
- Point labels retain equal bounds and native fitting. Their accessible descriptions identify the side and current point text.
- The games row shows the latest two completed sets in the smaller secondary style, followed by current games in the brighter medium face. Accessible descriptions distinguish completed sets from current games.
- Undo uses the same touch dimensions as the plus controls. Enabled opacity is 0.8, pressed opacity is 0.55, and disabled opacity is 0.3. Release restores 0.8.

## Motion and deletion

Horizontal dragging moves the entire score face with the finger and reveals the adjacent match. Left advances through history and previews a fresh match at the newest end. Right shows previous matches. At the oldest match, a rightward drag moves the face at 18% of the finger distance and springs back. A completed horizontal swipe settles in 150ms; an incomplete swipe returns in 160ms.

Upward dragging reveals a red, 48dp trash icon centered at 80% height. Its opacity rises from 0.45 to 1 and its scale from 0.8 to 1 as the drag approaches the deletion threshold. Release beyond 45% of the viewport height, with a minimum of 96dp, deletes the match. The score exits upward in 130ms, and the selected remaining match enters from below in 180ms. A short swipe or a return below the threshold cancels deletion. All settling uses cubic ease-out and respects Android's animator-duration setting, including disabled animations.

## Launcher icon

The approved raster icon is a yellow-green tennis ball with ivory seams on black. Keep the source image and embedded generation prompt together. The launcher artwork is the only colored element outside the transient red deletion cue.

## Do's and Don'ts

- Do preserve the filled plus controls and condensed bold points as the scoring screen's main row.
- Do retain native text fitting, tabular figures, and fixed touch targets when adjusting proportions.
- Do keep secondary scoring and undo visually quieter than the points.
- Don't add names, menus, banners, extra controls, or a completion screen to this scoring view.
- Don't draw a hardware bezel inside the app.

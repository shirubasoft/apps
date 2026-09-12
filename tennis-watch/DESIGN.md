---
name: Tennis Watch
description: Minimal tennis scoring for a round Wear OS watch.
colors:
  background: "#000000"
  score: "#F3F5F0"
  secondary: "#949B92"
  control-outline: "#686C67"
  divider: "#424640"
typography:
  display:
    fontFamily: "sans-serif-light"
    fontSize: "42sp"
  games:
    fontFamily: "sans-serif-light"
    fontSize: "17sp"
  separator:
    fontFamily: "sans-serif-light"
    fontSize: "14sp"
  completed-set:
    fontFamily: "sans-serif-light"
    fontSize: "12sp"
rounded:
  control-outline: "11dp"
components:
  point-control:
    textColor: "{colors.score}"
    width: "48dp"
    height: "48dp"
    padding: "0dp"
  undo-control:
    textColor: "{colors.score}"
    width: "48dp"
    height: "48dp"
    padding: "0dp"
---

# Design System: Tennis Watch

## Overview

Large, lightweight point scores lead a single black watch screen. Outlined plus buttons sit beside the scores; the games row and undo control have less visual emphasis. The spacing follows the round display of the Samsung Galaxy Watch 5 Pro.

## Colors

The palette uses off-white and subtly green-tinted grays on black. `score` colors the point numerals, current games, and icon strokes. `secondary` colors the small multiplication separator and completed sets. `control-outline` defines the plus buttons; `divider` separates points from games without competing with either.

## Typography

Android's `sans-serif-light` supplies every score label. Labels are centered, single-line, and support system font scaling. The point numerals use native uniform text fitting from 24sp through the display size in 1sp increments, allowing large-text settings and wider scores to fit their bounds.

Current games are larger and brighter than completed sets. The multiplication sign remains small between the point scores.

## Layout

The layout centers a square whose side is the shorter display dimension. Positions scale with that square, while controls retain their fixed touch area.

The point row is centered at 48% of the square's height. Plus buttons sit at 12.5% and 87.5% of its width; the scores sit at 33% and 67%. The separator sits between them, slightly lower at 49% height.

A thin divider spans 43% of the square at 61.5% height. The centered games row sits at 68.5%, with 12dp between score entries. Undo sits near the bottom at 85.5%. The space above the point row remains open.

## Elevation & Depth

The screen is flat. Brightness, outlines, and spacing establish hierarchy without shadows or raised containers.

## Shapes

Each plus icon has a rounded square outline, 32dp across inside its touch target. Its border is 1.2dp thick; the plus uses rounded 1.6dp strokes. Undo uses an open curved arrow with rounded 1.7dp strokes. The divider is 0.75dp thick.

## Components

- The left and right plus controls use the same outlined SVG and retain separate accessible descriptions. Pressing a control sets its opacity to 0.55; release restores it to 1.
- Point labels share equal bounds, each 23% of the square's width and 62dp high. Their native fit preserves a single line.
- The compact score row shows the latest completed sets in the smaller secondary style, followed by current games in the brighter games style.
- Undo uses the same touch dimensions as the plus controls. Refresh sets its opacity to 0.75 when an undo is available and 0.3 when disabled.

## Do's and Don'ts

- Do preserve the two plus controls and the large point scores as the scoring screen's main row.
- Do retain native text fitting and fixed touch targets when adjusting proportions.
- Do keep secondary scoring and undo visually quieter than the points.
- Don't add names, menus, banners, extra controls, or a completion screen to this scoring view.
- Don't draw a hardware bezel inside the app.

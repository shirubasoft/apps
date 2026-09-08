---
name: Pen Sketch
description: Native Android controls around a monochrome sketch notebook.
colors:
  primary: "#246BCE"
  paper: "#FFFFFF"
  ink: "#252A31"
  grid: "#DBE0E6"
  canvas-matte: "#E4E9F0"
  surface-light: "#F1F4F8"
  surface-dark: "#161D28"
  foreground-dark: "#E7EDF7"
  muted-light: "#526174"
  muted-dark: "#BDC8DA"
  tonal-light: "#DFE8F5"
  tonal-dark: "#29374C"
typography:
  title:
    fontFamily: "Android system sans-serif"
    fontSize: "22sp"
    fontWeight: 700
  body:
    fontFamily: "Android system sans-serif"
    fontSize: "14sp"
  label:
    fontFamily: "Android system sans-serif"
    fontSize: "12sp"
rounded:
  control: "12dp"
spacing:
  compact: "4dp"
  header: "6dp"
  gap: "8dp"
  inset: "12dp"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.paper}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "4dp 12dp"
  button-tonal-light:
    backgroundColor: "{colors.tonal-light}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "4dp 12dp"
  button-tonal-dark:
    backgroundColor: "{colors.tonal-dark}"
    textColor: "{colors.foreground-dark}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    padding: "4dp 12dp"
  paper:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
---

# Design System: Pen Sketch

## Overview

A pen notebook

White paper holds the drawing. Blue marks the chosen tool, editing state, and animation trigger. The surrounding Android controls use slate tones and system text, with the canvas taking the space left by the controls.

This records the built MAUI interface. The notebook description comes from the implementation; it is not a separately approved concept. Native measurements below use dp for layout and sp for interface text. Canvas measurements use document units and scale with the paper. The sidecar's browser previews and synthesized color ramps are illustrative, not native specifications.

The interface uses:

- Monochrome artwork on white dotted paper.
- Blue selection and primary actions.
- Native controls with readable labels and horizontally scrolling tool rows.
- A shared canvas for Draw and Animate.

## Colors

The accent stays blue across themes. Neutrals separate the paper from its controls.

### Primary

`primary` colors Export, Play, the selected mode, selected tool, selected Start or End state, slider thumbs and filled tracks, and the enabled Pen only switch. The renderer uses the same accent for selection outlines and trigger markers. Filled buttons and trigger labels use `paper` text.

### Neutral

| Role | Light theme | Dark theme |
| --- | --- | --- |
| App background | `surface-light` | `surface-dark` |
| Title and tonal-button text | `ink` | `foreground-dark` |
| Status, hints, secondary labels | `muted-light` | `muted-dark` |
| Tonal controls and unfilled slider tracks | `tonal-light` | `tonal-dark` |

The canvas always uses `paper`, `ink`, `grid`, and `canvas-matte`. The matte fills the space around the fitted document. Android resource colors match the primary accent and the light or dark app background used for the status-bar theme.

**The Paper Rule.** Keep the document white and its artwork monochrome when the surrounding interface changes theme.

## Typography

The app leaves the font family to Android through MAUI. Title, body, and label tokens describe the repeated interface hierarchy. Button captions use mixed case. Line height and letter spacing follow the native controls.

The sketch title uses the title role and truncates at the end when space is tight. Buttons, the duration picker, and explanatory body copy use the body role. Status, contextual hints, opacity, and preview percentages use the label role. Hints wrap. The text-entry modal has a 24sp heading and a 20sp editor.

Canvas text is artwork. Skia's default typeface starts at 20 document units, wraps inside the object's bounds, and shrinks when needed to fit the height. It is independent of Android font scaling. The text-fit screenshot demonstrates a wrapped label; extremely small text boxes are not a readable typography target. Trigger captions use 13 document units.

**The Native Text Rule.** Preserve system font scaling for interface text and keep canvas text sizing tied to the document.

## Layout

The root grid respects safe areas. It uses 12dp side padding, 6dp top padding, 8dp bottom padding, and 8dp between rows. The header gives remaining width to the sketch title and sizes Sketch and Export to their content. Draw and Animate occupy equal columns below it, separated by the gap token.

The next row contains either drawing tools or animation controls. Drawing and selection actions scroll horizontally without a visible scrollbar. Animation controls use equal Start and End columns beside Play, followed by the trigger button, a 100dp duration picker, and a preview slider that takes the remaining width.

The canvas receives the remaining height. It fits the whole document without changing its aspect ratio and centers it in the matte. The default document is 360 by 640 document units. Extra controls or larger interface text reduce the displayed paper size. Bottom controls keep opacity above save status, Pen only, selection actions, and a wrapping hint. Related bottom rows use the compact gap.

The text-entry modal scrolls vertically with 24dp padding and 16dp spacing. Its editor grows with text and starts at a 160dp minimum height. The recorded screenshots cover a portrait phone, including dark theme with enlarged text. The implementation has no separate tablet or landscape composition.

## Elevation & Depth

Paper, matte, and app background create flat tonal layers. The app defines no custom shadows. Native controls supply their own platform feedback and rendering. There is no documented custom hover, focus-ring, or elevation scale to transfer to new controls.

## Shapes

Buttons share the control radius and have a minimum 48dp width and height. Their horizontal padding is the inset token and vertical padding is the compact token. These are minimum touch dimensions, not fixed dimensions that should clip enlarged text.

The paper has square corners. Drawn rectangles, circles, and squircles are user artwork. Their ink has round stroke caps and joins with a default width of 2.5 document units. Pen pressure changes freehand stroke width. The squircle renderer uses a continuous superellipse outline.

Selection uses a thin blue rectangle 4 document units outside the object, plus a white circular handle at the lower-right corner. The handle radius is 8 document units, with a blue outline and diagonal resize mark. Its hit tolerance maps to 24dp around the corner, independently of document zoom. Trigger captions use a 6-document-unit corner radius.

## Components

### Buttons and mode controls

Primary buttons use blue and white. Tonal buttons use the active theme's foreground and tonal background. The selected Draw or Animate mode, tool, and Start or End state use the primary appearance. Their semantic descriptions say selected or not selected, and the Android view receives the selected state.

Undo and Redo enable according to history. Export disables while rendering. Selection reveals Delete. In Draw it also reveals Copy, and text selection reveals Edit text. Delete and opacity editing disable during preview. Platform disabled styling remains native, with no app-defined disabled palette.

### Paper and selection

Editing shows grid dots spaced 16 document units apart, each with a 0.7-unit radius. Selection adds the resize handle. Preview hides the grid and selection so the transition can be read. Exported artwork omits editing aids; animated GIFs retain the trigger annotation.

The canvas has a semantic description of drawing, moving, and resizing. Individual rendered objects do not expose a separate accessibility tree. A screenshot verifies appearance, not TalkBack navigation or physical S Pen behavior.

### Animation controls

Start and End identify which pose is editable. Scrubbing enters preview and shows a percentage; choosing Start or End resumes editing. Play becomes Stop during playback. Trigger setup uses native action sheets and prompts, then asks the user to place the marker on the canvas.

The marker combines a blue ring, center dot, and a caption beginning with Tap or Type. The caption stays within the paper width and clips long labels. Opacity is visible in Animate and enables for a selected object outside preview. Both sliders have semantic descriptions.

Playback holds the trigger for 700ms, interpolates position, size, and opacity over the chosen duration with smoothstep easing, then holds the end for 600ms. Available durations are 0.3, 0.6, 1.0, 1.5, 2.0, and 3.0 seconds. Preview is user initiated; the app defines no separate reduced-motion behavior.

### Text entry and native system controls

The modal editor uses tonal fill, theme foreground, and a 300-character limit. Use text commits nonempty input. Microphone opens Android speech recognition; handwriting conversion belongs to the installed keyboard. Dialogs, prompts, the duration picker, switch, and share sheet retain native behavior.

Pen only keeps a visible label and a semantic description explaining that fingers are ignored on the canvas. Save and render states appear as text in the status row. Save failure asks for an export copy; action failures use native alerts rather than a separate visual error component.

## Do's and Don'ts

### Do:

- Do keep paper, ink, and grid colors stable across app themes.
- Do preserve the minimum 48dp button targets and 8dp horizontal control gaps.
- Do let tool rows scroll, hints wrap, and the title truncate as interface text grows.
- Do pair selected colors with the existing semantic selected state.
- Do keep motion, opacity, and trigger feedback in the shared document view.

### Don't:

- Don't invert the artwork when applying dark theme to the controls.
- Don't treat canvas document units as dp or sp.
- Don't export selection handles or the editing grid.
- Don't copy browser-preview focus styles or synthesized ramps into the native theme as established tokens.

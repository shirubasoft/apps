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
  text-size-rail:
    typography: "{typography.label}"
    width: "60dp"
---

# Design System: Pen Sketch

## Overview

A pen notebook

White paper holds the drawing. Blue marks the chosen tool, editing state, and animation trigger. The surrounding Android controls use slate tones and system text. The canvas takes the space left by the controls, with a persistent text-size rail at its right edge.

This records the built MAUI interface. The notebook description comes from the implementation; it is not a separately approved concept. Native measurements below use dp for layout and sp for interface text. Canvas measurements use document units, labeled px in the text-size control, and scale with the paper. Selection affordances retain their screen size. The sidecar's browser previews and synthesized color ramps are illustrative, not native specifications.

The interface uses:

- Monochrome artwork on white dotted paper.
- Blue selection and primary actions.
- Native controls with readable labels and horizontally scrolling tool rows.
- A shared, zoomable canvas for Draw and Animate, with named viewport presets.

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

The sketch title uses the title role and truncates at the end when space is tight. Buttons, the duration picker, and explanatory body copy use the body role. Status, contextual hints, opacity, preview percentages, and text-size captions use the label role. Hints wrap. The text-entry modal has a 24sp heading and a 20sp editor.

Canvas text is artwork. Skia's default typeface starts at 20 document units and wraps inside the object's bounds. The text-size rail changes selected text in canvas pixels, including the animation end state. Resizing text adjusts its bounds to fit the content within the paper; fitting may reduce the font when the content reaches the canvas limits. Canvas text is independent of Android font scaling. Extremely small text boxes are not a readable typography target. Trigger captions use 13 document units.

**The Native Text Rule.** Preserve system font scaling for interface text and keep canvas text sizing tied to the document.

## Layout

The root grid respects safe areas. It uses 12dp side padding, 6dp top padding, 8dp bottom padding, and 8dp between rows. The header gives remaining width to the sketch title and sizes Sketch and Export to their content. Draw and Animate occupy equal columns below it, separated by the gap token. The dimensions button sits below the modes, taking the remaining width beside Rotate and Fit. These top rows use 6dp spacing.

The next row contains either drawing tools or animation controls. Drawing and selection actions scroll horizontally without a visible scrollbar. Animation controls use equal Start and End columns beside Play, followed by the trigger button, a 100dp duration picker, and a preview slider that takes the remaining width.

Below 640dp root width, the controls sit above and below the canvas workspace. The workspace receives the remaining height and reserves 60dp for the text-size rail, separated from the canvas by 8dp. The paper fits without changing its aspect ratio and centers in the matte. Extra controls or larger interface text reduce the displayed paper size. Bottom controls keep opacity above save status, Pen only, selection actions, and a wrapping hint. Related bottom rows use the compact gap.

At 640dp root width and above, the header, modes, viewport actions, tools, and bottom controls move into a 320dp panel on the left. That panel scrolls vertically. A 12dp gap separates it from the canvas workspace, which keeps the text-size rail on its right. This arrangement uses the height available on a landscape phone.

The text-entry modal scrolls vertically with 24dp padding and 16dp spacing. Its editor grows with text and starts at a 160dp minimum height. Native emulator evidence covers portrait and landscape phone layouts, the viewport sheet, selected text, a desktop-sized canvas, zoom, and dark theme with enlarged interface text in `docs/screenshots/viewport-phone-*.png`. The target device remains an Android phone. Preset names describe the sketch canvas, not additional supported app platforms or device classes.

## Elevation & Depth

Paper, matte, and app background create flat tonal layers. The app defines no custom shadows. Native controls supply their own platform feedback and rendering. There is no documented custom hover, focus-ring, or elevation scale to transfer to new controls.

## Shapes

Buttons share the control radius and have a minimum 48dp width and height. Their horizontal padding is the inset token and vertical padding is the compact token. These are minimum touch dimensions, not fixed dimensions that should clip enlarged text.

The paper has square corners. Drawn rectangles, circles, and squircles are user artwork. Their ink has round stroke caps and joins with a default width of 2.5 document units. Pen pressure changes freehand stroke width. The squircle renderer uses a continuous superellipse outline.

Selection uses a 1dp blue rectangle 4dp outside the object, plus a white circular handle at the lower-right corner. The handle radius is 8dp, with a 2dp blue outline and diagonal resize mark. The renderer compensates for fit and zoom to keep these dimensions stable on screen. The resize hit tolerance is 24dp around the corner. Trigger captions use a 6-document-unit corner radius and scale with the artwork.

## Components

### Buttons and mode controls

Primary buttons use blue and white. Tonal buttons use the active theme's foreground and tonal background. The selected Draw or Animate mode, tool, and Start or End state use the primary appearance. Their semantic descriptions say selected or not selected, and the Android view receives the selected state.

Undo and Redo enable according to history. Export disables while rendering. Selection reveals Delete. In Draw it also reveals Copy, and text selection reveals Edit text. Delete and opacity editing disable during preview. Platform disabled styling remains native, with no app-defined disabled palette.

### Canvas viewport and navigation

The dimensions button opens a native action sheet whose options combine a viewport name and dimensions. Rotate swaps canvas width and height. Viewport changes fit and center the artwork proportionally, including text, strokes, animation end states, and the trigger. Returning through preset or orientation changes preserves artwork scale across round trips, including after saves and edits.

Two-finger gestures pan and zoom from the fitted scale up to eight times that scale. Fit centers the whole paper again. Panning, zooming, and Fit keep document geometry unchanged. Viewport changes and changes to the available canvas area also restore the fitted view. Preset dimensions and operating instructions belong in README.md.

### Paper and selection

Editing shows grid dots spaced 16 document units apart, each with a 0.7-unit radius. Selection adds the resize handle. Preview hides the grid and selection so the transition can be read. Exported artwork omits editing aids; animated GIFs retain the trigger annotation.

The canvas has a semantic description of drawing, moving, resizing, two-finger navigation, and Fit. Individual rendered objects do not expose a separate accessibility tree. TalkBack navigation and physical S Pen behavior remain unverified by the emulator screenshots.

### Text-size rail

The persistent rail labels the control Text size and places the numeric canvas-pixel value below its track. Its native Slider rotates 90 degrees counterclockwise inside a tall layout, retaining a 48dp cross-axis touch area and Android range semantics. Sliding up enlarges the text. The semantic description explains the canvas-pixel unit and direction; Up and Down key presses change the value by one.

The slider enables for selected text outside preview. Otherwise it stays visible, disables, and shows Select text below the track. Its usual range is 1 to 256 canvas pixels; the maximum expands to include larger selected text. During a drag, the artwork updates live and the completed gesture creates one Undo step. In Animate, it edits the selected Start or End state.

### Animation controls

Start and End identify which pose is editable. Scrubbing enters preview and shows a percentage; choosing Start or End resumes editing. Play becomes Stop during playback. Trigger setup uses native action sheets and prompts, then asks the user to place the marker on the canvas.

The marker combines a blue ring, center dot, and a caption beginning with Tap or Type. The caption stays within the paper width and clips long labels. Opacity is visible in Animate and enables for a selected object outside preview. The opacity and preview sliders have semantic descriptions.

Playback holds the trigger for 700ms, interpolates position, size, opacity, and font size over the chosen duration with smoothstep easing, then holds the end for 600ms. Available durations are 0.3, 0.6, 1.0, 1.5, 2.0, and 3.0 seconds. Preview is user initiated; the app defines no separate reduced-motion behavior.

### Text entry and native system controls

The modal editor uses tonal fill, theme foreground, and a 300-character limit. Use text commits nonempty input. Microphone opens Android speech recognition; handwriting conversion belongs to the installed keyboard. Dialogs, prompts, the duration picker, switch, and share sheet retain native behavior.

Pen only keeps a visible label and a semantic description about ignoring fingers for drawing. Two-finger navigation remains available while it is enabled. Save and render states appear as text in the status row. Save failure asks for an export copy; action failures use native alerts rather than a separate visual error component.

## Do's and Don'ts

### Do:

- Do keep paper, ink, and grid colors stable across app themes.
- Do preserve the minimum 48dp button targets and 8dp horizontal control gaps.
- Do let tool rows scroll, hints wrap, and the title truncate as interface text grows.
- Do keep the text-size rail visible and selection affordances stable while the paper zooms.
- Do use the scrolling control panel at 640dp root width and above.
- Do pair selected colors with the existing semantic selected state.
- Do keep motion, opacity, and trigger feedback in the shared document view.

### Don't:

- Don't invert the artwork when applying dark theme to the controls.
- Don't treat canvas document units as dp or sp.
- Don't export selection handles or the editing grid.
- Don't copy browser-preview focus styles or synthesized ramps into the native theme as established tokens.

# Pen Sketch

<!-- impeccable:product-schema 1 -->

## Platform

android

## Stack

.NET MAUI for Android, following the repository stack. Native stylus events feed a Skia canvas.

## Users

People drawing UI sketches on a Galaxy phone with an S Pen to communicate interface behavior to an LLM on a PC.

## Product purpose

Draw shapes and text, show how they move or change after a visible trigger, and export the result as PNG or GIF.

## Capabilities and constraints

Draw and Animate are the main views. Shapes include rectangles, circles, and squircles. Shapes can move, resize, expand, shrink, and fade. Text can be typed, handwritten, or dictated. Animations identify a tap location or text input as their trigger. Color editing is outside the current request.

Sketch canvases support mobile, tablet, laptop, and desktop viewports in either orientation. Two-finger navigation supports detail work on a phone. A vertical slider adjusts selected text size, including animation end states.

## Working assumptions

The first version stores one active sketch locally and offers Android's share sheet for transferring exports. Android handwriting recognition depends on the installed keyboard. The name Pen Sketch is provisional. Animation uses one start-to-end transition per sketch.

## Implementation approach

This implementation proceeded directly from the two-view brief with native controls. No concept-selection roll, approved image comp, or quality board was produced. The visual design and code-first approach are working assumptions; no approval or seed is recorded.

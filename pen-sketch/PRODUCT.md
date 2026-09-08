# Pen Sketch

<!-- impeccable:product-schema 1 -->

## Platform

android

## Stack

Working assumption: .NET MAUI for Android, following the repository stack. Native stylus events feed a Skia canvas. The user has been offered a stack choice; no reply has arrived.

## Users

People drawing UI sketches on a Galaxy phone with an S Pen to communicate interface behavior to an LLM on a PC.

## Product purpose

Draw shapes and text, show how they move or change after a visible trigger, and export the result as PNG or GIF.

## Capabilities and constraints

Draw and Animate are the main views. Shapes include rectangles, circles, and squircles. Shapes can move, resize, expand, shrink, and fade. Text can be typed, handwritten, or dictated. Animations identify a tap location or text input as their trigger. Color editing is outside the current request.

## Working assumptions

The first version stores one active sketch locally and offers Android's share sheet for transferring exports. Android handwriting recognition depends on the installed keyboard. The name Pen Sketch is provisional. Animation uses one start-to-end transition per sketch.

## Implementation approach

This implementation proceeded directly from the two-view brief with native controls. No concept-selection roll, approved image comp, or quality board was produced. The visual design and code-first approach are working assumptions; no approval or seed is recorded.

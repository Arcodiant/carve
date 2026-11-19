---
description: >-
  ColourRule predicts low-albedo faces too dark
---
# B-8 — ColourRule predicts low-albedo faces too dark

**Status:** Open

**Severity:** Minor

**Milestone:** — (off-milestone)

## Symptom

`ColourRule.Lit` (linked from `scenarios/Carve.Scenario.Crate/ColourRule.cs` by most scenarios) predicts dark faces 3 to 4 levels below what `render` draws, against its tolerance of 2. T-94 (M-9) found it on the rover's dark grey wheels (#3C3C3C) on 2026-11-18, e.g. the face with normal (0.98, -0.2, 0) predicted (55, 58, 62) and drawn (58, 61, 66), and normal (-0.2, 0.98, 0) predicted (60, 67, 78) and drawn (64, 71, 81). Every lighter face judged was within 2. The measurements are in `G:\My Drive\artifacts\carve\M-9\T-94\ray-and-shade-diagnostic.txt`.

The person ruled at T-94 that its wheels' #3C3C3C faces are held at tolerance 4 meanwhile, with 2 everywhere else, and that the light model's miss is filed here.

## Cause

Not investigated. The fit (T-82, M-19's `scenario-sky` measurements) was made on grey, black and white, and the miss suggests a term that does not scale with albedo at low values (an ambient or specular floor, or sRGB rounding).

## Fix

When fixed, take T-94's dark-wheel tolerance back to 2.

## Verified by

---
description: >-
  An agent measures distances, gaps and overlaps, and declares relationships between parts that are re-checked after every edit
---
# M-11 — Measure and constrain

**Status:** Planned

**One-liner:** *An agent measures distances, gaps and overlaps, and declares relationships between parts that are re-checked after every edit*

## What this milestone delivers

An agent checks a model with numbers instead of judging pixels. It measures the distance and gap between parts, overlap and clearance, and casts a ray ("what is directly below this wheel?"). It declares relationships the model keeps ("rests on", "centred on", "does not pass through", "symmetric in X"), and Carve re-checks each one after every edit and reports any that broke.

Content: a sample model with its relationships declared, an edit that breaks one so the report shows it, and descriptions with worked examples.

Plans against [D-3](../design/D-3-constraints-and-measurement.md).

**Depends on:** M-4 (queries and selections) and M-9 (relationships are between parts).

## Open at planning

- Which relationships come first, and how each is written as tool arguments.
- Whether a broken relationship is a warning on the edit, or refuses it.
- Tolerances: absolute in metres, or relative to the parts' size.
- One measuring tool with a kind, or a tool per measure.
- What overlap and clearance mean for open or non-manifold parts.

## Required tasks

## Retrospective

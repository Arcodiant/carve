---
description: >-
  Smooth normals with marked hard edges and a subdivide edit, so a character reads soft and painterly in render and game
---
# M-18 — Soft characters

**Status:** Planned

**One-liner:** *Smooth normals with marked hard edges and a subdivide edit, so a character reads soft and painterly in render and game*

## What this milestone delivers

A character stops looking faceted. A mesh can be smooth, with hard edges marked, and `subdivide` turns extruded blocks into soft shapes. Render and export draw the same normals.

Content: the gardener soft, with a creased hat brim, flat beside smooth.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06. This takes hard edges and subdivide out of [M-14](M-14-bevel-and-finish.md).

**Depends on:** M-16. It runs before M-17, because Orcward's first character goes to a look review.

**Order.** T-42, T-43, T-44 in sequence. Stronger model on T-43, Sonnet on the rest.

## Required tasks

- [ ] [T-42](../tasks/T-42-smooth-normals-and-hard-edges.md) — An agent marks a mesh smooth and marks hard edges, and render and export draw the same per-corner normals
- [ ] [T-43](../tasks/T-43-subdivide.md) — subdivide smooths a mesh with Catmull-Clark, keeping ids, colours, groups, hard edges and weights
- [ ] [T-44](../tasks/T-44-soft-character-scenario.md) — The test character is subdivided and smoothed through the tools and shown soft and with hard creases, in its own suite

## Retrospective

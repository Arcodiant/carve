---
description: >-
  Bevel and the finishing edits the modifier design chooses, with hard and smooth edges
---
# M-14 — Bevel and finish

**Status:** Planned

**One-liner:** *Bevel and the finishing edits the modifier design chooses, with hard and smooth edges*

## What this milestone delivers

An agent finishes a hard-surface model so it reads as more than a grey box: it bevels edges, controls which edges are hard and which smooth, and exports the result with matching normals. Other finishing edits (subdivide, solidify, array) come in as D-5 decides.

Content: the crate and M-9's sample vehicle bevelled, before and after, in Carve's render and through the viewer.

Plans against [D-5](../design/D-5-bevel-and-the-modifier-question.md).

**Depends on:** M-3 (export normals: it exports every edge hard and leaves smoothing to a later milestone) and M-5 (cuts, which bevel builds on).

**Changed 2026-11-06.** Hard and smooth edges and `subdivide` arrive earlier, in [M-18](M-18-soft-characters.md). This milestone keeps bevel, and solidify and array as D-5 decides.

## Open at planning

- Whether bevel is a destructive edit or a modifier kept in the file (D-5).
- Smoothing control: by angle, by edges marked with a tool, or both.
- Bevel segments and profile, and what happens at corners where three edges meet.
- Which of subdivide, solidify and array belong here, and which later.

## Required tasks

## Retrospective

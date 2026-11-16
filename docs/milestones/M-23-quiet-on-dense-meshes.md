---
description: >-
  Open, check, subdivide, unwrap and export report repeated warnings as counts with samples, so a dense cage costs hundreds of tokens and not thousands
---
# M-23 — Quiet on dense meshes

**Status:** Planned

**One-liner:** *Open, check, subdivide, unwrap and export report repeated warnings as counts with samples, so a dense cage costs hundreds of tokens and not thousands*

## What this milestone delivers

A tool result on a dense mesh stays small enough to read, and still says what is wrong.

- Warnings of one kind are grouped: a count, the worst case and a few samples.
- A verbosity setting, and a way to get the full list when it is wanted.
- `unwrap` reports by island, not by face.
- A stated tolerance for non-planar faces, or export that triangulates them, so a subdivided quad mesh can stay quads in the file.

Content: the fps-arms cage opened, subdivided, checked and exported with each result under a stated size.

## Why

Raised by the fps-arms project (private repo `Arcodiant/fps-arms`, 2026-11-15), which built a rigged, textured pair of first-person arms and four tools with Carve 0.9.0 through script-written `.carve` files.

- Opening the 600-vertex quad cage listed about 250 non-planar faces, one warning each: about 12,000 tokens for one `open`.
- The subdivided mesh would have listed thousands, so the project let Carve subdivide and save, triangulated in its own script, and only then opened and exported.
- `unwrap` returned 5,000 to 9,000 tokens for each tool. One tool was rescaled in script to avoid a second unwrap.

## Open at planning

- Default size budget for each tool's result.
- Does the full list go to a file the agent can read?
- Is non-planarity a warning at all on a face that will be triangulated at export?

## Required tasks

Not planned yet.

## Retrospective

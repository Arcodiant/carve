---
description: >-
  bake, render's frames and tools/Carve.SampleTextures write PNGs through the fast writer M-33 built, not StbImageWriteSharp, once a shared home for it is decided
---
# T-190 — Carve's PNG writers on the fast writer

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

Carve's own PNGs are written by the fast writer M-33 built for patina, so a large bake or frame stops paying for
`StbImageWriteSharp`'s encoder.

## Scope

- The writers: `bake` (`src/Carve.Server/BakeTool.cs`, which writes RGB and grey files), `render`'s frames
  (`src/Carve.Server/RenderTool.cs`) and `tools/Carve.SampleTextures/Png.cs`.
- First decide, with the person, where a shared writer lives. `PngWriter` is in `Patina.Graphs`, which `Carve.Server`
  does not reference before M-29, and it writes RGBA only.
- Measure first: at carve's sizes the stb encode may not be worth moving.

## Notes

- Raised at M-33's planning (2026-11-22). The person kept carve's writers on stb for that milestone.
- Moving them changes the bytes of every frame, every bake and the eight committed `samples/textures/` PNGs, which
  `tests/Carve.MaterialsLoad.Tests` compares byte for byte with the exported `.glb`'s textures. List every check
  that moves and put them to the person in one question before changing any.
- Do not start before M-33 has landed.

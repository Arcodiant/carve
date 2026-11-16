---
description: >-
  Committed code generates the textured barrel's eight 256 x 256 PNG textures, and a test keeps the committed files equal to it pixel for pixel
---
# T-77 — Sample textures

**Status:** In Progress

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-55

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

The barrel's textures, the content of this milestone's mechanic, exist as committed PNGs. Committed code makes them, so they can be remade and their layout is checked against the barrel's UVs.

## Scope

- **A generator** in a small engine-free project, `tools/Carve.SampleTextures`, a console app that writes `samples/textures/`. It joins `Carve.slnx`.
  - It is deterministic: any noise is an integer hash of the texel and a fixed seed, with no `System.Random` and no clock.
  - PNG writing uses the encoder the product already depends on (`StbImageWriteSharp`) or one of the project's own.
- **The eight textures**, each 256 x 256 RGBA8 with alpha 255:

  | File | Space | What |
  |---|---|---|
  | `staves-base.png` | sRGB | 8 wood staves as columns of u, each 1/8 wide, each a slightly different tone, grain running along v, and a dark 2-texel gap at each stave edge |
  | `staves-normal.png` | linear | flat (128, 128, 255), with a groove at each stave edge (normals tilted toward the gap) |
  | `staves-occlusion.png` | linear, R | 255, falling to about 140 at the stave gaps |
  | `iron-base.png` | sRGB | dark iron grey with rust-orange patches from a hash-noise mask |
  | `iron-mr.png` | linear | from the same mask: clean is G (roughness) 0.35 and B (metallic) 1; rust is G 0.9 and B 0 |
  | `end-grain-base.png` | sRGB | growth rings round the centre (128, 128) |
  | `end-grain-normal.png` | linear | the rings as slight ridges |
  | `rune-emissive.png` | sRGB | black, with one white rune glyph of simple strokes, placed where faces 14 and 22 fall (below) |

- **Where the UVs put things** (T-80 lays these UVs with the real tools):
  - The side faces 0 to 31 get `project_uvs` `cylindrical` with the defaults (axis +Y, seam at -Z, through vertices 2, 10, 18, 26 and 34), into `[0, 0, 1, 1]`.
  - So each of the 8 side columns is 1/8 of u, which is why the staves are 1/8 wide. The hoop rows (faces 0 to 7, y 0 to 0.1, and faces 24 to 31, y 0.9 to 1.0) are bands of v.
  - The rune glyph sits inside the u and v rectangle of faces 14 and 22, column `k = 6` (vertex angles 270° to 315°, facing +Z and +X, toward `front`'s eye at (1.6, 1.2, 2.2)). Work that rectangle out from T-55's landed rule for the direction u rises from the seam, and the v rule.
  - The caps get `planar` from `+y` (face 33, the top) and from `-y` (face 32, the bottom), each into `[0, 0, 1, 1]`, so the rings are centred.
- **Tests (`unit`):**
  - The committed PNGs decode to exactly the generator's pixels, compared by pixel, not by file bytes, since a compressor may change between runtimes.
  - The rune's rectangle equals the UV bounds of faces 14 and 22 under `project_uvs` `cylindrical` on `TestMeshes.Barrel()`, through the Core's projection. If the glyph is moved by hand, this test fails.
  - Every texture is 256 x 256, opaque, and under 64 KB.

## Notes

- **Sonnet.**
- Content comes with its mechanic, in the same wave: it needs nothing of T-74, so it runs side by side with T-74 in the first wave and may run on beside T-75 and T-76.
- While it runs it owns:
  - `Carve.slnx`;
  - `tools/`;
  - `samples/textures/`;
  - its test, in `tests/Carve.Core.Tests` or a project of its own (a new project joins `tests.json`'s `core-unit` suite, which only this task edits while it runs).
- **Add a row to `CLAUDE.md`'s Layout** for `tools/Carve.SampleTextures` and `samples/textures/`. Only this task edits `CLAUDE.md` while it runs.
- **Write down for the dependants**, in this file's As built: each file's channels, the rune's UV rectangle, and how to remake the textures.
- **Set by the planner, open to the person's objection:** the colours, the 2-texel gap, the rust coverage and the glyph. The person sees them in T-80's frames.
- **Before the first code**, read this Scope against T-55's As built (`cylindrical`'s u direction and v rule) and T-53's barrel. Stop if the rune's faces or the hoop rows do not land where this Scope says.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - anything the milestone's *Decided at planning* does not settle;
  - a texture over 64 KB;
  - any loosened check.
- **Carried from earlier plans:**
  - each new check shown failing, as a table (include a glyph moved one column and a texture written v-flipped);
  - `finish` read whole, with its `Merge task/T-NN` line;
  - report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/review/T-77/`.
- Run `finish` from inside the worktree and leave the folder.

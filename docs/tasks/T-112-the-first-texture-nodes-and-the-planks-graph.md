---
description: >-
  noise, gradient, levels, blend and height_to_normal, and samples/graphs/planks.texgraph with its PNGs kept equal to the graph
---
# T-112 — The first texture nodes and the planks graph

**Status:** In Progress

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-110

## Goal

The five node types that, with T-110's `colour` and `output`, make one whole material, and that material itself:
`samples/graphs/planks.texgraph`, eight staves across u with gaps, a tone per stave and grain along v, committed with
its four PNGs at 256.

## Scope

- **Five node types in `src/Patina.Graphs`**, one file each, registered through T-110's registry. All parameters are
  image-relative, every node tiles, and evaluation is deterministic (T-110):
  - `noise`: value noise from T-77's integer hash (`TextureSet.Hash`, copied with a comment naming its source).
    Parameters: `cells` `[x, y]`, whole numbers of lattice cells across the image (so it tiles); `interpolation`
    `smooth` (smoothstep, as T-77) or `step` (one value per cell); `octaves` 1 to 4 (each doubles the cells and halves
    the weight, normalised to 0..1); and `seed` (an offset on the graph's seed). Greyscale.
  - `gradient`: `axis` `u` or `v`; `repeat`, a whole number; `shape` `linear` (0 to 1 across each repeat) or
    `triangle` (0 at each repeat's edges, 1 in its middle). Greyscale, sampled at texel centres.
  - `levels`: `in_low`, `in_high`, `out_low`, `out_high` and `gamma` (default 0, 1, 0, 1, 1). The output is clamped,
    and `out_low > out_high` inverts. Greyscale or colour in, the same out.
  - `blend`: inputs `a` and `b`, optional `mask`; `mode` `mix`, `multiply`, `add`, `subtract`, `min` or `max`;
    `opacity` 0..1. Colour if either input is colour (a greyscale input is spread to grey). In `mix`, the mask takes
    `a` where it is 0 and `b` where it is 1.
  - `height_to_normal`: input `height`; `strength` (slope per image width at height 1); radius 1 with wrap-around.
    Central differences, glTF tangent space (green up, image rows running down), unit length before encoding.
- **Tests (unit), with numbers worked by hand in the test:**
  - each node's value at chosen texels for given parameters;
  - tiling: the left and right columns, and the top and bottom rows, continue each other (the difference across the
    wrap is no larger than across any interior seam of the same image);
  - size-invariance: at 256 and at 1024, texels at the same uv agree to within one interpolation step;
  - `height_to_normal`: a flat height gives (128, 128, 255) up to rounding, a ramp in u tilts red the right way, and
    every encoded normal decodes to unit length within the rounding;
  - each node shown failing on a broken copy (a sign flipped, wrap-around removed): a table of breaks and what caught
    each.
- **The planks graph**, `samples/graphs/planks.texgraph`, size 256, with all four outputs. This is roughly the shape
  the person saw at planning:

  ```
  tone   = noise(cells 8x1, step)              a tone per stave
  grain  = noise(cells 64x2, smooth, 2 oct)    runs along v
  bevel  = gradient(u, repeat 8, triangle)     0 at each stave edge
  gap    = levels(bevel, in 0..0.2)            0 in the gap, 1 on the stave
  base   = colour #aa7040 x tone x grain x gap
  normal = height_to_normal(levels(bevel, in 0..0.25))
  rough  = levels(grain, out 0.55..0.8)
  occl   = levels(bevel, in 0..0.25, out 0.55..1)
  ```

  Work its numbers against the barrel at planning time, as M-7's retrospective asks. The barrel's staves are 32
  texels wide at 256 with boundaries at u = k/8 (`TextureSet.StaveWidth`), and the graph's must fall there too.
  Each gap is at least 3 texels on each side of a boundary (6 in all): M-7's one-texel gaps could not be sampled
  (T-80). The stave colour stays near T-77's (170, 112, 64) x 0.82..1.18, which is known to pass M-7's lit checks.
  A test proves the boundaries, the gap width and the colour range from the PNGs.
- **Committed PNGs**: `samples/graphs/planks/base_colour.png`, `normal.png`, `roughness.png` and `occlusion.png`,
  evaluated at 256. A unit test keeps them equal to the graph pixel for pixel, as `SampleTexturesTests` does for
  T-77's. There is one documented command to remake them: choose a small console under `tools/` or a test mode, and
  record it in CLAUDE.md's Layout. T-115's `export` must later write the same bytes, and T-115 tests that.
- `tests.json`: `patina-unit`'s inputs include `samples/graphs/**`.

## Notes

- Decided at planning (2026-11-17, by the person): these seven node types, with the parameters above, and the planks
  as the content.
- Content comes with its mechanic: the planks graph is this task's, not a later one's. The person looks at the four
  PNGs (contact sheet or Waystone review) before this task lands. Treat Waystone's "Commented" as unanswered until
  the person says approved (M-7).
- **Stop and ask** before landing: a parameter beyond those above, a change to a node's meaning, or planks whose
  gap, boundary or colour rule a test cannot hold.
- Owns `samples/graphs/`. T-116 copies its PNGs, and T-114's and T-115's scenarios build the same graph through the
  tools.
- Sonnet should do: the design is settled. The node files and their tests are mechanical against T-110's API.
- Runs beside T-113, which touches no file here. T-113's descriptions name these node types and parameters as
  written above, so a rename here is a stop.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it will add. Once patina-unit (T-110 adds it) is in `tests.json`, add it to this file's `**Verify:**` line (creating the line if there is none), with the task's other suites.

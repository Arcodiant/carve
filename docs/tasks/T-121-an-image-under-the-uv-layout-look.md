---
description: >-
  render mode uvs takes an image argument and draws a PNG under one mesh's layout, full inside the islands and dimmed outside, so padding and bakes can be seen
---
# T-121 — An image under the UV layout look

**Status:** In Progress

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit, server-regression, scenario-materials

## Goal

An agent sees a texture where it lands on a mesh's layout: `render mode=uvs image=<png>` draws the PNG under the
layout, so a bake's islands, its padding and any texel a face misses can be looked at. The look works on any PNG, so
M-7's barrel textures show it before there is a bake.

## Scope

- **`render`, `mode: "uvs"`, new argument `image`.** A PNG path, resolved as `define_material`'s texture paths are
  (relative to the working directory, through `ToolPaths`). It is read under M-7's rules (`MaterialTexture.Load`):
  a PNG, at most 4096, with the same problems reported in the same words. A missing file lists the PNGs in its folder,
  as T-81's missing texture does. `image` with any other mode is refused by name, as the `uvs` look refuses `view`
  today (`UvRefused` and the other modes' checks).
- **What is drawn** (the sketch agreed at planning, in M-24's file):
  - the image covers the uv unit square, u right and v down, at the place the layout already gives that square;
  - inside the mesh's islands, where today's look draws the bright fill, the image is drawn as stored;
  - outside the islands, where today's look is dim, it is drawn at half brightness, each channel halved, so padding
    shows as a fringe of island colour on a darker ground;
  - the white edges, yellow seams, face labels and highlight are drawn over it exactly as today;
  - the image's bytes are shown as stored, with no sRGB or linear conversion and no lighting (the look's own
    material, as every look but `shaded` has);
  - a single-channel or grey PNG shows as grey;
  - beyond the unit square the ground stays as today.
  - The caption names the image file and its size, for example `image wrench.occlusion.png 1024 x 1024`.
- **Sampling.** Work out how the engine samples the image in this look: nearest, or linear with or without mips. Probe
  it first, then choose what makes a pixel predictable from the PNG, and write it down in the code. Pixel checks use
  test images made of blocks several frame pixels wide, never single texels, since at `size: 1024` the window
  (`UvWindow.Growth` 8/7) gives under one frame pixel per texel of a 1024 image.
- **Tests:**
  - `unit` on `ToolHarness` for the arguments: refused with another mode, missing file with the folder's PNGs listed,
    a bad PNG, a PNG over 4096;
  - `regression` for the frames:
    - a 64 x 64 four-quadrant marker (`TestPng.Marker`) under the front-UV box: every quadrant's class inside the
      island, halved outside it, read at block centres worked from `UvLayoutPixels`;
    - a v-flipped image and a u-mirrored one each fail a check;
    - the edges and seams are unchanged where they cross the image;
    - a grey PNG.
- **Content:** scenario-materials gains one frame: `samples/barrel-textured.carve`'s staves mesh in the `uvs` look with
  `staves-base.png` under it. It is checked by the same block rule, using T-77's stave boundaries at u = k/8 and the
  gap colour from `TextureSet`. Add it after the existing frames and leave every existing frame and check as it was.
  The tool description names the argument and what it is for (seeing a bake or a texture on the layout).

## Notes

- Decided at planning (2026-11-18), by the person: the sketch in M-24's file. The image is full inside the islands,
  half outside, and edges and seams are over it.
- **Stop and ask** before landing: a sampling rule a pixel check cannot predict, a change to any existing `uvs` frame,
  or a threshold that is not worked from the image (M-7 lost two stops to a threshold probed on another material).
- Sonnet should do: the layout and its pixel rule exist (`UvLayout`, `UvLayoutPixels`). Probe the engine's sampling
  before writing checks.
- Runs beside T-120, which touches only `src/Carve.Core/Baking/`. It owns the `uvs` look's `image` argument; T-123
  uses it on the wrench's bakes.
- The person looks at the new frames before this lands (a Waystone review). Treat "Commented" as unanswered until the
  person says approved.

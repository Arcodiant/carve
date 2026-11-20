---
description: >-
  render mode uvs takes an image argument and draws a PNG under one mesh's layout, full inside the islands and dimmed outside, so padding and bakes can be seen
---
# T-121 — An image under the UV layout look

**Status:** Done

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit, server-regression, scenario-materials, scenario-crate, scenario-uvs

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

### As built

Built by Sonnet 5.5 (claude-sonnet-5-5).

- **`render mode=uvs image=<png>`** (`RenderTool.cs`). The path is resolved and read by `DefineMaterialTool.LoadTexture` (now `internal`, taking the tool name), so the rules and wording are define_material's, including the missing-file listing of the folder's PNGs. The image problem is collected with the other argument problems. `image` with any other mode is refused by name (`image: not with mode 'shaded'; ...`). The caption gains `image <file> <w> x <h>` after `uvs <mesh>`. The tool description names the argument and what it is for.
- **Drawing** (`UvLayout.Compose(..., imaged)`, `RenderSystem`). With an image the fill is four index ranges, each with its own material: the checker ground (as today), a quad over the unit square with the image at half brightness, the faces with the image as stored, and highlighted faces last with the checker's white cell (orange, as today). Without an image nothing changed: one range and one material, as before. The image is decoded to RGBA8 and uploaded twice (as stored; each channel `(c + 1) / 2`), sRGB, so the bytes round-trip through the sRGB target; unlit; the textures are released when the look changes (`UvImageDraw`, destroyed in `Install`). `LookHolder.Apply` gained a `variant` so a different image rebuilds the look; `RenderSpec.UvImage` carries it.
- **Sampling rule found** (probed with the marker at size 512, a texel 7 frame pixels wide): `SamplerDescriptions.PixelArt`, which the checker already uses, is nearest with no mips, so a frame pixel shows texel `floor(uv * size)` exactly. At 2 pixels from a texel edge the pixel is the pure colour, not a blend (`Sampling_is_nearest_so_a_texel_edge_is_a_step_not_a_blend`). Linear filtering was not chosen, since it makes a pixel a blend a check cannot name. Faces beyond the unit square repeat the image (the sampler repeats), as the checker does; the ground beyond the square stays the dim checker.
- **Readings landed.** Marker: inside the island the stored colours within 2, in the padding `(c + 1) / 2` within 2, the margin equal to the no-image frame. Grey 8-bit PNG: R = G = B = the stored grey inside (200, 60) and half of it outside (100, 30). Highlight is the same pixel with and without an image. The white pixels of the plain frame (labels, border and the edges' cores) are identical with the image under them; the antialiased edge and seam pixels blend with the different ground, so those are checked for the line's colour over the image's (seam yellow over blue and white, the top edge's white lifting the green or red channel), not for equality. The frame without an image after one with it is byte-equal to the plain frame.
- **Scenario-materials frame** `barrel-uvs-image` (1024, after every existing frame; `UvImageFrame.cs`, wired at the end of `MaterialsScenario.RunAsync`): the barrel's layout with `staves-base.png` under it. 48 stave samples (16 inside islands, 32 in the padding) at pixels a quarter to three quarters into their texel and 4 pixels clear of every face edge, each the stored texel inside an island (islands are the faces' polygons) and its half outside, tolerance 2; 84 gap samples 2 to 3 pixels either side of the edge line at u = k/8, the gap colour (28, 18, 11) or its half, all read exact in the run (so tolerance 2, worked from the image). The caption is checked.
- **Tests.** `UvImageArgumentTests` (unit, ToolHarness: another mode, missing file with the folder's PNGs, not a PNG, over 4096, problems collected) and `UvImageLookTests` (regression: marker, sampling, v-flip and u-mirror each fail, lines and highlight, grey). The marker test uses a quad whose UVs cover the middle of the unit square rather than a front-UV box, so that island, padding and margin all exist.
- **Existing frames.** Compared by sha256 against a baseline run before the change: scenario-crate, scenario-uvs and the 13 existing scenario-materials frames are all byte-identical (91 and 13 frames).
- **Not checked.** A PNG with alpha below 255 or a 16-bit or palette PNG (alpha is kept as stored in the dimmed copy; the engine's decode was not probed for those); an image over a mesh whose UVs lie beyond the unit square; an image on a sheet or any mode but `uvs` (refused).
- **Review**: round 1, approved by the person (barrel, marker, lines, grey frames). Lines are antialiased and semi-transparent, so only pure-white pixels are compared for equality; the lines are checked for their colour over the image. `PartToolsListTests`'s digest and `EditToolDefinitionTests`'s argument list gained `image`.

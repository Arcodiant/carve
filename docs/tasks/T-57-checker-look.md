---
description: >-
  render mode checker draws the model lit with a generated checker texture, and unmapped faces magenta
---
# T-57 — Checker look

**Status:** Open

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-54

**Verify:** server-unit, server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent sees its UVs on the model: stretch, direction, mirroring and unmapped faces all read at a glance, in every view `render` already has.

## Scope

- **First, a probe in the product** (Carve's own `RenderSystem`, headless): a texture made with `IGraphicsResourceManager.CreateTexture` and drawn with `Material.LitTextured` on `PositionUvNormalVertex` corners.
  - Planning's probe showed only that `IModelLoader` reads `TEXCOORD_0` and an embedded texture from a `.glb` and draws it headless.
  - If Carve's path fails, stop: it is an engine need, recorded as `CLAUDE.md` says.
- **The checker texture is this look's content** (`src/Carve.Server/CheckerTexture.cs`):
  - 256 x 256 RGBA8, sRGB, made in code: 8 x 8 cells of 32 px.
  - Column c has hue `#E04040`, `#E08A30`, `#D8C830`, `#50B050`, `#40B0C0`, `#4070E0`, `#6A50D0`, `#B050C0` (red at u = 0 to violet at u = 1).
  - Cell `(c, r)` is that hue when `c + r` is even, and the hue times 0.55 per channel, rounded, when odd.
  - Cell `(0, 0)`, at UV (0, 0), the top-left, is white `#FFFFFF`.
  - Sampled nearest (`PixelArt`), repeat, no mipmaps, so UVs outside 0..1 tile as the game would tile them.
- **`render` with `mode: "checker"`:**
  - Mapped faces are drawn lit with the checker at their corner UVs, with the same normals as `shaded` (flat or smooth, per T-42).
  - Unmapped faces are drawn lit in magenta `#FF00FF`.
  - A mesh with no UVs at all is all magenta, and the result says no face has UVs.
  - It works with every argument `render` has: `view`, `direction`, `zoom`, `target`, `projection`, `size`, `views` and `columns`, `labels`, `highlight` (as `shaded` draws it), `mesh`, `pose`, `clip` and `time`.
  - The caption names the mode, as `wireframe` does.
- **The description is content:** what the checker shows (stretched cells mean stretch; hue order is u, the white cell is the origin, so a reversed order or a misplaced white cell means a mirrored or turned island; magenta is unmapped), with a worked example.
- **Frames in `scenario-crate`** (`CrateLooks.cs` or a sibling file):
  - The crate gets UVs through `set_uvs`, worked out in the scenario: each face's corners laid planar along its dominant axis into the unit square. One face is left unmapped.
  - Frames: `front`, `back` and `+y`; a 2 x 2 `views` sheet; and `front` at sizes 256 and 1024.
  - Pixel checks are worked from the geometry and the UVs given (`CameraPixels` projection, then the UV at that point, then the expected cell): the white cell where predicted and absent from its mirror; the hue order across a face; magenta on the unmapped face.
  - Lighting changes value, so compare hue and order, not exact bytes.
  - Work every check against the geometry before writing it, and stop if one cannot pass.
- **Tests:** `regression`: the mode with each argument family, the all-magenta case, and that `shaded` frames are unchanged.

## Notes

- Opus: a new engine path, and the frames other tasks compare against.
- Runs side by side with T-55, T-56, T-59 and T-60. It owns `RenderTool.cs`, `RenderSystem.cs`, `MeshBridge.cs`, `LookMeshes.cs` and the crate scenario's look files in this wave. T-58 follows it in the same files.
- **Write down for the dependants:** how the texture is made and held (T-58 draws it again), the mode plumbing in `RenderTool`, and the hashes of the new frames.
- No sound, motion or feel: the person's look at the frames is the whole review.
- **Carried from earlier plans:**
  - the byte-identical guard on every existing frame (all suites listed in Verify), by hash before and after;
  - a positive pixel check on every new frame, and one a mirrored or flipped camera would fail;
  - each new check shown failing, as a table (include a texture uploaded with v flipped);
  - sample shading near face corners as well as middles;
  - `finish` read whole, artifacts by job id, report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/review/T-57/`.
- **Open rules: stop and ask.** Anything the milestone's *Decided at planning* does not settle, any loosened check, and any change to the checker's colours.
- Run `finish` from inside the worktree and leave the folder.

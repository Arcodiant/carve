---
description: >-
  An agent authors and unwraps UVs, carries them through file and export, and checks them by eye
---
# M-6 — UVs

**Status:** Planned

**One-liner:** *An agent authors and unwraps UVs, carries them through file and export, and checks them by eye*

## What this milestone delivers

An agent authors UVs by coordinates and by projection, marks seams and unwraps, carries UVs through the Carve file and `.glb` export, and checks them in a UV render mode and with a checker texture.

Content: the UV look, the checker texture and a sample unwrapped model.

## Open at planning

- Which projections and which unwrap method; written here or from a library.
- Does the UV mode draw the layout flat, the checker on the model, or both?
- How seams relate to the mesh's edges and to export vertex splitting.

**Decided at planning (2026-11-09).** Asked and answered by the person, each on the recommended option:

- **Order:** M-6 runs after M-18. It builds on T-42's per-corner normals, `mark_edges` and file version 3, and on T-43's `subdivide`. M-5's cuts plan their own UV carrying whenever M-5 runs.
- **UV axes:** glTF's. (0,0) is the texture's top-left, u runs right and v runs down, in the tools, the file, the layout image and the export, with no conversion anywhere.
- **Storage:** UVs per face corner. A face may have none (unmapped); edits keep UVs.
  - `move` keeps them.
  - `extrude`'s cap keeps its own, and the new walls are unmapped.
  - `subdivide` interpolates them linearly.
  - `undo` restores them.
- **Seams:** a marked edge set (vertex pairs), beside hard edges. `unwrap` cuts at seams and at open edges. Export splits a vertex wherever its corners' UV or normal differ. `get_mesh` reports UVs and seams.
- **Tools:**
  - `set_uvs` (by coordinates);
  - `project_uvs` (`planar`, `box`, `cylindrical`, into a rectangle);
  - seams marked with `mark_edges(..., seam)`: T-42's tool widened, rather than a separate `mark_seams` (asked again once T-42's tool came to light);
  - `unwrap`.
- **Unwrap:** LSCM written here, with a conjugate-gradient solver in double, then a shelf packer. Deterministic, with no dependency.
- **Looks:** both.
  - `render` `mode: "checker"` draws the model lit with a generated checker in every view.
  - `mode: "uvs"` draws one mesh's layout flat (sketch below).
- **Check:** warns of unmapped, flipped, zero-area and overlapping UV faces.
- **Sample:** a barrel (`samples/barrel.carve`, seamed and unwrapped) plus the existing crate box-projected, in a scenario of their own.
- **Acceptance:** a closing run by fresh agents with a planted fault (T-62).

**Checked at planning:**

- **Engine probe** (2026-11-09, files in `%TEMP%\m6-probe`): a `carve`-exported cube with `TEXCOORD_0` and an embedded 8 x 8 checker added by hand loaded through `IModelLoader` in `spikes/Carve.ModelViewer` and drew headless, crisp under a nearest sampler. A marker texel at image (0,0) landed where glTF's top-left origin puts it.
  - The engine docs give `IGraphicsResourceManager.CreateTexture` from raw pixels and `Material.LitTextured`, and Carve's render vertex is already `PositionUvNormalVertex`.
  - So no engine need is expected. T-57 confirms Carve's own path first, and records an engine need if it fails.
- **The barrel's geometry**, defined through `carve` (40 vertices, 34 faces, 72 edges, closed, no warnings, `check` ok, volume 0.4398 m³). Its definition is in T-53's Notes. With T-56's seams it unwraps to three islands: the side (32 faces) and the two caps.

**Set by the planner, open to the person's objection:**

- the checker's colours (T-57);
- the layout's colours and window rule (T-58);
- `box`'s 3 x 2 grid and `cylindrical`'s defaults (axis +Y, seam at -Z) (T-55);
- unwrap refusing an island that is not a disc, the margin of 1/64, and the stretch measure (T-56);
- the overlap tolerance of 1e-6 in UV area (T-60);
- the "unflipped" rule: corners counterclockwise on screen with v down, i.e. a negative shoelace sum (T-55, T-60).

**The agreed sketch** (the person chose it from these drawings):

```
mode "checker" (any view)      mode "uvs" (512 x 512)
                               (0,0) u ->            (1,0)
     ____________             +-----------------------+
    /R W/O B V /|             |.dimmed checker........|
   /__/__/__/_/ |             |  +------+   +------+  |
  |R |O |Y |G | |             |  |  0   |   |  1   |  |
  |__|__|__|__| |             |  |      |   |      |  |
  |O |Y |G |B | /             |  +------+===+------+  |
  |__|__|__|__|/              |   white edges         |
  lit, hue per u column       |   === seam (yellow)   |
  white cell = uv (0,0)       |   0,1 = face ids      |
  unmapped face: magenta      v                       |
                            (0,1)                 (1,1)
                              +-----------------------+
                            u right, v down (glTF);
                            frame = unit square, grown
                            to fit UVs outside it
```

The checker is 256 x 256, in 8 x 8 cells. Each column has a hue from red (u = 0) to violet (u = 1), light and dark alternate, and the cell at (0,0) is white. In the layout, mapped faces show the checker at full brightness over the dimmed ground.

**Order.**

1. T-53.
2. T-54.
3. T-55, T-56, T-57, T-59 and T-60 side by side.
4. T-58 (after T-57, in the same render files).
5. T-61.
6. T-62.

Opus on T-53, T-56, T-57 and T-58, each planned for two agents. Sonnet on the rest. Ten tasks, two over the usual eight. The unwrap and the two looks are each a full agent's work, and the scenario and acceptance run follow the pattern of M-2 to M-4.

**Before dispatch:**

- check one GPU suite through the queue, and that every runner's feed has the pinned engine;
- read every member's Scope against what M-18 (and anything else since 2026-11-09) has landed.

The looks add no sound, motion or feel, so the person's look at the frames of T-57, T-58 and T-61 is the whole review.

## Required tasks

- [ ] [T-53](../tasks/T-53-uvs-and-seams-in-the-kernel-and-the-file.md) — A mesh holds UVs per face corner and marked seams, carried through every edit, undo and the Carve file
- [ ] [T-54](../tasks/T-54-set-uvs-seams-on-mark-edges-and-get-mesh.md) — An agent sets UVs by coordinates with set_uvs and marks seams with mark_edges, and get_mesh reads both back
- [ ] [T-55](../tasks/T-55-projections.md) — project_uvs lays chosen faces' UVs by planar, box or cylindrical projection into a rectangle
- [ ] [T-56](../tasks/T-56-unwrap.md) — unwrap cuts at seams and open edges, flattens each island by LSCM and packs them, shown on the barrel sample
- [ ] [T-57](../tasks/T-57-checker-look.md) — render mode checker draws the model lit with a generated checker texture, and unmapped faces magenta
- [ ] [T-58](../tasks/T-58-uv-layout-look.md) — render mode uvs draws a mesh's UV layout flat over the dimmed checker, with seams, labels and highlight
- [ ] [T-59](../tasks/T-59-uvs-in-export.md) — export writes each corner's UV as TEXCOORD_0, splitting vertices where UVs differ, and warns of unmapped faces
- [ ] [T-60](../tasks/T-60-uv-faults-in-check.md) — check warns of unmapped, flipped, zero-area and overlapping UV faces
- [ ] [T-61](../tasks/T-61-barrel-scenario.md) — The barrel is seamed, unwrapped and seen in both UV looks, and the crate box-projected, saved, reopened and exported, in suite scenario-uvs
- [ ] [T-62](../tasks/T-62-uvs-acceptance-run.md) — Fresh agents unwrap a model from the tool descriptions alone and find and fix a planted unmapped extrusion

## Retrospective

---
description: >-
  swatch gains a cube view, with the same tiling option as the sphere, so a material can be judged on flat faces and edges as well as a curve
---
# T-157 — A cube view on swatch

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-156

**Verify:** patina-unit, scenario-patina

## Goal

`swatch` can draw the material on a lit cube as well as the sphere, with T-156's `tiling`, so an agent and the person
see flat faces, the edges where faces meet, and a texel density near a game's.

## Scope

- **The view:** `swatch` `view` `cube`, lit as the sphere is (carve's lights, through `Carve.Hosting`), each face UV'd
  0..1 so a tiling texture is continuous across a face; turned so three faces show; `angle` as the sphere's; `tiling`
  1 to 8 as T-156 gave the sphere.
- **The sheet:** whether the sheet gains a cube cell beside the sphere, or the cube is its own view only, is this task's
  call against the agreed sheet layout in the milestone's file. Ask the person if a sheet change is proposed. At the
  defaults, every existing sheet and sphere stays byte-identical, by hash against `main`'s, fetched by job id.
- **How:** the cube's mesh from the engine's geometry where it has one (read its docs, not its source), else built in
  `Patina.Server` beside `SwatchSphere.cs`. If the engine is missing something, record it as an engine need, as
  `CLAUDE.md` says.
- **Tests and scenario:** the description's worked example run as written; a scenario-patina check predicts the cube's
  faces from the geometry and the exported texels, as T-156 predicts the sphere, at tiling 1 and 3, with breaks (a face
  mirrored, tiling ignored on one face, a face's UVs turned) in the scenario, never in the product.
- **Review:** the floor tiles on the sphere and the cube at tiling 3, shown to the person.

## Notes

- **Decided (2026-11-20, by the person):** approving T-138's floor tiles, the person asked to see the swatch on a sphere
  and a cube, with 3 x 3 tiling. T-156 gave the sphere a tiling option; this task adds the cube, as a new M-28 member.
  T-142, which commits each sample's swatch sheet, waits on it.
- Sonnet: the scope is concrete.

### Hand-off

Written by Sonnet 5.5 (`claude-sonnet-5-5`) at its call checkpoint (about 60 calls). Code committed (`0b1597d`) and
`cairn task T-157 verify` passed (patina-unit, scenario-patina, job J-20261120-092511-767cc2).

**Done:**
- `swatch` `view: cube` (`src/Patina.Server/SwatchCube.cs`: 24 vertices, each face its own UVs 0..1, u right and v down as
  seen, flat normals, edge 0.7, turned 35 degrees about y plus `angle`, leaned 25 degrees toward the camera, so the front, left
  and top show). Built in Patina.Server: the docs of the engine's `PrimitiveMeshData.Cube` do not give its UVs. No engine need.
  `tiling` 1 to 8 as the sphere (`MeshInstance.AtlasRect`); a cube request shows the cube entity and hides the sphere, both
  wearing the one material. The sheet is unchanged (the cube is its own view only; no sheet-layout change to ask about).
- The sphere cell's label in the sheet names the tiling when it is not 1 (`sphere, Pbr, carve lights, angle 0, tiling 3`;
  at 1 byte-identical), as the orchestrator asked after T-140 found it. The cube view has no atlas label; its caption names it.
- Tests: label, cube geometry (UVs, normals, winding), refusals, worked example. Scenario (`SwatchCubeCheck.cs`): the cube
  predicted per face from geometry and exported texels (an asymmetric diagonal-times-stripes graph, since the sphere's stripe
  grid is mirror-symmetric) at tiling 1, 3 and angle 20, each visible face held to 0.5 % wrong of its own judged pixels; breaks
  in the scenario (front mirrored, left UVs turned, top tiling ignored, top mirrored in v, tiling 1 judged as 3) all caught.
- Frames: all 43 PNGs of J-20261120-070631-363baf equal by SHA-256 to this job's. The sphere-at-tiling frames postdate that
  baseline, so their identity is held by the scenario's own check (tiling 1 equals no argument; the sphere after a cube).

**Left:**
1. The review. `floor-tiles.texgraph` is not on M-28 yet (T-138). After T-138 lands, `git rebase origin/milestone/M-28`,
   rebuild `dotnet build src/Patina.Server -c Release`, then run `python3 C:/Users/andre/AppData/Local/Temp/t157-review.py
   <this worktree>/src/Patina.Server/bin/Release/net10.0/patina.exe <floor-tiles.texgraph> "G:/My Drive/artifacts/carve/M-28/T-157/round-1"`
   (a stdio MCP client: opens the graph, writes the sphere and cube at tiling 3 and 1). Show them with `mcp__waystone__review`.
   Do not land before the person answers.
2. `### As built` (this section replaced), `cairn lint`, `cairn task T-157 finish`; re-run verify after the rebase.

**Surprising:** the Bash tool consumed backslashes in generated C# (`
`, `	`) twice; compose them with `chr(92)`.

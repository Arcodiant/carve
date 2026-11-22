---
description: >-
  swatch gains a cube view, with the same tiling option as the sphere, so a material can be judged on flat faces and edges as well as a curve
---
# T-157 — A cube view on swatch

**Status:** Done

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

### As built

Built and verified on Sonnet 5.5 (`claude-sonnet-5-5`), over two sessions.

- `swatch` `view: cube` (`src/Patina.Server/SwatchCube.cs`): 24 vertices, each face its own UVs 0..1, flat normals, turned 35
  degrees about y plus `angle`, leaned 25 degrees toward the camera, so the front, left and top show. Built in Patina.Server
  (no engine need). `tiling` 1 to 8 as on the sphere; the cube and sphere wear the one material. The sheet is unchanged.
- The sphere cell's label names the tiling when it is not 1 (byte-identical at 1).
- Tests: label, cube geometry, refusals, worked example. Scenario (`SwatchCubeCheck.cs`) predicts each visible face from
  geometry and exported texels at tiling 1, 3 and angle 20, 0.5 % wrong per face; five seeded breaks are all caught.
- Frames: all 43 PNGs of the baseline job equal by SHA-256.
- Review: floor-tiles on the sphere and the cube at tiling 3 and 1 (`G:\My Drive\artifacts\carve\M-28\T-157\round-1`),
  approved by the person in round 1.
- Verify passed after rebasing onto T-138 (patina-unit, scenario-patina).

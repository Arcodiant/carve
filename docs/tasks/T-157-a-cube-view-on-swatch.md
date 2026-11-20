---
description: >-
  swatch gains a cube view, with the same tiling option as the sphere, so a material can be judged on flat faces and edges as well as a curve
---
# T-157 — A cube view on swatch

**Status:** Open

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

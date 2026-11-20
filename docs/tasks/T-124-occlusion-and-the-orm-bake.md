---
description: >-
  bake writes ray-cast ambient occlusion against the whole model with a ray count and reach, and an ORM PNG with occlusion, roughness and metallic for glTF
---
# T-124 — Occlusion and the ORM bake

**Status:** In Progress

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** T-123

**Verify:** core-unit, server-unit, scenario-bakes

## Goal

`bake` writes ambient occlusion and a glTF ORM texture, which completes the wrench's single material with base colour,
occlusion, roughness and metallic from bakes alone.

## Scope

- **Core, `src/Carve.Core/Baking/`:**
  - **A ray caster:** a BVH over the triangles of the occluding meshes at rest, from mesh positions and never a pose.
    It is deterministic and engine-free. It gives a closest hit within a distance, and both sides of a triangle block.
  - **Occlusion:**
    - rays leave each covered texel from its world position, lifted along the face's geometric normal by a small
      offset. Choose the offset so a texel never hits its own face, scale it with the model, and write the rule down;
    - each texel casts `rays` directions over the hemisphere about the geometric normal: a fixed, stratified,
      cosine-weighted set (for example a Fibonacci spiral). Each texel's set is turned about the normal by an angle
      from T-77's integer hash of the texel index, which trades banding for fine noise. No `System.Random`;
    - a hit at distance `t < reach` occludes by `1 − (t / reach)²`, so distant geometry only tints (fps-arms'
      `pipeline/ao.py`);
    - the value is 1 − the mean occlusion: 1 is open, 0 is closed;
    - the image is one channel, padded by T-120's `Pad`.
  - **ORM:** three channels. R is occlusion. G is roughness and B is metallic, from `surface` by the same resolution
    T-122 uses for `base_colour`: the later entry, then the face's material, then `Material`'s defaults (roughness 1,
    metallic 0). It is linear.
- **`bake`:**
  - new maps `occlusion` (`<mesh>.occlusion.png`, grey) and `orm` (`<mesh>.orm.png`, RGB);
  - a new block `occlusion: {rays, reach, occluders}`:
    - `rays` is 1 to 1024, default 32;
    - `reach` is in metres and required when `occlusion` or `orm` is asked for. It has no default;
    - `occluders` is a list of mesh names, every mesh of the model by default. An unknown name lists the meshes;
  - the result reports, per file, the time taken in milliseconds;
  - the description gains a paragraph, and the worked example uses `orm` as both `metallic_roughness_texture` and
    `occlusion_texture`. Test that `define_material` accepts one path in both slots.
- **Stage `OcclusionStage.cs`** in the Bakes scenario:
  - bake `occlusion` and `orm` at 1024 with rays 32 and reach 0.035 (fps-arms). The `surface` adds fps-arms'
    roughness and metallic: `rubber` 0.88/0, `steel` 0.36/1, `dark` 0.62/0, `red` 0.45/0, `black` 0.50/0;
  - redefine `wrench` with the base colour, the ORM in both slots and `occlusion` 1;
  - **frames:** the `uvs` look with the occlusion under it; the wrench from the six sides, the three-quarter view and
    the close frames of T-123; the jaw from inside its opening;
  - **checks**, each with a break:
    - in the PNG, texels in the jaw's inside corner are darker than texels in the middle of the handle's flat face by
      a probed margin. Break: reach 0.0001;
    - an open texel far from other geometry is near 1, by a probed bound;
    - the ORM's G and B equal each group's surface values exactly on covered texels. Break: `steel` metallic 0;
    - the render and `render_glb` comparison of T-123 holds with the full material.
  - Every number is probed on the wrench and brought to the person before landing.
- **Tests** (`unit`):
  - the BVH against a brute-force cast on a small mesh;
  - a texel at the floor of a closed box's inside reads 0;
  - a lone quad reads 1;
  - a quad beside a wall reads lower near the wall, and the value is monotonic in distance;
  - `occluders` narrowing changes the result;
  - two bakes byte-identical;
  - ORM channels exact;
  - each error.
- **Performance:** measure the wrench at 1024 with 32 rays. Report the time in the task's Notes, and stop and ask if it
  is over 10 s on this machine.

## Notes

- Decided at planning (2026-11-18), by the person: occlusion against the whole model at rest, narrowed by `occluders`;
  an ORM PNG with R occlusion, G roughness and B metallic; rays default 32 and reach required; fixed directions, so a
  bake is byte-identical from run to run.
- Chosen by the orchestrator, each a stop if it proves wrong: the `1 − (t / reach)²` falloff (fps-arms' own); the
  per-texel hash rotation; the cosine-weighted set; the 10 s budget.
- **Stop and ask** before landing: any threshold, a gamma or curve on occlusion (fps-arms applied `^1.2`, which is not
  adopted), or a falloff other than the above.
- Opus: the BVH, the sampling and the self-hit offset are design work, and the stage is a look stage, so plan for two
  agents.
- Runs beside T-125 and T-126. All three add a map and a block to `bake`, a paragraph to its description, a stage file
  and a line in `BakesStages.cs`. Keep both sides of any such conflict. This task owns the ray caster, which no sibling
  uses.
- The person looks at the occlusion frames before this lands (a Waystone review). Treat "Commented" as unanswered.
- **Verify line:** once scenario-bakes (T-123) is in `tests.json`, add it to this file's `**Verify:**` line, with the
  task's other suites.

### Hand-off

Agent 1, Claude Opus 5.5 (2026-11-19). Status: **QUESTION**. The numbers are below for the person to rule, and one check
fails. Nothing is ruled yet.

- **Done and committed.**
  - Core: `RayCaster.cs` (a median-split BVH; closest hit; both sides block) and `BakeOcclusion.cs`. `Bake.Occlusion`
    uses a Fibonacci-spiral cosine-weighted set, turned per texel by T-77's hash of `y × size + x`, with falloff
    `1 − (t / reach)²` and value 1 − mean, and no curve. `Bake.Orm`, `Bake.OcclusionOffset` and `Bake.OcclusionDirections`.
  - The offset rule is 1e-5 × max(box diagonal, largest absolute coordinate) over the occluders and the baked mesh. On the
    wrench that is about 2.5 µm. The wrench at 256 with 8 rays has 0 self-hits.
  - `bake` gains maps `occlusion` and `orm`, the `occlusion` block (`rays`, `reach`, `occluders`) and `ms` per file. The
    default maps stay `masks` and `base_colour`, since `reach` has no default. The description has a new paragraph and the
    example uses `orm` in both slots.
  - Tests: `OcclusionTests` (13, unit) and `BakeOcclusionToolTests` (17 cases, unit). T-122's example test now expects the ORM.
  - `OcclusionStage.cs` and its line in `BakesStages.cs`. The `CLAUDE.md` Bakes row has a clause for it, and
    `scenario-bakes` is on the Verify line.
- **The wrench has no jaw opening.** It is a ratchet: separate solids stacked, with no concave edge. Its one crevice is
  the steel square drive (faces 100 to 105) standing between the dark ring under the head (face 99) and the socket top
  (face 118). The stage uses that crevice as "the jaw's inside corner" (`socket-top-by-drive`: texels of face 118 within
  1.5 mm outside the drive's footprint). It replaces "the jaw from inside its opening" with `wrench-orm-drive-close`. The
  open texel is the middle of grip facet 5, within 2 mm of its centre. **The person to confirm or redirect.**
- **Probe** (job-less local runs 1 to 3 on this machine, Debug build; files in `G:\My Drive\artifacts\carve\M-24\T-124\probe`):

  | region (8-bit occlusion) | n | min | mean | max |
  |---|---|---|---|---|
  | socket-top-by-drive (the crevice) | 818 | 103 | 133.7 | 183 |
  | ring-under-by-drive | 818 | 100 | 130.9 | 187 |
  | head-top-by-ring | 2001 | 91 | 186.5 | 223 |
  | drive-sides-middle | 1950 | 188 | 202.1 | 218 |
  | grip-facet-5-middle (open) | 123 | 255 | 255 | 255 |
  | crevice margin (open min − crevice max) | | | 72 | |
  | under break `reach` (0.0001): crevice | 818 | 255 | 255 | 255 (margin 0) |

- **Timing**, wrench at 1024 with 32 rays, inside `carve` (Debug, as the suites build it): occlusion **8268 ms** (7354
  and 8517 in the break runs), orm 365 ms. This is under the 10 s budget, with little room.
- **Options to rule** (each names its check):

  | check | option A | option B | option C |
  |---|---|---|---|
  | crevice darker than the grip's middle (`CreviceMargin`) | ≥ 36 (half of 72) | ≥ 50 | ≥ 60 |
  | open texel near 1 (`OpenLeast`) | = 255 | ≥ 250 | ≥ 240 |
  | ORM G and B exact | as built: exact bytes (break `metal`: 271418 of 497040 texels differ) | | |

  Both constants are `null` in `OcclusionStage.cs` until ruled, so the stage only probes them now. Break `reach` drives
  the margin to 0, so any option above catches it.
- **Failing: render against `render_glb` with the full material.** Three-quarter: 215 px differ, largest patch 54. D-6's
  rule allows a patch of 50. Front: 315 px, patch 39, a pass. With reach 0.0001 (occlusion about 1) the comparison
  passes, and stage 1's comparison still passes (48 and 10 px). So the occlusion texture is what `render` and
  `render_glb` disagree on. This is not investigated yet. It could be how `render` builds the material's occlusion
  against the loader's, or filtering. Loosening the rule is the person's call, not ours.
- **Left:**
  - find the render/`render_glb` occlusion difference;
  - land the ruled numbers and show each break failing (breaks `reach` and `metal` are wired);
  - the Waystone frame review (11 frames, `wrench-orm-*`, plus the two `compare/good-wrench-orm-*` triptychs). No frame
    has been opened yet;
  - `### As built`, `cairn lint` and `finish`.
- **Not checked:** a Release-build timing, and part transforms. Occluders use each mesh's stored positions, as Scope
  says; a multi-part model's part offsets are not applied.

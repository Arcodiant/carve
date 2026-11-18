---
description: >-
  bake writes ray-cast ambient occlusion against the whole model with a ray count and reach, and an ORM PNG with occlusion, roughness and metallic for glTF
---
# T-124 — Occlusion and the ORM bake

**Status:** Open

**Pri:** P1

**Milestone:** M-24

**Batch:** — (standalone)

**Depends on:** T-123

**Verify:** core-unit, server-unit

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

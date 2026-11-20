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

**Verify:** core-unit, server-unit, server-regression, scenario-bakes, scenario-orcward, scenario-morph

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

### As built

Agent 1 Claude Opus 5.5 (BVH, occlusion, ORM, `bake`, stage, probe); research agent Claude Opus 5.5 (render against
`render_glb`); agent 3 Claude Opus 5.5 (the fix, rulings, unused-block warning, before hashes); agent 4 Claude Opus 5.5
(the corrected crevice check, after hashes, runs, review, landing). 2026-11-19.

- **Core.** `RayCaster.cs`: a median-split BVH over the occluders' triangles at rest, closest hit within a distance, both
  sides block. `BakeOcclusion.cs`: a Fibonacci-spiral cosine-weighted set, turned per texel by T-77's hash of
  `y × size + x`; falloff `1 − (t / reach)²`; value 1 − mean; no curve; padded by `Pad`. The self-hit offset is
  1e-5 × max(box diagonal, largest absolute coordinate) over the occluders and the baked mesh (about 2.5 µm on the
  wrench; 0 self-hits at 256 × 8 rays). `Bake.Orm`: R occlusion, G roughness and B metallic by T-122's resolution, linear.
- **`bake`.** Maps `occlusion` and `orm`; block `occlusion: {rays, reach, occluders}` (rays 1 to 1024, default 32; reach
  required; occluders default every mesh); `ms` per file; the description paragraph and the example with `orm` in both
  slots. An unused `occlusion` block warns through the shared `UnusedBlock` helper (T-125 adds `bevel` and `curvature`
  to it). Default maps stay `masks` and `base_colour`, since `reach` has no default.
- **The person's rulings (2026-11-20).**
  - Crevice, **as corrected by the person**: every crevice texel reads at least 50 below every open texel, the open
    texels' least minus the crevice texels' most (`CreviceMargin = 50`; measured 255 − 183 = 72). This replaced the
    mean-to-mean comparison first coded. Break: reach 0.0001.
  - Open texels far from other geometry read at least 250 (`OpenLeast`; measured 255).
  - The wrench is a ratchet with no jaw opening: the drive crevice (socket top within 1.5 mm of the square drive's
    footprint) stands in for the jaw's inside corner, and `wrench-orm-drive-close` for the inside-the-opening frame.
  - `render_glb` loads with `GenerateMipmaps = true`, landed here; the untextured `render_glb` frames had to stay
    byte-identical.
  - An unused `occlusion` block warns.
  - ORM G and B exact, as built.
- **The fix.** `RenderSystem.cs` loaded the `.glb` with default `ModelLoadOptions`, so `render_glb` point-sampled its
  textures with no mips while `render` used mips and Linear (T-78 ruling 1); the occlusion's per-texel noise exposed it.
  It now loads with `GenerateMipmaps = true`, citing engine need 8. `render_glb`'s description says so
  (`RenderGlbTexts.Sampling`, taken out of `PartToolsListTests`' digest as T-94's sentences are). `RenderGlbSamplingTests`
  (regression) draws a 1024 texel-noise face with both tools under D-6's rule: without the flag 184846 px differ (limit
  1310.72); with it the two frames are byte-identical.
- **Before and after hashes** (`G:\My Drive\artifacts\carve\M-24\T-124\mipmap-fix`, `before.sha256`, `after.sha256`,
  `compare.py`): scenario-orcward, scenario-morph and server-regression run `--fresh` without and with the fix (all
  PASS). Of 287 PNGs, 286 byte-identical; the one change is `render-glb-sampling-tests/noise-render-glb.png`, which now
  equals `noise-render.png`. No untextured `render_glb` frame changed.
- **Comparison numbers** (scenario-bakes run 5, after the fix), differing px / largest patch:

  | comparison | before the fix | after |
  |---|---|---|
  | `wrench-front` (T-123 stage 1) | 48 / 15 | 0 / 0 |
  | `wrench-three-quarter` (T-123 stage 1) | 10 / 3 | 1 / 1 |
  | `wrench-orm-front` | 315 / 39 | 0 / 0 |
  | `wrench-orm-three-quarter` | 215 / 54 (failed) | 1 / 1 |
  | control `three-quarter-glb-against-front` | 90204 (fails) | 90202 (fails) |
  | control `orm-three-quarter-glb-against-front` | 78162 (fails) | 78198 / 76667 (fails) |

- **Probe** (8-bit occlusion, unchanged by the fix): crevice n 818, 103 / 133.7 / 183; open (grip facet 5's middle) n
  123, all 255; margin 72.
- **Breaks:** `reach` fails "the crevice's most (255) is only 0 below the grip's middle's least (255), under 50";
  `metal` fails "271418 of 497040 covered texels differ from the surface's roughness and metallic".
- **Timing**, wrench at 1024 × 32 rays, Debug, this machine: occlusion 8788 ms (8268 in the probe), orm 605 ms. Under
  10 s, with little room. A Release timing was not taken.
- **The description's example bakes at 512**, not 1024: with `orm` it cast 32 rays per texel at 1024 and took 13 s
  alone in `BakeToolTests`, and under `finish`'s load (server-regression on the same machine) it ran past the harness's
  30 s answer window (server-unit failed once on a TaskCanceledException). At 512 it takes 3 s. No check changed; the
  test's size assertion follows the example.
- **Review:** Waystone round 1 (17 items: the occlusion `uvs` frame, the `wrench-orm-*` frames, the two compare
  triptychs and the sampling test's two frames, `G:\My Drive\artifacts\carve\M-24\T-124
ound-1`): **approved**, no notes.
- **Not checked:** part transforms; occluders use each mesh's stored positions, so a multi-part model's part offsets are
  not applied. T-125's normal-map comparison moves with the fix; it was not run here.

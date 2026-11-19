---
description: >-
  scenario-parts builds the rover through the tools at two levels, turns its mast, edits an instance and shows each in frames worked from the geometry
---
# T-96 — The rover through the tools

**Status:** In Progress

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-92, T-93, T-94, T-95

**Verify:** core-unit, scenario-parts

**Perf:** skip: Carve declares no heavy suites

## Goal

The agreed rover is built through the product's own tools, blockout first and then refined, and is seen in the frames agreed at planning. Each frame has pixel checks worked from the geometry.

## Scope

- **`scenarios/Carve.Scenario.Parts`**, a new scenario. It links `RoverSpec` from `tests/Carve.Core.Tests` (T-91), so no number is typed twice, and builds the rover through MCP calls only:
  - `define_part` for the tree, the empties and the instances;
  - `define_mesh` for the level 0 boxes and the 8-sided wheel, then `set_colour`, then `keep_level {level: 0}`;
  - the refinement: the wheel's 16-sided `define_mesh` and its hub boss with `extrude`, and the cab's windscreen and lamp's lens with `extrude` (inset, then recess), each coloured;
  - `keep_level {level: 1, parts: [Cab, Lamp, Wheel_FL]}`.
- **Stages and frames (512 px unless noted), named as agreed:**
  1. `blockout-front`: level 0, view front;
  2. `blockout-sheet`: level 0, views +x, +z, +y;
  3. `front`: live;
  4. `back-left`: direction [−1, 0.6, −1];
  5. `top`: +y, orthographic;
  6. `mast-turned`: after `define_part {part: Mast, rotation: [0, 0.7071068, 0, 0.7071068]}`, front, with the lamp now at −Z;
  7. `part-axle-front`: `part: Axle_F`, direction [1, 0.3, 0], with the boss outward on both wheels;
  8. `part-wheel-labels`: `part: Wheel_FL`, `labels: faces`, 1024;
  9. `part-wheel-L0`: `part: Wheel_FL`, level 0;
  10. `reopened-front`: after `save` and `open`, byte-equal to frame 3;
  11. `instance-edit`: after one `move` on `Wheel_FL`'s hub boss, all four wheels changed, then `undo`.
- **`check` and `get_parts` stages:** the rover is silent. `Axle_F` lowered 0.3 m gives exactly T-91's envelope warning, and the scenario puts it back. An edit aimed at `Wheel_FR` is refused with the instance text.
- **Every frame's checks** are worked from `get_mesh` and `get_parts`, and from `RoverGeometry`'s counts and bounds, with the shared rules T-94 names (`ColourRule`, the Cuts ray test on the world-placed geometry, `SeenNarrow`, `Drawn`, in-sight). Each frame has one check that a mirrored or wrong-up camera fails, worked from the framing's bounds. Write a `CARVE_PARTS_BREAK` switch (`mirror`, `no-instance-rotation`, `levels-ignored`, `part-not-isolated`) that makes the scenario wrong on purpose, and show it failing on each, as the materials scenario does.
- **Suite `scenario-parts`** (`regression`) in `tests.json`, the project in `Carve.slnx`, and a Layout row in `CLAUDE.md`. Once the suite exists, add it to the `**Verify:**` lines of T-96, T-97 and T-98 (left out at planning, since `cairn lint` refuses a suite `tests.json` does not have yet).

## Notes

- **Frames fill an agent's context fastest:** Sonnet for the build and checks, with the frame review as a separate agent. That agent opens every frame itself, cropped or scaled, before it asks the person (M-7 lesson). Waystone's "Commented" is not an approval until the person says approved.
- **Bound by the agreed sketch** in the milestone file. If a frame cannot show what its line says (for example a boss hidden at the agreed direction), stop and ask; do not move the camera or the model.
- **Open rules: stop and ask.** A new shared check rule; any loosened threshold; any frame that differs from the sketch.
- **Write down for dependents (As built):** the run folder layout and the save path, for T-97.
- **Carried:** every existing frame byte-identical by hash; each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names, outside `artifacts/`; `finish` run inside the worktree, its output read whole and its `Merge task/T-96` line given; report a `verify` that waits more than a few minutes.


### As built

First agent (Sonnet 5.5), 2026-11-19: the scenario and its checks; the frame review and `finish` are still to come.

- **Project** `scenarios/Carve.Scenario.Parts` (in `Carve.slnx`; suite `scenario-parts`, `regression`, gpu, in `tests.json`; Layout row in `CLAUDE.md`; `scenario-parts` added to the `**Verify:**` lines of T-96, T-97 and T-98). It links `RoverSpec.cs` from `tests/Carve.Core.Tests` (typed once), the shed's `ShedChecks.cs` and `ShedGeometry.cs` (camera), the Cuts ray test `CutsGeometry.cs` and `ColourRule.cs`. The scenario cannot reference Core, so `PartsWorld.cs` places the geometry itself: `get_mesh` for each geometry part (live, and levels 0 and 1) placed by the tree in `RoverSpec.Tree` (the mast's rotation tracked by the scenario), then judged by the T-94 rules.
- **Build through the tools** (`PartsScenario.cs`): `define_part` for the non-instances, `define_mesh` for the blockout boxes and 8-sided wheel, `define_part` for the instances, `set_colour`, `keep_level {level: 0}`; then the 16-sided `define_mesh`, the six `extrude`s of `RoverSpec.Cuts`, `set_colour`, `keep_level {level: 1, parts: [Cab, Lamp, Wheel_FL]}`.
- **Order of the run, and the frames** (names as agreed; the run order differs from the numbers): `blockout-front` (`{level:0}`), `blockout-sheet` (`{level:0, views:[+x,+z,+y]}`, three 512 tiles, 1540x512), then the refinement, `front`, `back-left`, `top` (+y orthographic), `part-axle-front`, `part-wheel-plain` (an extra frame: the plain wheel at 1024 that the labelled one is compared with), `part-wheel-labels` (1024), `part-wheel-L0`, then `save` to `<run folder>/parts/rover.carve` and `open`, `reopened-front` (byte-equal to `front`, asserted), then `instance-edit` (one `move` on `Wheel_FL`'s face 1 by [0, 0.1, 0], seen from above, `+y` orthographic; see Questions), `instance-undone` (after `undo`, byte-equal to `top`, asserted), then `mast-turned` (`define_part Mast` with `RoverSpec.MastTurned`, front), then the envelope stage. `blockout-front`, `front` and `part-axle-front` are byte-identical with T-94's reviewed `rover-front-level-0`, `rover-front-live` and `axle-f` (same sha256).
- **Pixel checks per frame:** a caption check (terms and `N vertices, M faces`, the counts from the placed geometry, and at level 0 from `RoverSpec.Blockout`'s shapes); every fourth pixel each way that the ray test is sure of is the background or its face's `ColourRule` shade (tolerance 2; 4 for `RoverSpec.DarkGrey`, the person's B-8 ruling, cited at the site); a least count of sure pixels; and a mirror and up check (below). Axle_F's frame also checks both wheels' inner caps (3 000 sure pixels each at least, no boss on them), as T-94 ruled. The labelled wheel differs from the plain one by at least 800 pixels and wrongs at least one grid pixel. The sheet's tiles are judged one by one (the +y tile with -Z up), the caption corner (x < 48, y < 32) left out.
- **Mirror and up check** (`PartsJudge.TellsMirrorAndUp`): of the grid pixels the frame shows right, count those whose pixel turned left for right (then top for bottom) is a sure other shade (more than 6 away). At least one turn must tell 500 pixels (200 in a sheet tile). The other must too, unless it is a symmetry of the drawn rover, which tells under 100. The rover is symmetric in z, so its +x view cannot tell a mirror and its +y views cannot tell an upside-down camera (measured: +x tile mirrored 0 / up 4 133; top mirrored 3 429 / up 0; `instance-edit` mirrored 3 429 / up 16; the front and back-left views 1 200 to 1 900 both ways). **This is a rule the spec does not settle; see Questions.** Subtree frames (Axle_F, the wheel) have none, by the person's T-94 ruling.
- **`get_parts`, `check`:** `get_parts` is judged against the spec field for field after the blockout and after the refinement (name order, parent, instance_of, moves, translation, rotation, envelope, geometry, levels `[0]` or `[0,1]`, vertex and face counts, world bounds placed by the scenario). `check` on each geometry part is silent. `Axle_F` lowered by `RoverSpec.AxleLoweredTranslation` gives exactly the one warning (`'Axle_F': its envelope, placed in 'Chassis', goes below the envelope of 'Chassis' on y by 0.3 m`) in `check` of `Chassis` and `Wheel_FL` and in `get_parts`, and nothing else; put back, silent again. `move` aimed at `Wheel_FR` is a tool error naming `Wheel_FR`, `instance` and `Wheel_FL`. The mast-turned lamp: its placed z is at most 0.001, and `get_parts`'s world bounds equal the placed geometry's.
- **Instance edit:** each vertex of `Wheel_FL` after the move is its old position, plus [0, 0.1, 0] on the cap's loop; the four wheels each differ from the unedited rover in at least 20 pixels (measured 340, 520, 340, 520, counted pixel by pixel in each wheel's screen box, since the boss is a few pixels wide from above).
- **`CARVE_PARTS_BREAK`** (`PartsBreak` in `PartsWorld.cs`): `mirror` (every frame read left for right), `no-instance-rotation` (the instances defined unturned), `levels-ignored` (the expected draw ignores `level`), `part-not-isolated` (the expected draw ignores `part`). Each makes the scenario fail; table in `G:\My Drive\artifacts\carve\M-9\T-96\break-table.tsv`: mirror 17 checks, no-instance-rotation 16, levels-ignored 2 (the `part-wheel-L0` caption and pixels; the blockout frames pass it, since live equals level 0 until the refinement), part-not-isolated 10.
- **Shared file touched:** `scenarios/Carve.Scenario.Cuts/CutsGeometry.cs`, `Solid.FirstHit` works each face's normal once, not once a ray. Same results (no frame of any scenario changes); without it one whole-rover frame took 40 s to judge and the run timed out. The scenario's run is about 170 s locally (112 s queued on Mantorok); `tests.json` passes `-TimeoutSeconds 900` to `run-scenario.ps1`.
- **For T-97 (run folder and save path):** `artifacts/scenarios/parts/<n>/` (or `<name>/` under `CAIRN_ARTIFACTS` queued) holds `frames/`, `user-data/`, `run.json` and `parts/rover.carve`, the model after the refinement and its two levels (saved before the instance edit and the mast turn, so `Mast` is at its start rotation and the hub boss unmoved). No export is made here.

### Questions for the person

1. **The mirror and up rule on symmetric frames** (above). The rover is symmetric in z, so three frames cannot tell one of the two turns: the +x sheet tile (left for right), and `top` and `instance-edit` (top for bottom; also the sheet's +y tile). The check there is that the other turn tells over 500 pixels and this one tells under 100 (a true symmetry). Recommended: accept as built. The alternatives are to drop the check from those frames, or to give the rover an asymmetric part, which is not this member's to do.
2. **`instance-edit` is drawn from above.** The sketch gives no direction. From the front, the bosses of `Wheel_FR` and `Wheel_RR` face -Z and the chassis hides them, and no direction sees all four bosses; from above every wheel's moved boss shows, a few pixels wide. Recommended: from above, as built. The alternative is two frames.
3. **Extra frames** `part-wheel-plain` and `instance-undone` are not on the agreed list; both are helpers for comparisons (the labelled wheel against the plain one, the undo against `top`). Say if they should go.

### Hand-off

First agent (Sonnet 5.5), 2026-11-19, at the checkpoint (code committed, `verify` passing; the frame review and `finish` still to come).

- **Done:** the scenario, its checks, the suite, the break table, `CLAUDE.md`'s row and the Verify lines. `cairn task T-96 verify` runs `core-unit`, `server-unit`, `patina-unit` and `scenario-parts` (112 s on Mantorok) clean once committed.
- **Left:** (1) the frame review: a separate agent opens each frame below, cropped or scaled, against what it should show. (2) The person's answers to the three questions above. (3) A frame guard: `CutsGeometry.cs` changed (performance only), so the scenarios that link it (`scenario-cuts`, `scenario-gatehouse`, and any other the guard finds by `tests.json`) should be compared by hash with a fresh before-run, as T-94 did. (4) `finish`, run inside the worktree, its output read whole and its `Merge task/T-96` line given.
- **Frames**, in `G:\My Drive\artifacts\carve\M-9\T-96\round-1\` (copied from the local passing run, `artifacts/scenarios/parts/6`; hashes of the local copies):

| Frame | sha256 | Should show |
|---|---|---|
| `blockout-front.png` | `bd11f6dd7ad58eace448b54b30b3ba425f5e5b42f5f9a26480751182ef48c9d0` | T-94's reviewed level 0 frame: plain boxes, four 8-sided dark wheels with no boss, no recess, no lens |
| `blockout-sheet.png` | `ae06a00c16c069c919392a7970b9f7df8668c5a4bfd0754a66f4fe307869e2ea` | three tiles 1540x512, level 0, from +x, +z and +y (+y with -Z up the tile), each framed on the whole blockout |
| `front.png` | `3ab4aee283930739139ca9913c613d858752eb12d18c3f44e6fa1c780b9c0910` | T-94's reviewed live frame: sand cab at +X (right), mast and lamp at -X (left) with the yellow recessed lens on the lamp's +X end, 16-sided wheels, light-grey bosses on the +Z side; same as `reopened-front.png` |
| `back-left.png` | `16ce759b5eb62dd8bcfb452de2a3b1080baa54ea197805c5f81b2797e3d0a9c1` | from [-1, 0.6, -1]: the -Z side and the rear; the windscreen (+X) not in sight; the bosses of the wheels on the -Z side outward |
| `top.png` | `13ee2ec3c5288cde6f4f9af0d0aa7e393355f9142b128095a873f98832bf84fc` | +y orthographic, -Z up the frame: chassis, cab at +X (right), mast and lamp at -X, four wheels projecting at the sides; same as `instance-undone.png` |
| `mast-turned.png` | `3ec907c378b362039466561883762fe77cbd97caf5e7ddbaa02a58326774c6ba` | front; the lamp now lies along -Z (away from +X), the rest as `front.png` |
| `part-axle-front.png` | `8d9e152f9c8e0847b26d46b3d3f887f1c103f555efce8ba7c13a15264ed61d23` | T-94's reviewed axle frame: the two front wheels alone, plain inner caps, no boss in sight |
| `part-wheel-labels.png` | `2a1520ab95b6862c72bb77f3e491ff9e4ead34bef6a2223c4e1b42736d0d7e67` | 1024, Wheel_FL alone with its face ids 0 to 49 on the visible faces (T-94's labelled frame, larger) |
| `part-wheel-plain.png` | `fea0bd84c2607bed1add30eb738ad98e94b7b34b07c145350bdf5a57c844b0d2` | the same wheel without labels (helper) |
| `part-wheel-L0.png` | `1b75253d7201277b1136e3bf5e183b11987b76e1a704c39900c523e734bf7763` | Wheel_FL at level 0: an 8-sided prism, dark, no boss |
| `reopened-front.png` | `3ab4aee283930739139ca9913c613d858752eb12d18c3f44e6fa1c780b9c0910` | byte-equal to `front.png` |
| `instance-edit.png` | `14805245f1103e6b04b412eb03f6239c43bcc8b42f73b8084f89422c78ad0500` | from above: as `top.png`, but each of the four wheels' light-grey bosses shows its cap raised 0.1 m (the walls slanted) |
| `instance-undone.png` | `13ee2ec3c5288cde6f4f9af0d0aa7e393355f9142b128095a873f98832bf84fc` | byte-equal to `top.png` |

- **Checked:** every pixel check passes on the real product; each of the four breaks fails (table above); `reopened-front` equals `front` and `instance-undone` equals `top` by bytes. **Not checked:** by eye, no frame (this agent opened no image); the labelled wheel's ids by reading; the export and the split load (T-95, T-97).
- **Surprising:** `blockout-front`, `front` and `part-axle-front` match T-94's reviewed hashes exactly, so they need only a glance. The run is slow to judge (about 170 s locally, 112 s queued) because the Cuts ray test did a face normal per ray; now cached.

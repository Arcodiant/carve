---
description: >-
  The shed scenario finds every planted fault with check and select, moves a selection and shows the shed in every look, in its own suite
---
# T-27 — Leaky shed scenario

**Status:** Done

**Pri:** P1

**Milestone:** M-4

**Batch:** — (standalone)

**Depends on:** T-22, T-23, T-26

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

The milestone's content is seen end to end through the real tools: the leaky shed is opened, every fault is found by `check` and `select`, a selection drives an edit, and the shed is shown in every look, size and a sheet, in a suite of its own.

## Scope

**`scenarios/Carve.Scenario.Shed`** (new, laid out as `CLAUDE.md`'s *Writing a scenario* says). The project links `samples/leaky-shed.carve` as content copied to its output, and the scenario opens it by that absolute path; if a scenario cannot learn its own output folder, widen the debug extension rather than reach into the product.

1. **Open.** `open <path>/leaky-shed.carve`: the report has T-23's counts, `closed` false and T-23's warnings exactly. Capture `shed-front` (view front, shaded): the sketch in M-4's file.
2. **Check.** `check`: `ok` false; the problems, warnings, notes and stats equal T-23's *As built* table exactly (element and message).
3. **Select.** Each result compared exactly with T-23's ids:
   - `vertices where unattached` → the stray vertex;
   - `edges where open` → the missing roof slope's edges and the unmerged seam's;
   - `faces where normal.y > 0.5` → the remaining roof slope;
   - `faces where min.y >= 3.6` → the chimney's six faces;
   - `faces where y > 10` → `count` 0, no error (`centre` is not a property; a face's centre is `y`; person, 2026-11-07);
   - `faces where y >> 10` → a tool error naming character 3 and the operators (person, 2026-11-07).
4. **A selection feeds an edit.** `move` the chimney selection `by [0, 0.5, 0]`: `moved` has 8 vertex ids, `check` after still reports two shells and the same problems, and the chimney's `min.y` has risen by 0.5 in `get_mesh`.
5. **Looks**, each frame with a positive pixel check and one a wrong camera would fail (the shed is asymmetric: the door is off centre on the +Z wall and the chimney is off centre on the roof):
   - `shed-front-labels-faces`: view front, labels faces, 512: the +Z wall's label box present (darker than the wall, white inside), the -X wall (flipped, so facing the camera from inside) is labelled too, its box over the +Z wall, since a label ignores what hides its face (person, 2026-11-07);
   - `shed-front-wireframe`: view front, wireframe: the fill at a wall centre, an edge pixel on the ridge, the missing roof slope shows the far wall's edges through the gap;
   - `shed-front-normals`: view front, normals: from `front` the visible faces are asserted, each by the sRGB of ((n + 1) / 2) within 2: the +Z wall (0.5, 0.5, 1), the +X wall, and the flipped -X wall (1, 0.5, 0.5) through the roof gap. The remaining roof slope faces away from `front` and is culled, so it is checked by what is seen behind it (person, 2026-11-07). No `back` normals frame;
   - `shed-open-edges`: view front, shaded, `highlight` with the open-edge selection from step 3: an orange pixel on an eave edge, a grey pixel on the lid of a closed wall;
   - `shed-axes-sheet`: the six axis views, orthographic, 256, labels none: 772 x 514, gutters, the `+z` tile shows wall shade at the door's position (the door is coplanar with the wall, so it has the wall's shade), the chimney above the ridge on the left and the background gap between y 2.5 and 3.5 (person, 2026-11-07), the `-x` tile shows the flipped wall as missing;
   - `shed-1024-labels-both`: view `[1.6, 1.2, 2.2]`... use `front` at 1024, labels both: the stray vertex at (6, 0, 0) is labelled (it is a vertex on no face, so it is **not** labelled under the front-facing rule; check that it is absent and say so in the description if the rule surprises);
   - `shed-front-after-move`: shaded, after step 4: the chimney's pixel has moved up (a pixel that was chimney is now sky, one above it is now chimney).
6. **Error path**: `move` with `edges: [[3, 9]]` (not an edge) is a tool error naming the pair; the next `shed-front` capture is byte-identical to the one after step 4.

**The suite `scenario-shed`** (`regression`, `gpu`, `vulkan`) in `tests.json`; `Carve.slnx` lists the project; `CLAUDE.md`'s *Layout* gains the `Shed` row.

Pick sample pixels from the first good run, record them as constants with a comment saying why each was chosen, and **show each check failing** on a broken product, as a table. Breaks to use: `check` with the volume sign flipped; `select` with `>=` read as `>`; the wireframe scale at 1.0; labels drawn for back faces; the sheet's tiles in reverse order; `open` dropping the last face.

**Second run**: run the suite twice and compare every frame byte for byte. Record it.

**Show the person** every frame (`mcp__waystone__review`, kind `approve`), the sheet first. Their approval is the gate.

## Notes

- **Ruled by the person, 2026-11-07, on what T-23 found.** The shed is 4 x 3.9 x 3 m, not 3.5: the chimney sits at y 3.6 to 3.9, clear of the 3.5 ridge, so `faces where min.y >= 3.6` stands. `faces where normal.y > 0.5` returns faces 11 and 14, not the slope alone: the inside-out chimney's underside points up, and that is a true finding, so assert both and say why. The door's four edges carry non-manifold and winding warnings as well as `duplicate-face`, so the winding edges are the flipped wall's four and the door's four; T-23's `### As built` names them.
- **Ruled by the person, 2026-11-07, on the flipped -X wall.** From `front` it is drawn, not culled: its reversed loop faces the camera from inside, so it shows through the roof gap in the colour for n = +X, which shows it faces the wrong way, and it is labelled. From outside, in the `-x` tile of `shed-axes-sheet`, it is culled and missing.
- **Ruled by the person, 2026-11-07, on three more.** (1) The remaining -Z roof slope faces away from `front` (the eye is on the +Z side) and is culled there, so `shed-front-normals` asserts the +Z wall, the +X wall and the flipped -X wall, and the slope by what is seen behind it. (2) The select steps use `y`, not `centre.y`: `faces where y > 10` and `faces where y >> 10` (error at character 3). (3) The door is coplanar with the +Z wall and has its shade, so the `+z` tile asserts wall shade at the door, the chimney on the left and the background gap above the eave. Face labels ignore what hides a face, so face 2's label is drawn at its projected centre over the +Z wall.
- **Decided by the person at planning (2026-11-05):** the leaky shed is the sample; its fault list (T-23); the looks (T-25), labels and sheet (T-26) it is shown in.
- **Decided at planning on the planner's recommendation:** the step list and the frames above; the shed is opened from the committed sample through `open`, so M-3's `open` is exercised on a file an agent will meet.
- **Open rules: stop and ask.** Any change to the shed, a tool's behaviour or a look to make a step pass. Any tolerance looser than 10/255 (normals colours ±2). Any golden or cross-machine comparison. A planted fault not found, or found twice: report, do not patch the shed.
- **A fixer may not change** the shed, the views, the light, the framing or the look colours.
- This task is the run and its frames, split from the code on purpose. A defect found in a tool is a bug filed under M-4, not a fix here, unless it is a one-line fix in this task's own files.
- Sonnet is enough: every id, colour and caption it asserts is written down by T-22, T-23, T-25 and T-26.
- Its frames fill context. Look at them together at the end.
- Nothing has sound or motion, so there is no listening or real-time pass.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Sonnet 5.5 (`claude-sonnet-5-5`). No product code changed: `scenarios/Carve.Scenario.Shed` (`ShedScenario.cs` the steps, `ShedLooks.cs` the frames, `ShedFacts.cs` T-23's report verbatim, `ShedChecks.cs`, `ShedGeometry.cs` the camera and a ray test), the project in `Carve.slnx`, suite `scenario-shed` in `tests.json` (the sample is one of its inputs), the `Shed` row in `CLAUDE.md`'s *Layout*. The scenario finds the sample beside its own assembly (`leaky-shed.carve`, linked as content), so the debug extension needed no change.

**How the pixels are chosen.** Not by hand: `Camera` reproduces `render`'s framing and `Shape.FirstHit` casts the ray through a pixel at the mesh read back from `get_mesh`, keeping only faces that face the ray. A sample (each face's corners at 0.3 and 0.6 of the way to its centre, and its centre) is asserted only where that pixel and eight neighbours two to three pixels away all meet the same face or all meet nothing; in normals the spikes' pixels are left out and in the wireframe the edges'. So every face is sampled near corners and middles, and a mirrored or flipped camera, or a face drawn the wrong way round, puts the wrong colour at the wrong place. The first good run showed all of it passing except three sample choices (a wireframe edge on the small chimney, a spike on the culled slope, and `open`'s warnings, which are the same 22 as `check`'s), fixed by leaving those pixels out and by the expected list, not by loosening a tolerance (flat colours 2, lines 10, as before).

**The person's rulings of 2026-11-07, as built.** The flipped -X wall (face 2, normal +X, asserted) is drawn through the roof gap in the normals frame, in (1, 0.5, 0.5); its label is drawn over the +Z wall, in its box with the backing under it; the `-x` tile has it culled (background at its centre and both ends) under the drawn -X gable. The remaining slope (face 11) is culled from `front`: normals asserts that what is behind its samples is the background. `faces where y > 10` is count 0, and `y >> 10` is the error at character 3. The `+z` tile: wall shade at the door, the chimney at x 1.2 and background where a mirrored chimney would stand (x 2.8), and background in the gap at y 3.0. `faces where normal.y > 0.5` is 11 and 14.

**Frames of the clean run** (run 13 of `artifacts/scenarios/shed/`, kept in `artifacts/review/T-27/round-1/`; the person approved all, the sheet first).

| Frame | md5 |
|---|---|
| shed-front | `5c4adfacd00bbf73b6f4c5011e945cea` |
| shed-front-labels-faces | `eeee4578a3b2871f709471a2e0c3657c` |
| shed-front-wireframe | `52bd7170048da7b1a0cfb486c479b546` |
| shed-front-normals | `deadf10b3bd7327d7b1bef90d017242a` |
| shed-open-edges | `8f7b44392fe183b8f22c9a3c57583207` |
| shed-axes-sheet | `e2f9209925a6a598ef829a3800755be8` |
| shed-front-1024 | `a87188bb66fca6a6f1913c0ee7772e66` |
| shed-1024-labels-both | `774224f00daebca471d7760fedf2b6da` |
| shed-front-after-move, shed-front-after-error | `9a02b6a700d44b8ad5d83d639306655d` |

**Second run.** Runs 13 and 14 of the suite's script: all 10 frames equal by hash (`diff` of the two md5 lists empty). The queued `verify` run's frames (`cairn test artifacts scenario-shed`, a job of this member's own) are equal to them by hash.

**Byte-identical guard.** No product code changed. The cube (20) and crate (33 in this count, 53 frames in all) scenarios were run in this worktree and their frames compared by name with T-26's `verify.md5` (`artifacts/review/T-26/verify/`): all 53 equal (`artifacts/review/T-27/baseline/now.md5`).

**Each new check failing on a broken product.** Each break made in the product, run through the suite's script, reverted (`artifacts/review/T-27/breaks.log`, apart from rows 3 and 6, rerun after the checks were mended).

| The break | The check that failed |
|---|---|
| `check` with the volume sign flipped | `check: problems` (the inside-out problem is missing) |
| `select` with `>=` read as `>` | `faces where min.y >= 3.6` got [15], its count, `move: moved` (4 ids), the chimney's `min.y` after the move, and the chimney-rose pixels: 5 |
| the wireframe's edge lift at 0 (the product has no "scale"; `LineLift` is its stand-off, so this is the nearest break) | `shed-front-wireframe`: the ridge stands 0.05, -0.08 and -0.01 pixels above where it projects, not 0.5 to 1.7. The first form of this check, the closest pixel to the line colour within 10, did **not** fail: the lift moves a line about a pixel, so it was replaced by `LineStandsOff`, which does |
| labels drawn for back faces | `shed-front-labels-faces` and `shed-1024-labels-both`: 12, among them face 12's backing and the unmarked faces 0, 1, 9 and 11 |
| the sheet's tiles in reverse order | `shed-axes-sheet`: 170, from the `+z` tile's first sample |
| `open` dropping the last face | `open: faces` 17, `open: warnings`, and 16 more (a first form of the scenario crashed on a missing face id; mended, so all are reported) |

**Not checked, or not seen to fail on its own:** a mirrored or a flipped camera (not broken in the product; the checks are projected from the mesh, but none was run broken); the stray vertex's absence at 1024, the door's shade equal to the wall's, the gutter pixels, the captions (all pass, none run under a break; the sheet break stops at the tile checks only in that it reports every failure, so the gutters and caption passed under it); the `-x` tile's culled-wall pixels under a break; the `move` error text (names 3 and 9, which is weak: the single digits are in much text); `shed-open-edges`' orange eave pixel under a break.

**Readings, for the person.** `faces where y > 10` and `y >> 10` replace the spec's `centre.y` (ruled). The labels frames compare the backing at the corner of each label's padding, not its centre, so a letter at the centre cannot hide it. The after-move frame is reframed by the product (the bounds grew to y 4.4), so "a pixel that was chimney is now sky" is asserted at the pixels the new camera shows as sky or chimney, found by the ray test, with the old frame as the other side.

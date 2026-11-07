---
description: >-
  The shed scenario finds every planted fault with check and select, moves a selection and shows the shed in every look, in its own suite
---
# T-27 — Leaky shed scenario

**Status:** Open

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
   - `faces where centre.y > 10` → `count` 0, no error;
   - `faces where centre.y >> 10` → a tool error naming character 20 and the operators.
4. **A selection feeds an edit.** `move` the chimney selection `by [0, 0.5, 0]`: `moved` has 8 vertex ids, `check` after still reports two shells and the same problems, and the chimney's `min.y` has risen by 0.5 in `get_mesh`.
5. **Looks**, each frame with a positive pixel check and one a wrong camera would fail (the shed is asymmetric: the door is off centre on the +Z wall and the chimney is off centre on the roof):
   - `shed-front-labels-faces`: view front, labels faces, 512: the +Z wall's label box present (darker than the wall, white inside), the -X wall (flipped, so culled) has no label and shows what is behind it;
   - `shed-front-wireframe`: view front, wireframe: the fill at a wall centre, an edge pixel on the ridge, the missing roof slope shows the far wall's edges through the gap;
   - `shed-front-normals`: view front, normals: the remaining roof slope's pixel is sRGB of `((n + 1) / 2)` for its normal ±2; the flipped wall is missing (the pixel behind it is seen);
   - `shed-open-edges`: view front, shaded, `highlight` with the open-edge selection from step 3: an orange pixel on an eave edge, a grey pixel on the lid of a closed wall;
   - `shed-axes-sheet`: the six axis views, orthographic, 256, labels none: 772 x 514, gutters, the `+z` tile shows the door (a pixel check on the door's position in that tile), the `-x` tile shows the flipped wall as missing;
   - `shed-1024-labels-both`: view `[1.6, 1.2, 2.2]`... use `front` at 1024, labels both: the stray vertex at (6, 0, 0) is labelled (it is a vertex on no face, so it is **not** labelled under the front-facing rule; check that it is absent and say so in the description if the rule surprises);
   - `shed-front-after-move`: shaded, after step 4: the chimney's pixel has moved up (a pixel that was chimney is now sky, one above it is now chimney).
6. **Error path**: `move` with `edges: [[3, 9]]` (not an edge) is a tool error naming the pair; the next `shed-front` capture is byte-identical to the one after step 4.

**The suite `scenario-shed`** (`regression`, `gpu`, `vulkan`) in `tests.json`; `Carve.slnx` lists the project; `CLAUDE.md`'s *Layout* gains the `Shed` row.

Pick sample pixels from the first good run, record them as constants with a comment saying why each was chosen, and **show each check failing** on a broken product, as a table. Breaks to use: `check` with the volume sign flipped; `select` with `>=` read as `>`; the wireframe scale at 1.0; labels drawn for back faces; the sheet's tiles in reverse order; `open` dropping the last face.

**Second run**: run the suite twice and compare every frame byte for byte. Record it.

**Show the person** every frame (`mcp__waystone__review`, kind `approve`), the sheet first. Their approval is the gate.

## Notes

- **Ruled by the person, 2026-11-07, on what T-23 found.** The shed is 4 x 3.9 x 3 m, not 3.5: the chimney sits at y 3.6 to 3.9, clear of the 3.5 ridge, so `faces where min.y >= 3.6` stands. `faces where normal.y > 0.5` returns faces 11 and 14, not the slope alone: the inside-out chimney's underside points up, and that is a true finding, so assert both and say why. The door's four edges carry non-manifold and winding warnings as well as `duplicate-face`, so the winding edges are the flipped wall's four and the door's four; T-23's `### As built` names them.
- **Open from T-23, not ruled.** `front`'s eye is on the +X side, so the flipped -X wall is seen from inside, where its reversed loop faces the camera: by the winding it is drawn, not culled, though nothing has rendered it. This scope's `shed-front-labels-faces` and `shed-front-normals` checks expect it missing. Check them against the geometry first; if they cannot pass, stop and ask.
- **Decided by the person at planning (2026-11-05):** the leaky shed is the sample; its fault list (T-23); the looks (T-25), labels and sheet (T-26) it is shown in.
- **Decided at planning on the planner's recommendation:** the step list and the frames above; the shed is opened from the committed sample through `open`, so M-3's `open` is exercised on a file an agent will meet.
- **Open rules: stop and ask.** Any change to the shed, a tool's behaviour or a look to make a step pass. Any tolerance looser than 10/255 (normals colours ±2). Any golden or cross-machine comparison. A planted fault not found, or found twice: report, do not patch the shed.
- **A fixer may not change** the shed, the views, the light, the framing or the look colours.
- This task is the run and its frames, split from the code on purpose. A defect found in a tool is a bug filed under M-4, not a fix here, unless it is a one-line fix in this task's own files.
- Sonnet is enough: every id, colour and caption it asserts is written down by T-22, T-23, T-25 and T-26.
- Its frames fill context. Look at them together at the end.
- Nothing has sound or motion, so there is no listening or real-time pass.
- Run `finish` from inside the worktree and leave the folder.

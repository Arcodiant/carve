---
description: >-
  scenario-replay calls every model-changing tool on small models of its own, saves, replays the exported script and compares the files byte for byte, and shows planted breaks caught
---
# T-234 — The replay scenario

**Status:** In Progress

**Pri:** P1

**Milestone:** M-34

**Batch:** — (standalone)

**Depends on:** T-233

**Verify:** server-unit, scenario-replay

**Perf:** skip: Carve declares no heavy suites

## Goal

On the real `carve`, a session that calls every model-changing tool replays from its own exported script to a byte-identical Carve file, and D-8's invariant (*An exported script replays to the same model*) is enforced by a suite.

## Scope

A new scenario, `scenarios/Carve.Scenario.Replay`, built for replay on small models of its own (the person, 2026-11-26: not other scenarios' builds). Suite `scenario-replay` (`regression`, `gpu` as the other scenarios), in `tests.json` and `Carve.slnx`. Stages, one class each, in a `ReplayStages.cs` order list. The agreed outline:

```
1 every-tool   small plate + cube + 3-bone rig, built
               calling EVERY model-changing tool once
               (define_mesh .. displace, clear, open,
               a part with an instance, a material with
               a samples/textures PNG, define_target
               from a file, a clip, a target)
               -> save A, save_script, clear,
                  run_script, save B: A == B bytes
               -> render front before/after: equal
2 journal      read-only + refused calls absent;
               undo drops its line; open and clear
               restart it; undo of clear restores it;
               105 moves -> all 105 in the script
3 numbers      0.1, 0.30000000000000004, -0, 5e-324,
               1e308, unicode names: bit-exact
   & paths     script in a subfolder: paths written
               relative to it, run from there
4 atomic       error at line N: model, undo and
               version unchanged, message names N;
               run then one undo = back; run_script,
               save, render inside a script refused
5 breaks       on the scenario side: a line dropped,
               two swapped, a number changed ->
               each fails A == B or errors

coverage: tools/list minus {read-only list}
          must all appear in stage 1's script
```

- **Stage 1** builds in the run folder, copying what it reads (a texture from `samples/textures/`, a `.carve` for `open` and `define_target`, a graph for `displace` from `samples/graphs/`) into `inputs/`, so the run reads nothing outside its folder. Each call's result is checked as the tool's own tests would check it only where it matters to the replay; the point is the byte comparison. The front render of the model before and after replay is compared byte for byte (one frame pair, `replay-before`, `replay-after`).
- **Coverage**: the scenario holds two lists, the model-changing tools stage 1 calls and the tools that are not model-changing; it fails when tools/list has a tool in neither, naming it, so a new tool forces a row.
- **Stage 3** writes the script to `scripts/sub/` and runs it from there, and checks the written paths are relative (`../../inputs/...`) by reading the file.
- **Stage 5** edits the exported text on the scenario side (no product flag, no debug hook): drops one statement, swaps two that do not commute, changes one number in a `move`; each must give a different save or a `run_script` error, and the table of breaks is written to `breaks.tsv` in the run folder with a `Caught` column.
- **Size**: measure the suite's time on its first run and write it in `### As built` beside scenario-gatehouse's, as the person asked about the cost of replay.
- **CLAUDE.md**: a layout row for the scenario.

## Notes

- The `Verify:` line names `server-unit` only because `scenario-replay` does not exist at planning (lint refuses an unknown suite): add `scenario-replay` to it in the same commit that adds the suite to `tests.json`.
- Makes a real run and its frames are looked at (the two frames, and that they are equal); plan the frame look as a step of its own.
- Which tools exist is read from tools/list at the time; displace (T-223) and M-29's graph slots on materials if they have landed. If a model-changing tool cannot be called in a small model (it needs something stage 1 cannot make), stop and ask rather than leave it out of coverage.
- Reads T-231 to T-233's `### As built` for what is journalled, refused and how paths are written.
- Read the milestone's *Decided at planning (2026-11-26)* first.
- **Ask, don't land.** A rule neither this file nor the milestone settles, or a check that would be loosened, goes back to the person. Show each new check failing on a wrong input (stage 5 is the scenario's own; give the table).
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by one agent on Sonnet 5.5 (`claude-sonnet-5-5`), with no hand-off.

**Suite** `scenario-replay` (`regression`, `gpu`) in `tests.json` and `Carve.slnx`; `Verify:` now lists it; a layout row in `CLAUDE.md`. Project `scenarios/Carve.Scenario.Replay`: `ReplayStages.cs` (order), `EveryToolStage`, `JournalStage`, `NumbersPathsStage`, `AtomicStage`, `BreaksStage`, shared `Replay.cs`.

**Time**: the scenario runs in about 4 s locally (`run-scenario.ps1 replay -NoBuild`), and the suite took 21 s on the verify farm (2 s queued). scenario-gatehouse's declared estimate is 60 s; I did not get a measured gatehouse time here (its wrapper script failed at once with `-NoBuild` in this worktree, not looked into), so the comparison is replay's 21 s against that estimate.

**Stage 1** calls 32 model-changing tools (one session: a cube opened from a file with `mesh`, a plate, a cut and a knifed and a holed and a bridged cube, a subdivided and displaced cube (noise), a tessellated one displaced from `floor-tiles.texgraph`, an unwrapped one, a morph cube with a position target, a file target and a deleted one, a 3-bone rig with weights, `weigh`, two clips and one deleted, a part instance and `keep_level`, a material with an `iron-base.png`). Inputs are copied to `inputs/`. A == B byte for byte on the first run, and the front renders (`replay-before`, `replay-after`) have the same hash. Coverage: `ModelChanging` and `NotModelChanging` lists against tools/list, and every `ModelChanging` tool a statement of the exported script; shown failing by taking `tessellate` out of the list (failed naming it, twice), then restored.

**Breaks** (`breaks.tsv`, run 4):

| Break | Result | Caught |
|---|---|---|
| control: script as exported | file equals A | no (as meant) |
| the cube's move dropped | file differs from A | yes |
| the plate's extrude dropped | refused at line 8 (define_group) | yes |
| loop_cut and merge_faces swapped | refused at line 3 | yes |
| define_mesh plate and extrude swapped | refused at line 6 | yes |
| the move's 0.5 changed to 0.25 | file differs from A | yes |
| control: whitespace only changed | file equals A | no (as meant) |

**Coverage lists**: model-changing (all in the script): define_mesh, move, extrude, delete, merge_faces, bridge, plane_cut, loop_cut, knife, subdivide, tessellate, displace, unwrap, project_uvs, set_uvs, set_colour, define_material, set_material, set_shading, mark_edges, define_group, define_part, keep_level, define_skeleton, set_weights, weigh, define_clip, delete_clip, define_target, delete_target, open, clear. Not statements: undo, run_script, save, save_script, export, bake, check, select, render, render_glb, help, the five get_ tools.

**Findings**: no product bug in T-231..T-233 found. One limit: `1e308` cannot go in a script, because every tool keeps floats and refuses it (`define_part` scale, `define_clip` time); stage 3 uses `1e38` for the clip's duration and keeps `0.1`, `0.30000000000000004`, `-0`, `5e-324` (vertices) and unicode names; the format-level `1e308` is T-230's test. Stage 2 uses `undo` twice, since `undo` has no `steps`. The "version unchanged" part of stage 4 is checked through the model, the saved file, the journal and what undo then does, since no tool shows the version.

**Frame review**: one subagent looked at the frame (identical bytes, so one opened): non-empty model, three objects visible (a box, a bumpy box, a red-topped plate), no defect; the others overlap or sit behind in the front view, and the group is small in the frame. No golden or choice for the person.

**Not checked**: the gatehouse suite's measured time; the `scenario-replay` run on another machine than the verify farm and this one.


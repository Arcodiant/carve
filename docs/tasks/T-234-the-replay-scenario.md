---
description: >-
  scenario-replay calls every model-changing tool on small models of its own, saves, replays the exported script and compares the files byte for byte, and shows planted breaks caught
---
# T-234 — The replay scenario

**Status:** Open

**Pri:** P1

**Milestone:** M-34

**Batch:** — (standalone)

**Depends on:** T-233

**Verify:** server-unit

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

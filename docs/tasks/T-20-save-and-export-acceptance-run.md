---
description: >-
  Fresh agents build and save a crate, reopen it in a later session and export it to a game's content folder, recovering from a wrong file name
---
# T-20 — Save and export acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-3

**Batch:** — (standalone)

**Depends on:** T-19

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Agents that have never seen Carve, in an ordinary MCP client with plain `carve`, carry a model across two sessions and into a game's content folder, from the tool descriptions alone. The `.glb` they make loads in the viewer. That proves the milestone from a plain launch.

## Scope

**The run**, in a scratch folder `%TEMP%\carve-m3-acceptance\round-N\`, outside every Carve `CLAUDE.md`. The folder holds an `.mcp.json` of plain `carve` only (the README's entry) and an empty `game/Content/models/`. Use two fresh `claude -p` sessions with `--strict-mcp-config`, no Carve docs, both started in that folder. Each is a fresh carve process.

- **Session 1** prompt:

  > *Using the carve tools, build a wooden-crate shape: start from a single cube, then use the edit tools, not a long list of coordinates, to give it a raised frame round each side with recessed panels, and a handle bar on two opposite sides. Look at it from at least two sides. Save your work as crate.carve in this folder. Finish with get_mesh.*

- **Session 2** prompt:

  > *With the carve tools, open crates.carve from this folder. Make the handle bars stand out twice as far, save it again, and export it so a game whose content folder is game/Content can load it as models/crate.glb. Finish with get_mesh.*

  `crates.carve` is the **planted mistake** (agreed at planning). No description can warn about a wrong name. The agent must recover from `open`'s error, which lists `crate.carve`.
- **Then**: run `scripts/run-viewer.ps1` on the round's `game/Content` with `models/crate.glb` and the two agreed views. Keep the frames and `view.json` beside the transcripts.
- Keep in the round folder, and copy to the milestone worktree's `artifacts/m3-acceptance/round-N/`:
  - both transcripts and their images;
  - both final `get_mesh` results;
  - `crate.carve` after each session;
  - the `.glb`;
  - the viewer output.

**Pass means:**

- session 2 recovers from the wrong file name using the error text alone, and opens `crate.carve`;
- session 2's mesh after `open` equals session 1's final `get_mesh` (ids and positions);
- the edit is made after `open`, and saved;
- the `.glb` is at `game/Content/models/crate.glb`;
- the viewer loads it with exit 0, and its frames show a crate;
- the result is closed with no warnings.

How many calls each session took, and whether it used `undo` or `render`, are recorded, not judged.

**Rounds.** Wherever an agent hesitates, guesses wrong or misuses a tool (above all the paths, `replaced`, and where the `.glb` goes), sharpen that tool's description and repeat both sessions fresh. **At most three rounds.** If it still fails, stop and report.

**Afterwards:**

- Update D-1's *Tool shape* question with what the run showed.
- Put notes for the milestone's retrospective in the closing report.
- Show the person the final round's viewer frames (`mcp__waystone__review`, kind `approve`).

## Notes

- **Decided by the person at planning:** two sessions, a scratch game folder, the wrong-file-name plant.
- A real run fills context. This task is the run, its transcripts and text edits, split from the code on purpose. It changes description text, the README and docs only. A code defect found here is a bug filed under M-3, not a fix here.
- A description change must keep every worked-example test and every scenario passing. Run `cairn task T-20 verify`.
- **Open rules: stop and ask.**
  - A change to a tool's arguments or behaviour.
  - Any change to either prompt beyond the planted mistake.
  - Using a real game repo instead of the scratch folder.
- The prompts ask for a final `get_mesh` (M-2's retrospective), so there is a mesh to compare.
- Nothing has sound or motion, so there is no listening or real-time pass.
- Run `finish` from inside the worktree and leave the folder.

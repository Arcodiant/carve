---
description: >-
  Fresh agents build and save a crate, reopen it in a later session and export it to a game's content folder, recovering from a wrong file name
---
# T-20 — Save and export acceptance run

**Status:** Done

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

**Rounds.** Wherever an agent hesitates, guesses wrong or misuses a tool (above all the paths, `replaced`, `overwrite`, and where the `.glb` goes), sharpen that tool's description and repeat both sessions fresh. **At most three rounds.** If it still fails, stop and report.

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

### As built

Run on **Claude Sonnet 5.5** (`claude-sonnet-5-5`). The `claude -p` sessions ran on Claude Opus 5.5 (`claude-opus-5-5`, from each transcript's init event), with `--strict-mcp-config`, an `.mcp.json` of plain `carve` only (`dotnet run --project` on this worktree) and `--allowedTools "mcp__carve__*"`, from `%TEMP%\carve-m3-acceptance\round-N\` (an empty `game/Content/models/`). Prompts word for word as in the spec. Transcripts, final `get_mesh` results, `crate.carve` after each session, the `.glb` and viewer output (`front`, `minus-x`, `view.json`) are in `D:\Repos\carve\branches\milestone\M-3\artifacts\m3-acceptance\round-N\`; `run-round.sh`, `summarize.py`, `meshcmp.py` are beside them.

**No description, README or code changed.** Only D-1's *Tool shape* question was updated with the run.

| | Round 1 | Round 2 |
|---|---|---|
| Run condition | agent has its usual file tools | agent has only the carve tools (`--tools ""`) |
| Session 1 carve calls | 19 (1 define_mesh, 9 extrude/move edits, 5 renders, 2 get_mesh, 1 save) | 23 (1 define_mesh, 7 extrude, 4 move, 7 render, 3 get_mesh, 1 save) |
| Session 2 carve calls | 9 | 12 |
| `undo` | no | no |
| Planted `crates.carve` | not hit: listed the folder with its own Glob first, then opened `crate.carve` | hit: `open` error `no file at ...crates.carve; the .carve files in ... (1) are: crate.carve`; next call opened `crate.carve` |
| Session 2 mesh after `open` == session 1 final `get_mesh` | yes (identical text) | yes (identical text) |
| Edit after `open`, saved | yes: two `move` calls, `save` ... `(replaced)` | yes |
| `.glb` | `game/Content/models/crate.glb`, 172 triangles | same place, 236 triangles |
| Viewer | exit 0, frames show a crate | exit 0, frames show a crate (approved by the person) |
| Final `get_mesh` | 88 v / 86 f, closed, no warnings, bounds x -0.66..0.66 | 120 v / 118 f, closed, no warnings, bounds x -0.71..0.71 |

Round 2 was added because round 1 did not exercise the plant. The change is the run condition only (the agent's built-in file tools removed, which is how an MCP-only client looks); no prompt changed. Both rounds pass; round 1 passes except the plant, which round 2 covers. Paths: round 1 session 2 used absolute paths, round 2 relative ones (`crate.carve`, `game/Content/models/crate.glb`); both worked. No misuse of `replaced` or where the `.glb` goes. Round 1 session 2 read `.mcp.json` (odd, harmless). Each session pays a `ToolSearch` call that is not counted above.

**Hesitations.** None that pointed at a description, so none was sharpened. "Twice as far" was read differently in each round (bars moved by 0.11 and 0.13 on a 0.05 to 0.08 stand-out); recorded, not judged.

**Review.** The person approved (no notes) round 2's viewer `front` and `minus-x` through `mcp__waystone__review`.

**For the retrospective.** The planted mistake only fires when the agent has no file tools; agents with them look first (as in M-2). The plant is cheap to keep but a poor test of the error text for such agents, and the error text is already covered by T-17's tests. Agents never needed `undo` and called `get_mesh` several times when asked to finish with it. Handles are still solid bars (no bridge tool).

**Not checked.** The `.glb` was loaded only by the Carve viewer, not a real game. No window run. A third round was not needed.

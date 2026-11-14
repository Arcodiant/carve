---
description: >-
  Fresh agents unwrap a model from the tool descriptions alone and find and fix a planted unmapped extrusion
---
# T-62 — UVs acceptance run

**Status:** Done

**Pri:** P1

**Milestone:** M-6

**Batch:** — (standalone)

**Depends on:** T-61

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Agents who have never seen Carve give a model good UVs from the tool descriptions alone, find a fault by looking, and export the result.

## Scope

- **Run conditions**, those of T-28's Scope:
  - fresh `claude -p` sessions on Opus 5.5, on plain `carve` published in Release from the milestone branch into `%TEMP%\carve-m6-acceptance\`;
  - `--mcp-config .mcp.json --strict-mcp-config`, `--allowedTools "mcp__carve__*"` and `--tools ""`, from round 1;
  - the prompt on stdin, and no Carve docs.
- **The input:** `lantern-barrel.carve`, made in the run folder from `samples/barrel.carve`:
  - a knob extruded from the top cap after the unwrap, whose walls are unmapped (the plant);
  - one middle ring moved outward, so the side island is stretched.
- **The prompt** asks the agent to open the file, give it UVs fit for a texture (nothing stretched, missing or mirrored), look at the result, export it as `models/barrel.glb` into a content folder, and end with `get_mesh` and `check`. It names no tool for UVs.
- **Success:**
  - the final mesh has no unmapped, flipped or overlapping faces, and `check` gives no UV warning;
  - the agent re-unwrapped or re-projected after seeing the fault, by `checker`, `uvs` or `check`;
  - the `.glb` has `TEXCOORD_0` and loads in the viewer.

  Record the tools used, the call count, and whether each look was used.
- **Rounds:** up to three. One description edit is allowed between rounds, and it is put to the person before the next round.
- **Plant:** the unmapped walls can be fixed without guessing the person's intent: an understood fault, fixed. If no agent meets it (for example by unwrapping everything first), record that and ask the person whether the round counts.

## Notes

- Sonnet.
- **Name every run condition in the stop list:** a change to the flags, the model, the input file or the prompt goes back to the person before the round. T-20's `--tools ""` slipped through on that gap.
- Keep every round's transcript, final `get_mesh`, `check`, frames and `.glb` in the milestone worktree's `artifacts/m6-acceptance/round-N/`.
- After the milestone merges, `C:\tools\carve` is re-published by hand (the person's install). That is not this task's job; say so in the report.
- **Open rules: stop and ask.** Anything above that the run does not meet, and any change to a run condition.
- Run `finish` from inside the worktree and leave the folder.

### As built

Written by an agent running on **Sonnet 5.5** (claude-sonnet-5-5). The session under test was a fresh `claude -p` with no `--model` (the default, **Opus 5.5**), flags exactly as the run conditions, prompt on stdin. Carve was a Release publish of `milestone/M-6` at `e97671b` into `%TEMP%\carve-m6-acceptance\carve-bin`, named by absolute path in `.mcp.json`; the round ran in `%TEMP%\carve-m6-acceptance\round-1\`. No extra flag (no stream-json): the transcript is the session's own file from `~/.claude/projects`, images extracted from it. Evidence in `artifacts/m6-acceptance/round-1/` of this member's worktree (the spec says the milestone worktree; it was written here so the milestone worktree stays clean, and merges to the same path).

**Input.** `lantern-barrel.carve`, built through `carve` itself (`open` the sample, 8 `move`s, one `extrude`, `save`): ring 2 (vertices 16-23, y 0.5) moved 0.2 m radially outward after the unwrap (side island stale and stretched); the top cap (face 33) extruded 0.15 with inset 0.1, giving 8 unmapped walls (faces 34-41). 48 vertices, 42 faces; `check` showed only the 8 "no UVs" warnings. The person approved the prompt and the construction before round 1 (waystone review, 2026-11-13). Prompt: `round-1/prompt.txt`.

**Round 1** (no description edit). 12 carve calls, no ToolSearch (the tools were available directly): `open`, `get_mesh`, `check`, `mark_edges` (seams down the knob's wall and round the lid), `unwrap` (side stretch 1.48), `mark_edges` (rings 1-3 seamed), `unwrap` (7 islands, all stretch 1.0, flipped 0, fill 0.28), `render` mode `uvs`, `render` mode `checker` (views front, back, an oblique direction, -z; columns 2), `export` `content/models/barrel.glb`, `get_mesh`, `check`. No `undo`, `save`, `project_uvs`, `set_uvs`, `highlight`. Looks: UV layout used, checker used, both after the final unwrap; neither before it.
- **Plant:** met. The agent saw the unmapped walls in the first `check` (8 warnings, faces 34-41), named them and the cause (incomplete seams), and re-unwrapped. The stretch it saw only as `unwrap`'s stretch figure (1.48) on the first re-unwrap, not in a look, and it fixed it by seaming the middle rings so the side became four bands. It did not unwrap everything blind first: `check` came before any edit.
- **Success:** final `check` ok, no problems, no warnings, 42 mapped, 0 unmapped; the last `unwrap` gave seven islands, stretch 1.0, flipped 0 (islands are packed apart, so no overlap); the checker frame (front, back, oblique, -z) shows even cells and no magenta; `barrel.glb` (92 triangles) has `POSITION`, `NORMAL` and `TEXCOORD_0` and loads in F3D (`f3d-view.png`, rendered, not opened by eye). The agent did not save `lantern-barrel.carve` and said so.
- **Not seen:** no tool description hesitated or was misused; no error was returned. The agent's change to the layout (four side bands) was its own choice and it told the person the texture must match.

**Rounds 2 and 3** not run: round 1 met every success item and nothing in it called for a description edit.
**Not checked:** the `.glb` in the Empyrean engine itself (F3D only); overlap and flips of the final mesh are as `unwrap` reports them, not re-derived by me. `C:\tools\carve` is re-published by hand after the milestone merges and is not this task's job.

---
description: >-
  Fresh agents unwrap a model from the tool descriptions alone and find and fix a planted unmapped extrusion
---
# T-62 — UVs acceptance run

**Status:** In Progress

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

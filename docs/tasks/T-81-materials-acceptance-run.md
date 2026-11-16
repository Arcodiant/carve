---
description: >-
  Fresh agents texture a model from the tool descriptions alone and recover from a planted missing texture path
---
# T-81 — Materials acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-7

**Batch:** — (standalone)

**Depends on:** T-80

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

The materials tools are usable by an agent with no Carve docs: fresh agents texture a barrel, export it, and recover from a texture path that does not exist by reading the error.

## Scope

- **Run conditions**, as T-28 settled them and T-20's ruling added:
  - `claude -p` on a Release publish of `carve` built from this branch;
  - `--mcp-config .mcp.json --strict-mcp-config`;
  - `--allowedTools "mcp__carve__*"` and `--tools ""`, so the agent has no file tools of its own;
  - the prompt on stdin;
  - no Carve docs.

  Each round runs in a fresh folder under `%TEMP%\carve-m7-acceptance\round-N`.
- **The folder** holds:
  - `barrel.carve`, a copy of M-6's `samples/barrel.carve`, unwrapped and with no materials;
  - `textures/` with T-77's `staves-base.png`, `staves-normal.png`, `iron-base.png` and `iron-mr.png`;
  - a `models/` folder.
- **The prompt** asks the agent to:
  - open the barrel;
  - make the top and bottom bands of side faces iron (metal, with the rust map) and the rest wood staves with their normal map;
  - export `models/barrel.glb`;
  - finish with `get_mesh` and a `render` from `front`.
- **The plant:** the prompt names the rust map `textures/iron-mr.png`, but the folder holds it as `textures/iron_mr.png`. `define_material`'s error lists the PNGs in that folder, so the agent can fix the path without guessing the person's intent.
  - The plant counts as exercised when the agent reads the error and corrects the path. Asking instead counts too, as M-4 ruled, but that round is then judged on the error being understood.
- **Pass:**
  - every side face carries a material, iron on the band faces 0 to 7 and 24 to 31, and `check` warns of nothing;
  - the `.glb` loads in the viewer with `--sky`, with no warnings and two Pbr materials;
  - the final render shows wood and iron.

  Keep each round's transcript, `get_mesh`, render and viewer frames.
- Up to three rounds.

## Notes

- **Sonnet.**
- **A description changed between rounds goes to the person first.** M-4's round 3 showed that a stronger description makes the agent avoid the plant rather than read its error.
- **Open rules: stop and ask.** These are the open rules, and there is a stop on each:
  - any change to the run conditions above (M-3's lesson: name them here, not only the prompt);
  - any change to the prompt or the plant after round 1;
  - any description edit;
  - any loosened pass rule.
- **Carried from earlier plans:**
  - `finish` read whole, with its `Merge task/T-NN` line;
  - report a long `verify` wait;
  - evidence in the milestone worktree's `artifacts/m7-acceptance/round-N/`;
  - re-publish `C:\tools\carve` only when the person asks.
- Run `finish` from inside the worktree and leave the folder.

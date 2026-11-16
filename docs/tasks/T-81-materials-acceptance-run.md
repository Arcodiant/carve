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

## Round prompt

Fixed before round 1 (stdin, one paragraph; `textures/iron-mr.png` is the planted path, the file on disk is `textures/iron_mr.png`):

> With the carve tools, open barrel.carve from this folder. It is a barrel going into a game, and it has no materials yet. Texture it: the top and bottom bands of side faces (the topmost ring and the bottommost ring) are iron, which is metal, using textures/iron-base.png as its colour map and textures/iron-mr.png as its rust map; the rest of the side faces are wood staves, using textures/staves-base.png as their colour map and textures/staves-normal.png as their normal map. Export it as models/barrel.glb in this folder. Finish with get_mesh and a render from front, and tell me what you found and what you did.

Checked against the folder and the pass: the files named are the four textures (iron-mr.png the plant), `barrel.carve`, `models/`; no counts in the prompt, so nothing can clash with the model (rings of 8 side faces: bottom 0 to 7, top 24 to 31); `front` is a render view. The caps and the cylindrical-versus-unwrap layout are not in the prompt or the pass.


### As built

Written by an agent running on **Sonnet 5.5** (claude-sonnet-5-5). The session under test was a fresh `claude -p --model sonnet`, which reported **claude-sonnet-5-5** in its init line (the Notes say Sonnet). Flags as the run conditions plus the stream-json pair: `--mcp-config .mcp.json --strict-mcp-config --allowedTools "mcp__carve__*" --tools "" --output-format stream-json --verbose`, prompt on stdin (text above, also `prompt.txt`). Carve was a Release publish of this branch (`task/T-81` at 5ae3344, equal to `milestone/M-7`) into `%TEMP%\carve-m7-acceptance\carve-bin`. The round ran in `%TEMP%\carve-m7-acceptance\round-1\` with `barrel.carve` copied from `samples/` (no materials), the four textures, `models/`; `iron-mr.png` was renamed `iron_mr.png` on disk (the plant).

**Evidence:** `G:\My Drive\artifacts\carve\M-7\T-81\round-1\` (transcript.jsonl, prompt.txt, each tool result as `res-N-*`, get_mesh.json, check.json, render-front.png, view.json, viewer-front.png, barrel.glb). Not in the worktree.

**Round 1** (the only round; no description or prompt edit). 11 carve calls: `open`, `get_mesh`, `define_material` iron (with `textures/iron-mr.png`), `define_material` staves, `define_material` iron again (`iron_mr.png`), `set_material` iron on faces 0-7 and 24-31, `set_material` staves on 8-23, `check`, `export`, `get_mesh`, `render` front. No ToolSearch, `undo`, `save`.
- **Plant: exercised.** The first iron `define_material` failed with "no file at ...\textures\iron-mr.png; the .png files in ...\textures (4) are: iron-base.png, iron_mr.png, ...". The agent retried with `iron_mr.png` and said in its answer that the name differed and which it used. It had no file tools, so only the error could show it.
- **Pass, every side face a material:** `get_mesh` `face_materials` is iron 0-7 and 24-31, staves 8-23; all 32 side faces covered. Caps (32, 33) have none; the agent said so and offered to apply one.
- **`check`:** ok, no problems, no warnings, closed, 34 of 34 faces mapped.
- **Viewer with `--sky`:** exit 0, `view.json` `sky` true, 76 triangles, every material Pbr; no warnings key in `view.json` and none on the console. The glb holds **three** materials, not two: the default grey for the uncovered caps (untextured, metallic 0), iron (Textured, metallic 1) and staves (Textured, NormalMapped). I read the pass's "two Pbr materials" as the two the agent made; the third is the default for the caps, which the prompt leaves out. The person approved on that reading (below).
- **Render shows wood and iron:** both the agent's `render` and the viewer frame show light-wood staves with dark gaps and grey iron bands with rust at the top and bottom ring; the top cap is pale grey (default). Looked at by eye, scaled as is (512 px), no pixel sample.
- **Not in the pass, as told:** the staves lined up on the stave faces in the frames (staves' gaps appear at the face edges), although the layout was built for `cylindrical` and the sample is unwrapped; I did not measure it. The caps are unmapped to textures, as expected.

**Rounds 2 and 3** not run: round 1 met every item and nothing hesitated or was misused.

**Review:** the person approved `render-front.png` and `viewer-front.png` (waystone review, 2026-11-16, "Approved", no notes), including the three-material reading above.

**Not checked:** the glb in the Empyrean engine (viewer only); iron's rust under pixel sampling; `C:\tools\carve` not re-published (the person asks for that). Engine needs: none.

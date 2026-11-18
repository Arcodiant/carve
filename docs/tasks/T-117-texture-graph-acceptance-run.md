---
description: >-
  Fresh agents build a wood material in patina from the tool descriptions alone, recover from a broken starter graph and put it on the barrel
---
# T-117 — Texture graph acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** T-114, T-115, T-116

**Verify:** patina-unit, scenario-patina

## Goal

Show that a fresh agent, with only patina's and carve's tool descriptions, can open a broken graph, find and fix the
fault from the error alone, look at its material, export it, and texture the barrel with it in Carve.

## Scope

- **Run conditions**, as earlier rounds: Release publishes of `patina` and `carve` in `%TEMP%\carve-m27-acceptance\`;
  an `.mcp.json` naming both; `claude -p --tools "" --mcp-config .mcp.json --strict-mcp-config --output-format
  stream-json --verbose`, prompt on stdin, the **default model with no `--model` flag** (M-7's T-81 changed it
  without asking, and that was a finding). Any change to these conditions goes to the person first.
- **Round folder:** `barrel.carve`, a copy of `samples/barrel-planks.carve` with its staves material set back to
  plain colour and the textures it still uses beside it, plus `starter.texgraph`, a planks-like graph in which one
  `blend`'s `mask` names `grain_2` where the node is `grain2`. The agent has no file tools, so only `open`'s fault
  can reveal the plant.
- **The prompt asks for**, in its own words: a weathered-wood material starting from `starter.texgraph`, darker and
  rougher than it is now; a look at it before export; export at 512 into `textures/wood/`; then, in carve, the
  barrel's `staves` material taking the exported base colour, normal, roughness and occlusion; a silent `check`; a
  front render; and a save. Work the prompt's numbers for clashes before round 1 (M-5): sizes, paths relative to the
  `.carve` file, and role names against slot names.
- **Pass:** `open` reported the fault naming `grain_2` and the port, and the agent fixed it through `edit_graph` or
  `define_graph`. It called `swatch` at least once. Four PNGs at 512 are in `textures/wood/`. The saved barrel's
  `staves` material names them. Carve's `check` is silent. The render shows wood that differs from the starter's
  (judged by sampling stave-middle pixels against an export of the unedited starter, not only by eye). Recovery is
  judged by the error being understood, not by luck (M-4).
- **Evidence**: the transcript, round folder and frames, at an absolute path outside `artifacts/`, which is ignored:
  `G:\My Drive\artifacts\carve\M-27\T-117\round-N\`, or `C:\Users\andre\artifacts\carve\M-27\T-117\round-N\` where
  there is no G:. Written by the member itself (M-6, M-7).
- A description change between rounds is the person's call. Record what the agent misread and the wording proposed.

## Notes

- Decided at planning (2026-11-17, by the person): the plant is the broken starter graph's bad input id.
- Sonnet should do, as M-6's and M-7's acceptance runs did. One agent per round.
- `cd` out of the worktree before `finish` (M-7), and keep the evidence where the Scope says first.
- Touches no product file. If the run shows a defect, reserve a bug and stop rather than fix it here.
- **Verify line:** at planning, `cairn lint` accepts only suites already in `tests.json`, so this file names none it will add. Once patina-unit and scenario-patina (T-113 adds them) are in `tests.json`, add them to this file's `**Verify:**` line (creating the line if there is none), with the task's other suites.

### As built

Run by Claude Sonnet 5.5 (`claude-sonnet-5-5`). The fresh agent in both rounds reported **claude-opus-5-5** in its init line:
the default model with no `--model` flag is Opus 5.5 here, not the Sonnet the Notes expected. Not finished: the person's review
of round 2 is "Rejected" with two questions (below); `finish` waits on their answer.

**Prompt checks before round 1.** Size 512 is in the power-of-two list. patina's `export` folder is relative to patina's working
directory, carve's texture paths to the `.carve` file; both servers start in the round folder, so `textures/wood/` is the same place for both
(the agent used `textures/wood/<role>.png` unchanged). Role names against slot names: `roughness` goes in `metallic_roughness_texture`, which the prompt
says as "roughness"; `export`'s answer names each slot, which the agent used. The staves material in `barrel.carve` is set to plain `#aa7040`, metallic 0, roughness 0.7;
the textures beside it are `textures/` and `graphs/planks/` (the `rune-stave` material still uses the latter). The plant: `grained` gets a `mask` of a new
`grain2` noise, and the starter names `grain_2`; `open` reports "node 'grained' input 'mask': names 'grain_2', which is not a node; the nodes are ... grain2 ...".

**Run conditions.** Release publishes of this branch in `%TEMP%\carve-m27-acceptance\`, `.mcp.json` naming both, `claude -p --tools "" --mcp-config .mcp.json --strict-mcp-config
--output-format stream-json --verbose`, prompt on stdin, no `--model`. **The conditions as written were refused by the permission system**: every MCP call was denied
(`refused-transcript.jsonl`, `round-0-refused`). The person approved adding `--allowedTools "mcp__carve__*" "mcp__patina__*"` (as T-81 had); the Scope's list of flags should
carry it next time.

**Round 1** (prompt `prompt.txt`). 15 calls. `open` reported the fault; the agent said the `mask` names `grain_2` where the node is `grain2` and connected it with `edit_graph`
(it called that a guess, the only similar node; the error's node list is what it read). `swatch` twice, wood `#6b5440`, a stain blend, roughness 0.82..0.97, `export` 512 (four PNGs, 512 x 512),
`define_material` staves with white factors and roughness 1 (it reasoned the factors multiply the maps), `check` clean, front render, `save`. Every pass item met: fault named, fixed, `swatch` called,
four PNGs at 512, saved `staves` names them (`textures/wood/*.png`), `check` silent. Stave-middle pixels of the front render (rows 230..290, columns 300..312 and 340..352) against a render of the
starter fixed and exported at 512: (151,108,74) and (121,88,63) against (78,68,61) and (64,56,51); texture stave-middle mean (158,104,60) against (84,66,50); roughness green mean 172 against 228. The person **rejected**: darker, but the texture very coarse and
little visible difference from the normal or roughness.

**Round 2** (prompt `prompt2.txt`: items 1 and 6 changed to ask for finer grain, relief and roughness visible in a close render, and a close render; not a tool or condition change). 28 calls, the same fault understood and fixed in
its first edit; three `swatch` iterations (it saw the normal saturating and eased it), two `export`s, a re-`define_material` after the second (it knew carve reads PNGs when the material is defined), `check` clean, a front and a close render, both saved. Pass items all met again; stave middles (90,80,74) and (73,65,60) against the same reference. The person **rejected** with two questions:
1. *Why is the rune plank notably brighter?* The `rune-stave` material is a separate material (T-116's sample) with the planks' own textures from `graphs/planks/` and emissive `#FF8020`; the prompt names `staves` only, so it is untouched (the agent said so in round 1). By design, not a fault.
2. *Is roughness rendering correctly?* Measured on a copy of round 2's barrel, solid grey base and a solid roughness map of 0 against 1 (render at 512, a stave away from the rune): the mean differs by 0.45 and the largest pixel by 6 of 255, so the roughness map has a small effect in these renders (T-116's review also called its specular "subtle"; the sky is the only light). I cannot tell from this whether that is the engine's PBR lighting under a uniform sky or a defect, so I reserved no bug; it is the person's call.

**Description changes proposed:** none from this run: the agent misread nothing. One note: `define_material`'s roughness/base colour factors multiply the maps; both agents knew it. 

**Not checked:** the exported `.glb` and the viewer; round 3; any change to the tool descriptions. Engine needs: none recorded; the roughness question above stands.

**Evidence:** `G:\My Drive\artifacts\carve\M-27\T-117\round-1\`, `round-2\` and `round-0-refused\` (transcripts, prompts, round folders, frames, key points). Local: `%TEMP%\carve-m27-acceptance\` (reference, tools).

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

---
description: >-
  Fresh agents make a woven rug on patina from the tool descriptions alone and fix what check reports on a planted graph
---
# T-143 — Node library acceptance run

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-142

**Verify:** patina-unit

## Goal

Fresh agents, knowing only patina's tool descriptions, make a named material with the new nodes, give it a variant
through an exposed parameter, and fix what `check` reports on a graph handed to them. Whether swatches and check let
an agent judge a texture well enough is D-7's open question, and this run is M-28's answer to it.

## Scope

- **Two `claude -p` sessions per round**, on plain `patina`, with `--allowedTools "mcp__patina__*" --tools ""` (no file
  tools, so only the tools' text can reveal a plant). Use the default model, expected to be Opus 5.5 on this machine
  (M-27). Any change to these conditions (a flag, the model, a tool) is the person's to approve before the round.
  - **Session 1, the material:** "a woven rug for a tavern floor, with a border, about a metre across in one tile".
    The prompt asks for at least warp and weft, a border, a tone that varies, a normal, a roughness, one exposed
    parameter (the border's colour), a variant through it, `check` clean, `save` and `export` at 512. It asks for a final
    `get_graph` and a `swatch` sheet. It names no node types.
  - **Session 2, the fix:** `open` a planted graph copied into the round folder, a rug draft with faults that only
    `check` shows: a gradient-map stop at `#050505` that puts more than 1% of base colour below 20, a named mask clipped to
    one value, and a `transform` turned 30 degrees, which does not tile. The agent is asked to make it check clean and
    keep the design (node ids kept, apart from those it must change). Recovery is `check` clean on the saved file with
    the other nodes kept. An agent that rebuilds the graph from nothing has not recovered, and that is recorded.
- **The prompts and plants go to the person before round 1**: work each plant's numbers first (each must fire, and
  nothing in `get_graph`'s text alone should give it away), and work the prompt's numbers for clashes (M-5).
- **Evidence**, at an absolute path outside `artifacts/`: `C:\Users\andre\artifacts\carve\M-28\T-143\round-<n>\`, holding
  each session's transcript, the saved graphs, the exports, the swatch sheets and `check`'s output. A unit test in
  `tests/Patina.Server.Tests` (skipped unless `PATINA_ACCEPTANCE_RUN` is set) reads a round folder: the rug graph opens
  with no fault, checks clean, has a parameter with a variant, and its exports equal a fresh evaluation; the fixed graph
  checks clean and keeps the planted ids it did not need to change.
- **Record** in this file: each round's calls, what the agent found hard, every description or product problem it met
  (expect product problems, not only description ones, M-19), and what was fixed for round 2.

## Notes

- **Decided at planning (2026-11-19, by the person):** the woven rug.
- Sonnet orchestrates; the inner sessions run on the default model. Plan for two agents: the rounds, then the record and
  finish. The person looks at each round's swatch sheets before the round counts, as M-27's rounds were judged on the
  look.
- A problem the run finds in a description is fixed in this task; one in the product goes to the person, who decides
  between a fix here and a filed task.
- **Stop and ask:** before round 1 (the prompts and plants); before any change to run conditions; and before a second
  round.

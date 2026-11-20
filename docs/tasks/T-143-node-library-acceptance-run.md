---
description: >-
  Fresh agents make a woven rug, rusted corrugated iron and a mossy flagstone path on patina from the tool descriptions alone, and fix what check reports on a planted graph
---
# T-143 — Node library acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-142

**Verify:** patina-unit

## Goal

Fresh agents, knowing only patina's tool descriptions, make three named materials with the new nodes, give each a
variant through an exposed parameter, and fix what `check` reports on a graph handed to them. Whether swatches and
check let an agent judge a texture well enough is D-7's open question, and this run is M-28's answer to it.

## Scope

- **Four `claude -p` sessions per round**, each fresh, on plain `patina`, with `--allowedTools "mcp__patina__*" --tools ""`
  (no file tools, so only the tools' text can reveal a plant). Use the default model, expected to be Opus 5.5 on this
  machine (M-27). Any change to these conditions (a flag, the model, a tool) is the person's to approve before the
  round. Each material prompt asks for a normal, a roughness, a tone that varies, one exposed parameter and a variant
  through it, `check` clean, `save` and `export` at 512, and ends with a final `get_graph` and a `swatch` sheet. It names
  no node types and no sample file.
  - **Session 1, a woven rug:** "a woven rug for a tavern floor, with a border, about a metre across in one tile". It
    needs warp and weft and a border. The exposed parameter is the border's colour.
  - **Session 2, rusted corrugated iron:** "a sheet of corrugated iron, painted, with rust where the paint has gone". It
    needs corrugations running one way, `metallic` 1 on bare steel and 0 on paint and rust (check's metallic and
    metal-albedo rules apply), and rust streaks. The exposed parameter is the rust amount.
  - **Session 3, a mossy flagstone path:** "a path of irregular flagstones with moss in the joints", made of two graphs
    it writes: the flagstones, and a reusable moss filter used in it as a `graph` node with an `input`. The exposed
    parameter is the moss amount, set on the `graph` node.
  - **Session 4, the fix:** `open` a planted graph copied into the round folder, a rug draft with faults that only
    `check` shows: a gradient-map stop at `#050505` that puts more than 1% of base colour below 20, a named mask clipped to
    one value, and a `transform` turned 30 degrees, which does not tile. The agent is asked to make it check clean and
    keep the design (node ids kept, apart from those it must change). Recovery is `check` clean on the saved file with
    the other nodes kept. An agent that rebuilds the graph from nothing has not recovered, and that is recorded.
- **The prompts and plants go to the person before round 1**: work each plant's numbers first (each must fire, and
  nothing in `get_graph`'s text alone should give it away), and work each prompt's numbers for clashes (M-5).
- **Evidence**, at an absolute path outside `artifacts/`: `C:\Users\andre\artifacts\carve\M-28\T-143\round-<n>\<session>\`,
  holding each session's transcript, the saved graphs, the exports, the swatch sheets and `check`'s output. A unit test in
  `tests/Patina.Server.Tests` (skipped unless `PATINA_ACCEPTANCE_RUN` is set) reads a round folder:
  - each material graph opens with no fault, checks clean, has a parameter with a variant, and its exports equal a
    fresh evaluation;
  - the iron has a metallic output;
  - the path uses the moss graph through a `graph` node with an input;
  - the fixed graph checks clean and keeps the planted ids it did not need to change.
- **Record** in this file: each round's calls, what the agent found hard, every description or product problem it met
  (expect product problems, not only description ones, M-19), and what was fixed for round 2.

## Notes

- **Decided at planning (2026-11-19, by the person):** all three materials: the woven rug, the rusted corrugated iron and
  the mossy flagstone path (corrected from the rug alone after the plan was first committed).
- Sonnet orchestrates; the inner sessions run on the default model. Plan for three agents: the material sessions, the
  fix session, then the record and finish. The four sessions are independent and may run side by side. The person looks at each round's swatch sheets before the round counts, as M-27's rounds were judged on the
  look.
- A problem the run finds in a description is fixed in this task; one in the product goes to the person, who decides
  between a fix here and a filed task.
- **Stop and ask:** before round 1 (the prompts and plants); before any change to run conditions; and before a second
  round.

### Pre-round-1 numbers (Claude Sonnet 5.5, `claude-sonnet-5-5`, orchestrating; inner sessions on the default model, expected Opus 5.5)

Awaiting the person's approval (stop 1); no session has run.

- **Run:** a Release publish of `src/Patina.Server` at `%TEMP%\carve-m28-t143\patina\`; four folders under `C:\Users\andre\artifacts\carve\M-28\T-143\round-1\` (`s1-rug`, `s2-iron`, `s3-path`, `s4-fix`), each with `prompt.txt` and an `.mcp.json` naming that patina; `run-session.ps1` there runs `claude -p --allowedTools "mcp__patina__*" --tools "" --mcp-config .mcp.json --strict-mcp-config --output-format stream-json --verbose` with the prompt on stdin and no `--model`, in the session's folder (patina inherits it as its working folder).
- **Plant** (`s4-fix/rug-draft.texgraph`, 14 nodes, size 256, neutral ids): `open` reports no fault; `check` gives ok false with exactly three problems: `base-colour-range` 1.68% of texels outside 20..245 (worst #0b0706; gradient_map `base`, first stop `#050505`, positions 0, 0.7, 1; at positions 0.45 it was under 1% and did not fire); `one-value` on the named output `wear` (every texel 1.000, levels `in_high` 0.02); `does-not-tile` on `roughness` (a seam in v, wrap step 36.6 levels against a 99th percentile of 13.2; `transform` `sheen` rotation 30). A clean fix keeping all 14 ids exists (`#3a1510`, `in_high` 0.8, rotation 90, checked clean). Note: define_graph's worked example already describes the 30-degree seam.
- **Prompt numbers:** size 512 is a power of two in export's 32..8192; paths are relative to patina's working folder, the session folder; role names match export's roles; metallic has no file of its own (it is the B of `roughness.png`) and no prompt claims one; no node type or sample is named; file names given (`rug`, `iron`, `moss`, `flagstones`, `rug-draft`, `rug-fixed`) are the names the unit test reads; variants export to `textures/rug_variant/`, `iron_rusty/`, `flagstones_mossy/`.

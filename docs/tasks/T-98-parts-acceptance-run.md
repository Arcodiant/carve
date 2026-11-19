---
description: >-
  Fresh agents build a part-tree vehicle from the tool descriptions alone, find a planted envelope fault and export it
---
# T-98 — Parts acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-97

**Verify:** viewer-cube, scenario-parts

**Perf:** skip: Carve declares no heavy suites

## Goal

A fresh agent with plain `carve` and no Carve docs finishes a part-tree model from the tool descriptions alone. It places parts, uses instances, keeps a blockout level, finds a planted fault through the tools, and exports a `.glb` that the viewer loads.

## Scope

- **The model:** a hand cart, not the rover, so that nothing in the descriptions' worked examples gives away the answer. A starting file, `cart.carve`, holds the bed, the handle and one wheel. The prompt asks the agent to add the second wheel as an instance, mark both wheels as moving, keep the current shapes as level 0, refine the bed, check it, and export it.
- **The plant**, which nothing in the prompt's numbers reveals (the M-4 and M-5 lessons): the axle part in `cart.carve` is placed 0.25 m too low, so the wheel's placed envelope leaves the cart's. Only `check`, `get_parts` or a render shows it. The agent may fix it with `define_part` without guessing intent, because the envelopes say where the axle belongs. Recovery counts if the agent understands the error, even if it does not fix it (the person's rule since M-4).
- **Work the prompt's numbers for clashes before the first round** (M-5 lesson), and record that it was done.
- **Run conditions,** as T-28 set them: `claude -p` with the default model and no `--model` flag; `--mcp-config .mcp.json --strict-mcp-config`; `--tools ""` and `--allowedTools "mcp__carve__*"`; the prompt on stdin; `--output-format stream-json --verbose` for the transcript; and a Release publish of `carve` from this commit. Ask for a final `get_parts` and `check` in the prompt.
- **Pass:** a clean `check` (no envelope warning); two wheels, one an instance of the other, both moving; level 0 kept on every part with geometry; the `.glb` loads in the viewer and through the split load with the wheel nodes sharing one mesh. Up to three rounds. A description changed between rounds goes to the person first.

## Notes

- **Sonnet.** Evidence goes to the absolute path the dispatch names, outside `artifacts/`, which is ignored (M-6 lesson).
- **Stop list, run conditions included** (M-3 lesson): any change to the prompt, the model, the flags or the starting file; any description edit; a pass judged on anything other than the criteria above.
- **Carried:** `finish` run inside the worktree, its output read whole and its `Merge task/T-98` line given; report a `verify` that waits more than a few minutes.

### As built

Run by Claude Sonnet 5.5. The inner `claude -p` session ran on `claude-opus-5-5` (the init event; no `--model` flag, the default, as the spec says). One round, and it passed; no description changed.

- **Conditions:** `claude -p --mcp-config .mcp.json --strict-mcp-config --tools "" --allowedTools "mcp__carve__*" --output-format stream-json --verbose`, prompt on stdin, from `%TEMP%\carve-m9-acceptance\round-1\`, a Release publish of `carve` from this worktree at e616e54. Evidence in `G:\My Drive\artifacts\carve\M-9\T-98\round-1\`: `prompt.txt`, `.mcp.json`, `transcript.jsonl`, `cart.carve`, `cart.glb`, `view.json`, `carve-render-1.png`, `viewer-front.png`, `viewer-angle.png`. The person approved the three frames.
- **Starting file** `cart.carve` (made through the tools by a script, not committed): `Bed` (box, envelope y 0..0.8), `Handle` (child of Bed), `Axle` (empty, child of Bed, envelope y +-0.15) and `Wheel_A` (12-sided prism, radius 0.15, child of Axle at z +0.4).
- **Plant:** `Axle` at y -0.10 instead of 0.15, so its envelope placed in `Bed` goes 0.25 m below the bed's (`get_parts` and `check` both say so). The prompt never gives a height.
- **Clash check of the prompt's numbers, done before the round:** the prompt's only numbers are z = -0.4 (and +0.4, from the file), level 0, one subdivision. -0.4 puts the second wheel at z -0.44..-0.36, clear of the bed (+-0.35) and inside the axle's envelope (+-0.5) and the bed's (+-0.55); none of 0.25, 0.15, -0.10 or any height is in the prompt, so nothing reveals the plant. The starting file's warning was confirmed (exactly the one, 0.25 m).
- **Round 1 (21 carve calls):** open, get_parts (saw the warning), get_mesh, `define_part` Wheel_B (instance_of Wheel_A, z -0.4, moves), Wheel_A moves, `keep_level 0`, `subdivide` Bed once, get_parts, check, then **`define_part Axle translation [0,0.15,0]`** (understood the error and fixed it from the envelopes), set_shading smooth on the bed (unasked, reported), renders, save, export, `render_glb`, final get_parts and check. Final `get_parts` has no warnings; final `check` has only the bed's "not planar" warnings from the subdivision (no envelope warning), which the agent explained. Export: 148 triangles, nodes `cart`, `Wheel_A`, `Wheel_B`, one material.
- **Pass criteria, all met:** clean of envelope warnings; two moving wheels, `Wheel_B` an instance of `Wheel_A`; level 0 on Bed, Handle and Wheel_A; the `.glb` loads in the viewer (148 triangles, bounds -0.6,0,-0.44 to 0.68,0.75,0.44, `--sky`, two views) and through the split load with the two wheel nodes on one mesh and no warning.
- **Checks added:** `tests/Carve.PartsAcceptance.Tests` (5 checks, in `Carve.slnx`, reads `CARVE_PARTS_ACCEPTANCE_RUN`, skipped when unset, no suite). Shown failing: on the starting `cart.carve` in the round folder the envelope, wheels and level-0 checks fail (3 of 5); on the round, 5 pass.
- **Not checked:** only one round; no `server-regression`; the viewer frames were judged by the person, not by pixels; the unasked smooth shading was not held against the pass.

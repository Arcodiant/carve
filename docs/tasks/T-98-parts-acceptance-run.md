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

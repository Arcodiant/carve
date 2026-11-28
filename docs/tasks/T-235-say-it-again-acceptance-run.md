---
description: >-
  A fresh agent builds a prop through the tools, exports it as a script, changes a number by hand and runs it again
---
# T-235 — Say it again acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-34

**Batch:** — (standalone)

**Depends on:** T-234

**Verify:** server-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

A fresh agent, given only the plain `carve` server and a short brief, builds a small prop through the tools, writes it out with `save_script`, changes a number in the script by hand, and gets the changed model with `run_script`, learning all of it from the tool descriptions and `help`.

## Scope

- **Before any run, bring these to the person and wait for their answer**: the prompt, the prop (not the crate or the gatehouse, which the descriptions use), the number to be changed and what the change should do to the model, the planted fault and what counts as recovery from it, and the run conditions below. The retrospectives' lessons on acceptance runs bind this step: work the prompt's numbers for clashes first (M-5); give no number that reveals the fault (M-2, M-5); ask for the final state in files, not in chat (M-2).
- **The planted fault, the planner's suggestion**: the requested change shifts ids (for example, one more `loop_cut` cut early in the script), so a later statement with literal ids fails at a line or lands on the wrong faces. D-8 predicts this brittleness. Recovery: the agent reads `run_script`'s line error or the render, fixes the script, not the session, and runs it again. The person decides.
- **Run conditions**, pinned in the brief: `claude -p` with the model the person names, `--tools ""`, `--mcp-config` naming only the plain `carve` (the build at the milestone tip, no extension), `--strict-mcp-config`, `--allowedTools` for `mcp__carve__*`, and the prompt on stdin. Follow T-143's and T-49's `### As built` for how their rounds were run and where the round folder was kept. The round folder is an absolute path, never only under the ignored `artifacts/`.
- **The round folder** holds what the agent wrote: `prop.carve`, `prop.carvescript`, the edited script, `prop-edited.carve`, and its renders.
- **Checks** (a new `tests/Carve.ScriptAcceptance.Tests`, `unit`, reading `CARVE_SCRIPT_ACCEPTANCE_RUN` and skipped when it is unset, a `dotnet-closure` input of `server-unit` as `tests/Carve.AcceptanceLoad.Tests` is): the agent's script run from `clear` in `ToolHarness` gives `prop.carve` byte for byte; the edited script differs from the exported one only in the statements the agent changed (a line diff, written to the round folder); the edited script gives `prop-edited.carve` byte for byte; the change did what was asked (a measurement the person agrees with the prompt: a height, a count). A copy of a good round broken three ways (a byte changed in `prop.carve`, the edited script swapped for the original, the measurement off) is shown failing, as `scripts/run-acceptance-negatives.ps1` does for T-49.
- **Findings**: the agent's transcript is read for every place it guessed, probed or was misled. A product fix that is small and in M-34's own tools goes back to the person as a proposed follow-on task; it is not fixed here.

## Notes

- A real run with a fresh agent, so it fills context fast: the run and the reading of it are this task. Expect it to find product problems (T-49 led to T-72, T-143 to T-170), and leave them for follow-on tasks the person approves.
- Read the milestone's *Decided at planning (2026-11-26)* first, and T-232 and T-233's descriptions as an agent will see them.
- **Ask, don't land.** The prompt, the fault, the model and the measurement are the person's. A check that would be loosened goes back to them.
- Run `finish` from inside the worktree and leave the folder.

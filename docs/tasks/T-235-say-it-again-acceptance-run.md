---
description: >-
  A fresh agent builds a prop through the tools, exports it as a script, changes a number by hand and runs it again
---
# T-235 — Say it again acceptance run

**Status:** Done

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

### As built

Orchestrated by Claude Opus 5.5 (`claude-opus-5-5`), in two agents, both Opus 5.5: the first ran the round, the checks and the negatives and handed off at its 60-call checkpoint; the second ran `verify`, `lint` and `finish`. The acceptance session ran on `claude-opus-5-5`, read from the transcript's init event, and was pinned with `--model`.

**Rulings (2026-11-28, through `mcp__waystone__review`).**

1. **The draft.** The person's answer was "Okay", taken as all five recommendations:
   - run condition A: `--tools "Read,Write,Edit"`. The Scope's `--tools ""` leaves the agent no way to edit a file by hand, so the file tools are allowed, with a check that every file-tool path stays in the round folder;
   - the prop: a bookshelf going from 3 to 4 compartments;
   - the prompt as drafted;
   - the measurement (below);
   - the model: Opus 5.5.
2. **Q1, the person's answer: A.** The script check allows added statements. Every exported statement is kept in order, as it was or with new values, and new statements may be added. This is the Scope's wording; the draft's "same tools in the same order" was stricter and failed round 1.
3. **Q2.** The plant was not hit, and recovery by understanding counts.
   - **The filter answered first**, not the person: A, provided the transcript shows the agent explaining why the ids shift.
   - It never explains, so the question went back to the person. **The person's answer: A**, pass anyway, because the correct ids are the evidence.

The draft and the questions are in the round folder (`draft.md`, `ruling-2.md`, `q1-*.md`, `q2-*.md`).

**Round 1.** Run with `%TEMP%\carve-m34-t235\round-1\run-session.cmd`:

```
claude -p --model claude-opus-5-5 --tools "Read,Write,Edit" --allowedTools "mcp__carve__*,Read,Write,Edit" --mcp-config .mcp.json --strict-mcp-config --output-format stream-json --verbose
```

- The prompt went on stdin, word for word as drafted.
- carve was a Release publish of `src/Carve.Server` from this worktree, in `%TEMP%\carve-m34-t235\carve\`.
- The session took 19 turns and 40 s, and never called `help`.

What the agent did:

1. Built the shelf with `define_mesh` (the box), then three closed `knife` calls on the front face, then `extrude [6, 7, 8]` at -0.25, then two `set_colour` calls. It did not use `loop_cut`.
2. Took a render, ran `check`, then used `save` and `save_script` (7 statements).
3. Made `prop-edited.carvescript` with one `Write`: new knife heights, a fourth knife, extrude `[6, 7, 8, 9]` and wall colours `10..25`, all worked out ahead.
4. Ran `clear`, then `run_script`, which was **right first time**, then a render, `check` and `save`.

**Checks** (`tests/Carve.ScriptAcceptance.Tests`, `unit`, `CARVE_SCRIPT_ACCEPTANCE_RUN`): 5 of 5 pass on round 1.

- Each script, run from `clear` in a linked `ToolHarness`, gives its `.carve` file byte for byte.
- The edited script keeps every exported statement in order (`acceptance/script.diff`).
- The measurement: 3 backs, then 4, at heights 0.34 and 0.245, with bounds (-0.4, 0, 0) to (0.4, 1.2, 0.3) (`acceptance/measure.tsv`).
- Every file-tool path (Read, Write, Edit) in `transcript.jsonl` is under the session's `cwd`, the `cwd`'s folder name is the round's, and each such file is at the same relative path in the round folder (the session ran from a copy under `%TEMP%`, so the tie is by name and by content; a session rooted at `C:\` or elsewhere fails). Found at RV-105; the first version only bounded paths by the first `cwd`.

The project is a `dotnet-closure` input of `server-unit` and is in `Carve.slnx`. `ToolHarness.cs` is linked from the server tests; `Harness.cs` supplies the two names it takes from `CarveProcess.cs`.

**Negatives** (`scripts/run-script-acceptance-negatives.ps1`, `acceptance/negatives.tsv`): all five are caught.

| Break | Fails |
|---|---|
| A byte changed in `prop.carve` | measurement, exported script byte for byte |
| The edited script swapped for the exported one | statements kept, edited byte for byte |
| The measurement off (the edited pair is the exported pair) | measurement, statements kept |
| The session's `cwd` is the folder above its own (it holds every path, under another name) | file-tool paths, by the folder-name assert |
| The file the agent wrote is not a file of the round (the transcript names `prop-elsewhere.carvescript`) | file-tool paths, by the file-exists assert |

Run on a copy of round 1 (RV-106 round 3): each broken copy sits at `%TEMP%\carve-script-acceptance-negatives\<name>ound-1`, so the name tie holds for all but the `cwd` break. The first three break more than one check, as the Fails column lists; the last two break the one path check, each at its own assert.

**Frames.** The only frames are the agent's two renders, taken out of the transcript (`render-1.png`, `render-2.png`). A subagent reviewed them: 3 and then 4 compartments, equal in height, the right colours, depth that reads, no defect. Neither frame carries a choice. This task adds no golden.

**Findings (proposed follow-on tasks for the person; none reserved):**

1. A fresh agent has never met `run_script`'s line error. The plant could be foreseen, as in M-2, M-4 and M-5. The error is covered only by T-233's tests and scenario-replay's breaks.
2. A knife-built prop cannot change its count by changing one number. The agent had to recompute every knife coordinate and add a statement. That is D-8's open variables and loops, not a fix in M-34's tools.
3. Nothing misled the agent, and it needed neither a guess nor a probe. The descriptions alone were enough; `help {"topic":"scripts"}` was never read.

**README:** nothing needed. The checks and the negatives script are not reachable by a user or an agent.

**Written outside the worktree:**

- `G:\My Drive\artifacts\carve\M-34\T-235\round-1\`: the round folder with the prompt, transcript, the four files, renders, `acceptance/` and the review files;
- `%TEMP%\carve-m34-t235\`: the publish and the run copy;
- `%TEMP%\carve-script-acceptance-negatives\`.

**Verify:** passes, with one warning: `CLAUDE.md` and `scripts/run-script-acceptance-negatives.ps1` are inputs of no suite.

**Surprising:** the agent used `knife`, not `loop_cut`, so the plant never fired; and the Scope's `--tools ""` could not have worked, since editing by hand needs the file tools (ruled A).

**Not checked:** a second round; the default model (`claude-fable-5-2`); `run_script`'s line error in a real session.

---
description: >-
  A session's calls are kept as a journal, exported as a script, and run again to give the same model
---
# M-34 — Say it again

**Status:** Done

**One-liner:** *A session's calls are kept as a journal, exported as a script, and run again to give the same model*

## What this milestone delivers

An agent builds something through the tools, as today, then writes what it did out as a script and runs that script to get the same model back. It is the way into scripts: explore with a picture at each step, then keep the steps.

- The server keeps a journal: each successful call that changed the model, with its arguments, beside the undo history's snapshot. `undo` drops the entry with its step. The journal is not capped at 100 steps.
- A tool writes the journal as a script file beside the Carve file. A session that began with `open` exports as that `open` and the steps after it.
- `run` takes a script path and replays it as one undo step. An error names the script's line and leaves the model as it was. The result is counts and bounds for each mesh, not the geometry.
- The statement format: a tool call with named arguments, the tools' own names and refusals. Nothing more yet: no variables, no loops.

Content: the gatehouse (or the crate) built through the tools, exported as a script and replayed to a file equal byte for byte, with the script committed beside the sample; tool descriptions with a worked example each.

Plans against [D-8](../design/D-8-scripts-the-call-journal-and-atomic-edits.md).

## Estimated size

About **5 tasks**:

1. The journal in `ModelStore`, with undo.
2. The script file's statement format, read and written, in the Core.
3. The export tool and `run`.
4. The replay test over every scenario's `run.json`, and the sample script with its scenario.
5. Acceptance: a fresh agent builds a prop, exports it, changes a number in the script by hand and runs it.

## Why

Raised in the discussion of 2026-11-22 (the person's idea: export the undo stack as a script, so a consumer makes a first attempt with the existing tools, dumps it and starts tweaking). Two projects, fps-arms and stone-room, wrote their `.carve` files from scripts of their own, outside Carve, where nothing was checked until the file was loaded.

The undo history cannot be exported as it is: it keeps whole-model snapshots and each step's tool name, with no arguments (`ModelStore`).

## Open at planning

- The statement syntax. D-8's *The language* is Open; this milestone needs only the part every option shares, and should not close the rest by accident.
- Does the journal survive `save` and `open`, or only the session?
- Which calls are model-changing: `save` and `export` write files and change nothing in the model.
- Paths in a script (`open`, textures): relative to the script, as texture paths are relative to the Carve file?
- Literal ids only, or a `select` recorded in place of the ids it gave (D-8, *Addressing in a script*).
- The exported script's size on a long session, and whether `define_mesh` with large lists is written inline.

**Decided at planning (2026-11-26).** Each ruling is the person's unless it says the planner's.

1. **The statement syntax**: `tool(name: <json>, ...)`, each value one JSON value written as the MCP call took it, one statement a line or spread over several, numbers kept as their raw text. No comments, variables or loops: D-8's *The language* stays Open. Agreed sketch:
   ```
   open(path: "slab.carve")
   loop_cut(mesh: "wall", edge: 12, cuts: 2)
   extrude(mesh: "wall", faces: [5, 9], distance: 0.3)
   set_colour(mesh: "wall", faces: [40, 41], colour: "#6b4a2f")
   ```
2. **`run_script` acts on the current model**, and a new **`clear`** tool empties it as one undo step. A script that starts with `clear()` or `open(...)` rebuilds the model whole.
3. **Exported scripts are the journal as called**: a fresh session's script has no `clear()`; one after a `clear` or a whole-model `open` starts with it.
4. **`clear` and a whole-model `open` start a new journal**; undo of either restores the one before.
5. **The journal is session only**: never in the Carve file, untouched by `save`.
6. **Which calls are journalled**: the planner's rule, not ruled separately: a call is journalled when it made an undo step, so `save`, `export`, `bake`, the looks and the queries never are. T-231 writes it down.
7. **Tool names**: `save_script` and `run_script`; files `.carvescript` (`eol=lf` in `samples/.gitattributes`).
8. **Paths are relative to the script**: `save_script` rewrites each file a call read (`open`, `define_target`, `define_material`'s textures, `displace`'s graph) relative to the script's folder and refuses one with no relative path; `run_script` resolves against the script's folder.
9. **Literal ids only**, as D-8's *Addressing in a script* already says; recording the `select` an id list came from is later.
10. **Large lists inline, wrapped**: a long statement one argument a line and one vertex or face a line, as the Carve file is; no size cap.
11. **A `run_script` is journalled as its statements, inline**; a `run_script` inside a script is refused (one script calling another stays open in D-8).
12. **The replay test is a scenario of its own**, `scenario-replay` (T-234), on small models built for it, not a replay of other scenarios' sessions, after the person asked whether replaying every scenario would be expensive. The agreed outline is in T-234.
13. **M-34 runs after M-23**, so its tools are born with `verbosity`, under M-23's description test, with the format's long text in `help {topic: "scripts"}`, and `run_script`'s warnings folded as M-23 folds them. The six existing descriptions over Claude Code's 4,096 cut are M-23's to fix (its decision 7); nothing new is filed.
14. **Pins**: a tools/list pin or exact tool-list check may be re-taken when the only change is the new tools (and `undo`'s text); any other change goes back to the person.
15. **Content**: the gatehouse. Its scenario's session is committed as `samples/gatehouse.carvescript` (T-232) and replays after `clear` to `samples/gatehouse.carve` byte for byte (T-233). Each new tool's description has a worked example run as written.

No frame is drawn by this milestone beyond the replay scenario's before-and-after pair, which must be equal, and it adds no sound or motion, so there is no layout to sketch and no listening pass.

### Order

| Wave | Tasks | Why |
|---|---|---|
| 1 | T-230, T-231 | side by side: the format in `Carve.Core` only, the journal and `clear` in `Carve.Server` only. T-231 waits on M-23's last task (T-206) |
| 2 | T-232 | writes the journal in the format; the gatehouse script |
| 3 | T-233 | reads and runs it; shares `save_script`'s path rule, registration and help topic |
| 4 | T-234 | the real run over every model-changing tool |
| 5 | T-235 | the fresh agent, last, on everything |

T-230, T-231 and T-233 are design-heavy and decide what others build on (a format, the journal, the batch and in-process runner M-35 builds on); T-232 is mechanical on them; T-234 and T-235 are real runs. Expect T-235 to find product problems: they come back as follow-on tasks the person approves.

## Required tasks

- [ ] [T-230](../tasks/T-230-the-script-statement-format-read-and-written-in-the-core.md) — Carve.Core reads and writes scripts: one tool call a statement, named arguments with JSON values, bit-exact numbers, errors that name the line
- [ ] [T-231](../tasks/T-231-the-call-journal-in-modelstore-and-clear.md) — carve keeps every call that changed the model, with its arguments, beside the undo history; undo drops it, open and the new clear tool start it again
- [ ] [T-232](../tasks/T-232-save-script-writes-the-session-as-a-script.md) — save_script writes the session's journal as a .carvescript, paths relative to the script, and the gatehouse scenario's session is committed as samples/gatehouse.carvescript
- [ ] [T-233](../tasks/T-233-run-script-replays-a-script-as-one-undo-step.md) — run_script runs a script on the current model as one undo step, names the line of an error and leaves the model as it was, and answers counts and bounds for each mesh
- [ ] [T-234](../tasks/T-234-the-replay-scenario.md) — scenario-replay calls every model-changing tool on small models of its own, saves, replays the exported script and compares the files byte for byte, and shows planted breaks caught
- [ ] [T-235](../tasks/T-235-say-it-again-acceptance-run.md) — A fresh agent builds a prop through the tools, exports it as a script, changes a number by hand and runs it again

## Retrospective

Written 2026-11-28, before the person's go. The rebase onto `main`, the refactor pass and the review are still to come.

**As planned.**

- **The claim holds end to end.** An agent builds through the tools, `save_script` writes the session, and `run_script` replays it to the same model:
  - `samples/gatehouse.carvescript` (14 statements, 42 lines) replays after `clear` to `samples/gatehouse.carve` byte for byte. The unit tests and scenario-gatehouse on the real `carve` both check it.
  - scenario-replay (T-234) calls all 32 model-changing tools. The file saved after replay equals the one saved before it, and the two front renders are byte-equal.
  - A fresh Opus 5.5 agent (T-235) took a bookshelf from 3 to 4 compartments by editing its script. It needed no `help` call and made no guess or probe.
- **The members, in the planned waves, with no rework between them:**
  - **T-230**: `Carve.Core.Scripts` (reader, writer, statement, error). Numbers are copied as raw text, never through `double`. D-8 gains *The statement format* (Settled for M-34, Rule).
  - **T-231**: the journal is recorded by `MeshToolSupport.Create`'s wrapper; no tool changed. `clear` is new. `ModelStore.Change` takes several entries for one step.
  - **T-232**: `save_script`, the path rule, `help {"topic":"scripts"}`, the gatehouse sample.
  - **T-233**: `run_script` as one undo step under `ModelStore.Batch`. A failure puts back the model, undo history, journal and version. `RunScriptTool.RunStatements` is M-35's seam.
  - **T-234**: scenario-replay. Its coverage list fails on a tool in neither of its two lists, and its five breaks were all caught.
  - **T-235**: `tests/Carve.ScriptAcceptance.Tests`, with three negatives all caught.
- **Pins:** only the tool count was re-taken, 45 to 48: `clear`, `save_script`, `run_script`. The help-topic messages were re-taken for `scripts`. `undo`'s text gained the one sentence the plan allowed.
- **`cairn milestone M-34 verify`** (2026-11-28): every check passes but `review`, which comes after the go. 6 of 6 members are done and merged. 32 suites passed, 29 ran and 3 carried, every scenario, `server-regression` and `patina-regression` among them.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-230 | Opus 5.5 | 1 |
  | T-231 | Opus 5.5 | 1 |
  | T-232 | Sonnet 5.5 | 1 |
  | T-233 | Opus 5.5 | 1 |
  | T-234 | Sonnet 5.5 | 1 |
  | T-235 | Opus 5.5 | 2 (a hand-off at 60 calls, the round done) |

  No rework was traced to the Sonnet members.
- No engine need was recorded. Only frame review was done: subagents reviewed the replay pair and T-235's two renders, and none of the frames carried a choice for the person.

**Not as planned.**

- **T-235's run conditions changed.** The Scope's `--tools ""` left the agent no way to edit a script. The person agreed to `--tools "Read,Write,Edit"`, with a check that every path stays in the round folder.
- **T-235's planted fault was not hit.** The agent worked the shifted ids out ahead and its edited script ran first time. So no fresh agent has met `run_script`'s line error. The filter first answered "pass if the transcript explains the shift"; the transcript does not, and the person ruled pass anyway, on the correct ids.
- **The script check was loosened to the Scope's wording** (added statements allowed), by the person's ruling A. The draft's stricter "same tools in the same order" had failed round 1.
- **`1e308` cannot appear in a script**, because every tool takes floats and refuses it. scenario-replay uses `1e38`. The format itself keeps it bit-exact (T-230's tests).
- **Rules the members settled themselves** under the Notes' defaults:
  - a trailing comma and a newline before `(` are refused;
  - argument names follow the tool-name rule;
  - the width is 120;
  - an empty script is one undo step that changes nothing.

**Never verified.**

- Only the gatehouse scenario and the T-235 round have driven `run_script` through a real client.
- No test runs two calls against a batch at the same time; that the lock keeps them apart is argued.
- No test writes a graph path (`normal_texture`, `displace`) or a `define_target` file relative to a script and back. They share the texture's route, which is tested.
- T-232 did not compare the gatehouse frames' hashes with a baseline run. It argues they are taken before `save_script`, and scenario-gatehouse passed.
- T-234 has no measured time for scenario-gatehouse: its wrapper failed with `-NoBuild`, not looked into. scenario-replay ran in 20 s on the runners, scenario-gatehouse in 8 s in this verify.
- Only one T-235 round was run, on Opus 5.5, not on the default model.
- A surrogate pair counts as two columns in an error, and a lone surrogate escape would throw in the writer (T-230).

**Known and left.**

- T-235's two findings, proposed as follow-on tasks for the person and not reserved:
  1. a round where the agent meets `run_script`'s line error;
  2. a count-changing edit needs every coordinate worked again, which is D-8's open variables and loops, for M-35.
- `run_script` refuses `render` and `render_glb` as statements, and its runner throws if a statement tool does not finish at once. A statement tool that ever became async would need the runner changed.
- `CLAUDE.md`, `samples/.gitattributes` and `scripts/run-script-acceptance-negatives.ps1` are inputs of no suite (verify's `uncovered` warning).
- `finish` could not delete the worktree folders `branches/task/T-231` to `T-235`, each its agent's shell directory. Git no longer tracks them, and they are left to delete by hand.

**For the next plan.**

- Give an acceptance plant that the agent cannot foresee from the descriptions. M-2, M-4, M-5 and now M-34 show a careful agent avoids the obvious one. Make the fault one that only a run reveals, or plant it in the files rather than in the request.
- An acceptance brief that edits a file needs the file tools, and says so at planning, not at the run.
- A subagent that runs `finish` from inside its worktree cannot let it delete that worktree. Brief it to `cd` out first, or have cairn step out itself.

---
description: >-
  A session's calls are kept as a journal, exported as a script, and run again to give the same model
---
# M-34 — Say it again

**Status:** Active

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

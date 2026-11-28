---
description: >-
  save_script writes the session's journal as a .carvescript, paths relative to the script, and the gatehouse scenario's session is committed as samples/gatehouse.carvescript
---
# T-232 — save_script writes the session as a script

**Status:** In Progress

**Pri:** P1

**Milestone:** M-34

**Batch:** — (standalone)

**Depends on:** T-230, T-231

**Verify:** server-unit, server-regression, scenario-gatehouse

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent calls `save_script {path}` and gets a `.carvescript` file holding what this session did to the model, one statement per journalled call, that a person or an agent can read and edit. The gatehouse, built through the tools by its scenario, is committed as that script.

## Scope

- **`save_script`**, a new tool: `path` (required, `.carvescript`, resolved and written by `ToolPaths` as `save` writes, refusing what `save` refuses), and M-23's `verbosity`. It writes T-231's journal through T-230's `ScriptWriter`, one statement per entry, in order. It changes nothing in the model and is not journalled. An empty journal writes an empty file and says so in the result. The result: the path, the number of statements, and the number of lines.
- **What the script holds** (the person): the journal as called. A fresh session's script does not start with `clear()`; a session that called `clear` or a whole-model `open` starts with that statement. A `run_script` (T-233) appears as its statements, inline.
- **Paths** (the person: relative to the script): each file argument T-231 declared is written as the path from the script's folder to the resolved file, with `/` separators, as `save` writes texture paths. A file with no relative path from the script's folder (another drive) refuses the whole save, naming the statement's tool, its position in the journal and the argument, as `save` names the material and field. The file is not written then.
- **Literal ids** (D-8, *Addressing in a script*): ids are written as the calls gave them. Recording the `select` an id list came from is a later refinement, not this task.
- **The description**: what it writes, the path rule, that ids are literal and an earlier edit's change of topology shifts them, that `undo` and `clear` shape what is written, and a worked example (a cube, one `extrude`, `save_script`, and the file it writes, quoted) run as written by a test. Under M-23's description ceiling; the statement format in full goes in a new `help` topic, `scripts` (T-199's `help`), which this task adds and T-233 extends: the grammar, the path rule, literal ids, what is journalled.
- **`samples/.gitattributes`**: `*.carvescript text eol=lf`.
- **The sample** (the person: content is the gatehouse): the gatehouse scenario calls `save_script` into its run folder once the wall is built and before its own `save` and reopen (a reopen would start a new journal), and compares the file byte for byte with a committed `samples/gatehouse.carvescript`. Write that sample from the scenario's first run, read it, and say in `### As built` how many statements and lines it has and whether it reads as something a person could edit. Add the sample to the scenario-gatehouse suite's inputs in `tests.json`. Update the Gatehouse and `samples/` rows of `CLAUDE.md`'s layout table.
- **Tests** (`unit`, `ToolHarness`): the worked example; a session of every family of edit written and read back by `ScriptReader` to the journal's entries; awkward numbers written as sent; `open` of a file in a sibling folder written as `../models/x.carve`; a texture path written relative to a script in a subfolder; another drive refused (skip with a reason where the machine has one drive, and say so); an empty journal; `undo` and `clear` reflected; the path refusals `save` has. Break table in `### As built`.
- **`README.md`**: `save_script` and the sample script, with the worked call run as written.

## Notes

- Mechanical on T-230 and T-231's APIs; the path rule is the one decision, and it is the person's.
- Reads T-230's and T-231's `### As built` for the APIs. If either does not give what this needs, stop and ask rather than reach into it.
- **Pins** (the person): a tools/list pin or exact tool-list check may be re-taken when the only change is `save_script`; say which in `### As built`.
- scenario-gatehouse is a real run; frames do not change (the scenario's frames are taken before `save_script`); say that you checked they are unchanged rather than looking at each.
- Read the milestone's *Decided at planning (2026-11-26)* first.
- **Ask, don't land.** A rule neither this file nor the milestone settles, or a check that would be loosened, goes back to the person. Search `tests/` and `scenarios/` for what reads what you change before changing it. Show each new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

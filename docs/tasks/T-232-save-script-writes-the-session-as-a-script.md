---
description: >-
  save_script writes the session's journal as a .carvescript, paths relative to the script, and the gatehouse scenario's session is committed as samples/gatehouse.carvescript
---
# T-232 — save_script writes the session as a script

**Status:** Done

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

### As built

Built by one agent on Sonnet 5.5 (`claude-sonnet-5-5`), with no hand-off.

- **`save_script`** (`SaveScriptTool.cs`, registered after `clear`): `path` (`.carvescript`, `ToolPaths.Resolve` and `ToolPaths.Write` as `save` uses them) and `verbosity`. It turns each `ModelStore.Journal` entry into a `ScriptStatement`, with each declared file (`JournalEntry.Files`) written as `Path.GetRelativePath(script folder, file)` with `/`, and writes them with `ScriptWriter`. A file argument that is an object field (`normal_texture.graph`) is rebuilt with only that string replaced (other properties via `JsonElement.WriteTo`, so numbers stay raw). It is not journalled (no step). Result: `saved <file> to <path>: N statements, M lines`; an empty journal says `no statements (the session has made no step), an empty file`. Lines are LF.
- **Path rule**: a file with no relative path (another drive) refuses the whole save before anything is written: `save_script: statement 1 (open), argument path: the file '...' has no path relative to '...' (it is on another drive); ...`.
- **`help {"topic":"scripts"}`** (`ScriptsTopic.cs`): grammar, what is written, path rule, literal ids, the worked example. T-233 extends it.
- **Sample**: `samples/gatehouse.carvescript` is 14 statements, 42 lines (define_mesh, loop_cut, extrude, 3 merge_faces, 2 plane_cut, 3 knife, set_colour, delete, bridge). It reads as editable: one call a line, the slab's vertices and faces one a line as the Carve file has them, the knives' long point lists wrapped one point a line only past 120 characters. The ids are literal, so a person editing it works from the end (the topic says so). Written from the scenario's first run, then compared byte for byte on every run (`SaveScriptAsync` in `GatehouseScenario.cs`, before its own save and reopen). Added to scenario-gatehouse's inputs in `tests.json`; `samples/.gitattributes` has `*.carvescript text eol=lf`; the Gatehouse and `samples/` rows of `CLAUDE.md` and `README.md` updated (worked call, the sample).
- **Pins re-taken**: `ToolHarnessTests` tool count 46 to 47 (only `save_script`); `HelpToolTests`' three "the topics are warnings" messages now end `warnings, scripts` (only the new topic); `VerbosityToolTests.Tools` gained a `save_script` row and `JournalToolTests.NoStep` names it.
- **Frames**: the scenario takes every frame before `save_script`, and the call only reads the journal; I did not compare frame hashes against a baseline run, and no frame was looked at.
- **Tests** (`SaveScriptToolTests`, `unit`, 9): the worked example run as written and equal to the description's and topic's text; a 15-call session across the edit families read back by `ScriptReader` to the journal's tools, argument names and values; awkward numbers; `open` of `models/x.carve` as `../models/x.carve`; a texture relative to a script two folders down; another drive refused and nothing written (ran here: this machine has C: and D:; it skips with a reason on one drive); empty journal; undo and clear reflected, save_script not journalled; the path refusals of `save`, silent and replaced.

**Break table** (each planted alone in `SaveScriptTool.cs`, `SaveScriptToolTests` run, then reverted):

| Planted fault | Fails |
|---|---|
| File written as the absolute path | sibling-folder open, texture subfolder |
| Rooted relative path not refused | another drive |
| Statements written in reverse | every-family read-back, undo/clear, worked example |
| Only the first journal entry written | 5 tests |
| Line count taken as the statement count | worked example |

**Not checked**: a `define_material` graph slot object (`normal_texture.graph`) or `displace`'s `source.graph` written relative end to end (the code path is shared with the texture string and covered only by that); frames compared by hash.

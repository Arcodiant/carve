---
description: >-
  carve keeps every call that changed the model, with its arguments, beside the undo history; undo drops it, open and the new clear tool start it again
---
# T-231 — The call journal in ModelStore, and clear

**Status:** Done

**Pri:** P1

**Milestone:** M-34

**Batch:** — (standalone)

**Depends on:** T-206

**Verify:** server-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

The server keeps, for the session, every successful call that changed the model with its arguments as sent, so T-232 can write it out as a script. `undo` takes the entry back with its step, and a new `clear` tool empties the model.

## Scope

- **What is journalled** (the person, 2026-11-26): a call is journalled when, and only when, it made an undo step, that is when it pushed onto `ModelStore`'s history. So `save`, `export`, `bake`, `render`, `render_glb`, `select`, `check`, `help` and the `get_*` tools never are, a refused or failed call never is, and a new model-changing tool is journalled with no change here. Assert that every tool call pushes at most one step (a call that pushed two is a bug: fail loudly in a test, not silently).
- **An entry** is the tool name and its arguments as the MCP call sent them: the `JsonElement` of each argument, in the order sent, raw (numbers kept as their text). Also, for each argument that names a file the tool reads, the absolute path the tool resolved it to (T-232 writes it relative to the script). Those arguments today: `open`'s `path`, `define_target`'s file argument, `define_material`'s texture paths (and graph slots, if M-29's T-160 has landed), `displace`'s graph path (T-223). Find them by `ToolPaths.Resolve(..., verb: "reads")` in the model-changing tools rather than from this list; declare them once per tool, and add a `unit` table test that names every model-changing tool's file-reading arguments, so a new one is a deliberate row.
- **The mechanism**: the arguments reach the store without editing every tool. The planner's suggestion: `MeshToolSupport.Create`'s wrapper sets the current call (tool, arguments) for the duration of the call, and `PushLocked` records it. Another shape is fine if it keeps the tools unchanged; write it down in `### As built`.
- **The journal's life** (the person): it lives in memory, for the session; `save` leaves it alone and it is never written to the Carve file. A whole-model `open` starts a new journal whose only entry is that `open`. `open` of one part (`OpenPiece`) is an ordinary entry. `clear` starts a new journal whose only entry is `clear()`. `undo` restores the journal as it was before the step it takes back, so undo of an `open` or a `clear` brings the earlier journal back. A fresh process has an empty journal.
- **Not capped at 100**: the snapshots stay capped at `UndoDepth`; the journal keeps every entry since the last `open` or `clear`. A session of 105 moves has 105 entries; after one undo, 104.
- **Reading it**: one method on `ModelStore` returning the current entries (immutable, safe to read while another call writes), for T-232. No tool shows the journal (D-8's open question about a journal tool stays open).
- **`clear`**, a new tool: takes no arguments except M-23's `verbosity`; empties the model (meshes, parts, materials, skeleton, clips: `Model.Empty`) as one undo step, and answers as `undo` does for an empty model (`{meshes: []}`; `{"ok":true}` at `silent`). Clearing an empty model is still a step (say so in the description). Its description has a worked example run as written by a test, is under M-23's description ceiling, and says that `clear` restarts the journal save_script writes. Register it in `CarveServer.AddTools`.
- **`undo`'s description**: add `clear` to the tools it takes back, and one sentence that undo also takes the step out of the journal `save_script` writes. Keep it under the ceiling.
- **Tests** (`unit`, `ToolHarness`): one entry per model-changing tool's worked example and none for each read-only one (a theory over tools/list, so a new tool lands in one column or the other); a refused call adds none; undo drops the last; undo with nothing to undo leaves the journal; `open` resets and undo of it restores; `clear` resets and undo of it restores; `OpenPiece` appends; 105 moves give 105 entries; arguments come back in the order and raw text sent (`0.30000000000000004`, `-0`); a file argument's resolved path is absolute and right for a relative path against `ToolFolder`. Break table in `### As built`.
- **`README.md`**: `clear` in the tool list, and a line that carve keeps the session's calls for `save_script` (T-232 adds the rest).

## Notes

- **Decides an architecture others build on**: T-232 reads the journal and T-233 batches it (a `run_script` pushes one step whose journal entries are the script's statements, inline: the person). Leave a seam for T-233: the store must be able to append several entries for one step. Write the API in `### As built`.
- **Runs after M-23** (the person: M-34 after M-23). `clear` adopts M-23's `verbosity` helper (T-200) and falls under its description test (T-197/T-203 to T-205) and `help` (T-199). Re-read what M-23 landed before starting; if any of it is not as its plan says, stop and ask.
- Runs beside T-230, which works only in `Carve.Core`: do not touch `src/Carve.Core` or D-8.
- **Pins** (the person): a tools/list pin or exact tool-list check may be re-taken when the only change is the new tool (and undo's text); say which in `### As built`. Any other change to tools/list goes back to the person. Search `tests/` and `scenarios/` for every exact tool list (`ListToolsAsync`, tool-name arrays) before landing; scenario-patina's list is patina's and does not change.
- `ToolHarnessMatchesProcessTests` and `ToolFamiliesThroughProcessTests` compare the harness with the real process, hence `server-regression`.
- Read the milestone's *Decided at planning (2026-11-26)* first.
- **Ask, don't land.** A rule neither this file nor the milestone settles, or a check that would be loosened, goes back to the person. Show each new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by one agent on Opus 5.5 (`claude-opus-5-5`), with no hand-off.

**The mechanism** is the planner's shape. `MeshToolSupport.Create<T>(name, description, schema, params string[] reads)` returns a `JournalledTool` (a `DelegatingMcpServerTool`). For the length of each call, it sets an `AsyncLocal` `JournalCall`: the tool, its arguments, and the arguments it reads. `ModelStore.PushLocked` journals that call. `ToolPaths.Resolve(..., verb: "reads")` notes each path it resolves on the call. No tool's `Run` changed.

**The API, for T-232 and T-233** (`src/Carve.Server/Journal.cs`, `ModelStore.cs`):

- `ModelStore.Journal`: an `IReadOnlyList<JournalEntry>`. It is an `ImmutableList` published with `Volatile`, so it can be read from any thread while a call writes.
- `JournalEntry(string Tool, IReadOnlyList<KeyValuePair<string, JsonElement>> Arguments, IReadOnlyDictionary<string, string> Files)`:
  - `Arguments` are in the order sent. Each is a cloned raw `JsonElement`, so `GetRawText()` gives the text sent (`0.30000000000000004`, `-0`), and `verbosity` is kept like any other argument.
  - `Files` maps where an argument is (`path`, `file`, `normal_texture`, `normal_texture.graph`, `source.graph`) to the absolute path `Resolve` gave for it.
- `ModelStore.Clear(tool)`: `Model.Empty` as one step, which starts a new journal.
- **The seam for several entries in one step**: `ModelStore.Change(tool, change, IReadOnlyList<JournalEntry> entries, bool restart, out problem)`. It pushes one step and journals `entries` in place of the call under way. They are appended, or start a new journal when `restart` is true. One undo takes them all back. Each history entry keeps the journal as it stood after its step, and undo puts back the one before.
- **Declared file arguments**: open `path`; define_target `file`; define_material each of its five texture fields and `<field>.graph`; displace `source.graph`. `JournalCall.Entry()` throws when a call resolved a read path that no declared argument holds, so a new file-reading argument has to be declared.
- **At most one push per call**: a second `PushLocked` in one call throws `InvalidOperationException`, and the call comes back as a tool error.
- Outside a tool call (tests that drive the store directly), a push journals nothing.

**clear** (`ClearTool.cs`, registered after `undo`):
- Its worked example is `{}` on the unit cube, and the clear test runs it as written.
- `undo` names `clear` and has one more sentence: undo also takes the step out of the list of calls `save_script` writes. Both descriptions pass `HelpToolTests`' 3,686 ceiling.
- `README.md` adds `clear` to the verbosity list and has a paragraph on the session's calls.

**Pins re-taken**: one. `ToolHarnessTests.ListsEveryToolWithAClosedSchema`'s count went from 45 to 46, and the only change is `clear`. Searching `tests/` and `scenarios/` found no other exact tool list, and scenario-patina's list is untouched. `VerbosityToolTests.Tools` gained a `clear` row, and its guard puts `clear` among the tools that take `verbosity`.

**Tests**: `JournalToolTests` (`unit`).
- The per-tool check is a `[Fact]` over tools/list, not an xUnit theory, because the list is only known once a harness is running. Each reader is called and must add no entry. Every other tool runs `VerbosityToolTests.Tools`' working call for it after that row's setup:
  - save, check, export and bake must add nothing;
  - undo must drop the last entry;
  - open and clear must leave exactly their own call;
  - every other tool must add its own call, exactly as sent;
  - then that row's refused call must leave the journal unchanged;
  - a tool in neither column fails the test.
- Those rows are the calls M-23's verbosity test already runs, not each description's worked example. For unwrap, which has no row there, the row is one face of the cube.

**Break table**: each break was applied to the committed code, `JournalToolTests` and `TargetToolTests` were run, and the break was reverted.

| Break | Fails |
|---|---|
| A. undo does not restore the journal | every-tool, undo, open/clear-restore, 105 moves |
| B. open and clear append instead of restarting | every-tool, open/clear, clear, file path |
| C. arguments sorted by name instead of order sent | order and raw text, every-tool, undo |
| D. define_target's `file` not declared | the reads table, and both of TargetToolTests' file tests (the undeclared-read throw) |
| E. clear pushes twice | every-tool, clear, open/clear (the double-push throw) |
| H. journal capped at UndoDepth+1 | 105 moves |
| I. Resolve records the path as given, not the absolute one | file path |
| J. arguments re-serialised, not raw | order and raw text, and every test that compares calls |

**Not checked**: the description's worked example for each model-changing tool, one by one (the rows stand in for them, as above); a real MCP client calling `clear`, beyond `server-regression`'s harness-against-process comparisons; texture or graph `Files` entries (they are covered only by the undeclared-read throw, which the existing material and displace tests would set off).

**M-23 as landed**: it differs from its first plan (T-203 merged into T-199, T-204 and T-205 dropped), but the person ruled those changes and they are recorded in M-23. Nothing T-231 relies on differs from what was ruled.

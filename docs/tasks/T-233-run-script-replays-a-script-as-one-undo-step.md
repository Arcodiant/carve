---
description: >-
  run_script runs a script on the current model as one undo step, names the line of an error and leaves the model as it was, and answers counts and bounds for each mesh
---
# T-233 — run_script replays a script as one undo step

**Status:** Done

**Pri:** P1

**Milestone:** M-34

**Batch:** — (standalone)

**Depends on:** T-232

**Verify:** server-unit, server-regression, scenario-gatehouse

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent calls `run_script {path}` and the script's statements run as tool calls on the current model, all or nothing, as one undo step. `samples/gatehouse.carvescript` run after `clear` gives `samples/gatehouse.carve` byte for byte.

## Scope

- **`run_script`**, a new tool: `path` (required, `.carvescript`, read by `ToolPaths` with `verb: "reads"`) and M-23's `verbosity`. It reads the file with T-230's `ScriptReader`; a format error is a tool error naming the line and column, and nothing runs.
- **On the current model** (the person, 2026-11-26): the script acts on whatever the model is; a script that starts with `clear()` or `open(...)` rebuilds it whole. Statements run in order, each as the tool itself runs it: the same `McpServerTool` instances, the same argument checks and refusals. No second implementation of any tool.
- **Paths** (the person): a file argument (T-231's declared ones) that is relative resolves against the script's folder, not carve's working directory.
- **Refused statements**: only a model-changing tool may be a statement. `run_script` itself (D-8 leaves one script calling another open), `save`, `save_script`, `export`, `bake`, `render`, `render_glb`, `undo`, `select`, `check`, `help` and the `get_*` tools are refused before anything runs, naming the line: `line 7: save is not a statement a script can run: it changes nothing in the model`. Decide the list as T-231 decides journalling (a tool that never pushes a step is refused) only if that can be known before running; otherwise keep an explicit list beside T-231's table and test it against tools/list, so a new tool is a deliberate row.
- **All or nothing**: when a statement's tool refuses or fails, `run_script` is a tool error that names the line, the tool and the tool's own message unchanged (`line 12: extrude: faces[0]: no face 40; the mesh has faces 0 to 39`), and the model, the undo history, the journal and `ModelStore.Current.Version` are exactly as before the call. On success the whole run is **one** undo step (`undo` answers `undone: "run_script"`) and one published version; a render on the frame thread never sees a half-run model. This needs a batch in `ModelStore` (T-231 left a seam); it runs under the store's write lock or an equivalent that keeps another call from interleaving, and say which in `### As built`.
- **The journal** (the person: inline): a successful run appends the script's statements as entries, one each, with their paths resolved; a script whose statement is `clear()` or a whole-model `open` restarts the journal there, as the call itself would. So `save_script` after `run_script` writes the statements, not a `run_script` line. A failed run appends nothing.
- **The result**: for each mesh, its name, vertices, faces, edges, closed and bounds, with its warnings folded as M-23 folds them; never the geometry. Also the number of statements run. At `silent`, `{"ok":true}`.
- **The description**: what it does, on the current model, the path rule, all or nothing, one undo step, what a statement may be, and a worked example (a two-statement script file, quoted, and its result) run as written by a test (the file copied beside the test). Extend `help`'s `scripts` topic (T-232) with the running rules. Under M-23's description ceiling.
- **The sample replays** (content): a `unit` test runs `clear` then `run_script samples/gatehouse.carvescript` in `ToolHarness`, saves, and compares the file byte for byte with `samples/gatehouse.carve`; the gatehouse scenario does the same on the real `carve` after building, in its run folder. If the committed `samples/gatehouse.carve` is not byte-equal to what `save` writes for the model (it is compared by content today), stop and ask before re-taking it. `samples/gatehouse.*` among server-unit's inputs in `tests.json`.
- **Tests** (`unit`, `ToolHarness`): the worked example; the gatehouse; a run on a non-empty model adds to it; an error at the first, a middle and the last statement each leaves model, history, journal and version unchanged and names its line; a refused statement kind, each; a format error; a relative texture path from a script in a subfolder; `undo` after a run takes back all of it in one step; `save_script` after `run_script` and more edits writes the script's statements then the edits, and running that from `clear` gives the same file byte for byte; the journal after a failed run. Break table in `### As built`.
- **`README.md`**: `run_script`, with the worked call run as written, and the explore-then-keep-the-steps loop in a few lines.

## Notes

- **Decides an architecture** (the batch in `ModelStore`, and how a tool is called in-process outside an MCP request) and is design-heavy. M-35 builds its language on this runner: write the API in `### As built`.
- Reads T-230, T-231 and T-232's `### As built`.
- How a tool is invoked in-process with a different file base (the script's folder) is open to the implementer. Do not special-case tools. If it needs a change to how tools take `ToolFolder`, keep it to one place.
- **Pins** (the person): a tools/list pin or exact tool-list check may be re-taken when the only change is `run_script` (and `help`'s topic); say which in `### As built`.
- Read the milestone's *Decided at planning (2026-11-26)* first.
- **Ask, don't land.** A rule neither this file nor the milestone settles, or a check that would be loosened, goes back to the person. Search `tests/` and `scenarios/` for what reads what you change before changing it. Show each new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by one agent on Opus 5.5 (`claude-opus-5-5`), with no hand-off.

**The batch** (`ModelStore.Batch(string tool, Func<string?> run, out string? problem)`): it holds the store's **write lock** for the whole run, so no other call interleaves; the statement tools take the same lock again (it is re-entrant on the thread). The store now keeps its working state (`_current`, `_journal`) apart from what it publishes (`_published`, `_publishedJournal`). An `AsyncLocal` marks the run's flow: inside it, `Current` and `Journal` are the working state, so each statement sees the one before; every other flow (the frame thread, another call) reads the published state, which a batch does not touch until it ends. Each statement pushes its step as usual. At the end, history, model and journal are put back as they were (a copy of the history, at most 101 entries), and then on success the final model is pushed as **one** step of `run_script`, journalled with the run's entries in place (`restart: true` with the whole working journal, so a `clear()` or whole-model `open` among the statements restarts it where it stood). One undo step, one published version. On a problem or an exception nothing is pushed.

**The runner, for M-35** (`RunScriptTool.RunStatements(IServiceProvider services, McpServer server, JsonRpcRequest request, string folder, CarveScript script, out int ran) → string?`): it first checks every statement against `RunScriptTool.Refused` and the served tools, then invokes each through the served `McpServerTool` (`services.GetServices<McpServerTool>()`, the same `JournalledTool` instances as a direct call, so the same checks, refusals and journalling), with a `RequestContext` built on the outer call's server and request. It returns null when every statement succeeded, otherwise the first failure as `line N: <tool's own message>` (prefixed with `tool: ` when the message does not already start with it). It must run inside `ModelStore.Batch` to be all or nothing. **The file base** is changed in one place: the statements' `Services` is a wrapper that answers `ToolFolder` with the script's folder and delegates everything else, so every tool that takes `ToolFolder` resolves against the script without any tool changing. Each argument is re-serialised compact (raw numbers kept, relaxed escaping), so the journal entry reads as a direct call's. Every statement tool is synchronous; the runner throws if one is not (render and render_glb, the async ones, are refused).

**Refused statements**: an explicit table (`RunScriptTool.Refused`), since whether a tool pushes is only known by running it: `run_script` (one script does not run another), `undo` (a run is taken back by undo after it), and `save`, `save_script`, `export`, `bake`, `render`, `render_glb`, `select`, `check`, `help` and the five `get_` tools (`it changes nothing in the model`). A test holds it equal to `VerbosityToolTests.Readers` + `JournalToolTests.NoStep` + undo + run_script and to tools/list, so a new tool is a deliberate row. An unknown tool is `line N: carve has no tool 'x'`. Both are refused before anything runs. An empty script is a run of nothing, still one step, as clear on an empty model is.

**Errors**: a format error is `run_script: <file>: line L, column C: <reader's message>; nothing ran`; a failed statement is `line N: ...` then `; nothing of <file> was kept, the model is as before the call`.

**Result**: `{"statements":N,"meshes":[{name, vertices, faces, edges, closed, bounds, warnings}]}` (warnings folded by `Report` at the given level), `{"ok":true}` at silent. The worked example (a two-statement slab file) is run as written by `RunScriptToolTests`, which also checks the description, the scripts topic and README quote it.

**Content**: `clear` then `run_script samples/gatehouse.carvescript` in `ToolHarness`, then `save`, is byte for byte `samples/gatehouse.carve` (it was already byte-equal, so nothing was re-taken). The gatehouse scenario does the same last, after its export, running the script it wrote in its run folder and saving `replayed.carve`; it takes no frame there, so no frame changed and no capture review was needed. `samples/gatehouse.*` and `README.md` added to server-unit's inputs.

**Pins re-taken**: `ToolHarnessTests` tool count 47 to 48 (only `run_script`). `VerbosityToolTests.Tools` gained a `run_script` row, and `JournalToolTests`' every-tool test a `run_script` case (it gets the script's statement, not its own call). `JournalToolTests.NoStep` became `internal` so the refused-list test can read it. `help`'s `scripts` topic gained one paragraph on running; `undo`'s text is unchanged.

**README**: `run_script` in the verbosity list, a paragraph with the worked call and its result, the gatehouse replay line, and the explore-then-keep-the-steps loop.

**Tests** (`RunScriptToolTests`, 12, `unit`; and one in `ModelStoreTests`): the worked example and silent; the gatehouse; a run on a non-empty model adds to it and undo takes all of it back in one step; a failing statement at line 1, 2 and 3 leaves model, version, journal and history, and the next call builds on the old model at the next version; each refused kind and an unknown tool, by line; line 7 `save` with the exact message; the refused table against tools/list; a format error; a relative texture from a script in a subfolder (and the same call made directly refused); save_script after a run and a move writes the statements then the move, and running that from clear saves the same file byte for byte; a clear in a script restarts the journal; the batch seen from a thread outside the flow, and a failed batch.

**Break table** (each planted alone, `RunScriptToolTests` and the batch test run, then reverted):

| Planted fault | Fails |
|---|---|
| A. A batch's working model and journal not put back | failing statement at lines 2 and 3, undo of a run, the batch test |
| B. Steps inside a batch published to other flows | the batch test, failing statement at lines 2 and 3 |
| C. Statements given the server's services, not the script folder's | relative texture |
| D. The refused table not checked | each refused kind |
| E. The run's entries appended to the journal instead of put in place | undo of a run, clear in a script restarts |

The first version of the failing-statement test did not catch A (the published state was right; only the working state was wrong); it now makes a call after the failed run, which does.

**Not checked**: a real MCP client calling `run_script` beyond the gatehouse scenario; two calls truly at once against a running batch (the lock is argued, not raced in a test); a `define_material` graph slot or `displace` graph resolved from a script's folder (the same `ToolFolder` path as the texture).

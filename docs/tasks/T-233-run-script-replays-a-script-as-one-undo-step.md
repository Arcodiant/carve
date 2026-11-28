---
description: >-
  run_script runs a script on the current model as one undo step, names the line of an error and leaves the model as it was, and answers counts and bounds for each mesh
---
# T-233 — run_script replays a script as one undo step

**Status:** In Progress

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

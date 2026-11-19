---
description: >-
  define_part, get_parts and keep_level build and read a part tree, the edit tools act on a named part, and check warns when a part leaves its envelope
---
# T-93 — Part tools

**Status:** Done

**Pri:** P1

**Milestone:** M-9

**Batch:** — (standalone)

**Depends on:** T-91

**Verify:** core-unit, server-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent builds and reads a part tree through MCP: it places parts, marks what moves, declares envelopes, makes instances and keeps levels. Every edit tool acts on a named part, as the person ruled at M-9's planning (2026-11-17).

## Scope

- **`define_part`** `{part, parent?, translation?, rotation?, scale?, envelope?, instance_of?, moves?}`. A new name makes a part: an empty, or an instance when `instance_of` is given. An existing name changes only the fields given, and the rest are kept. `parent: null` makes the part top-level, and `envelope: null` clears the envelope. Every T-91 refusal is a tool error in the tool's words, naming the argument. One undo step.
- **`define_mesh mesh=X`:** a new name is a top-level part at the origin, as today. An existing empty gets geometry and keeps its placement. An existing part with geometry has it replaced, keeping placement and levels. An instance is refused, naming its source.
- **`keep_level`** `{level, parts?}`: keeps each named part's live geometry as level N, or every part's that has geometry when `parts` is left out. It refuses an instance or an empty by name, and an N below a part's newest kept level. An equal N replaces it. One undo step. The result lists, per part, the levels it now holds.
- **`get_parts`**: the tree in pre-order. Per part: `name`, `parent`, `translation`, `rotation`, `scale`, `envelope`, `instance_of`, `moves`, whether it has geometry, its vertex and face counts, `levels`, local bounds, world bounds, and its envelope warnings.
- **Every edit tool** takes `mesh` as today, naming a part with geometry. An instance or an empty is refused with T-91's text. Coordinates in edits and in `select` are in the part's own local space, and descriptions say so once, in words shared across tools.
- **`check`** adds T-91's envelope warnings, after today's. A model with no envelopes gives exactly today's output.
- **Content:** each new tool's description has a worked example on the rover: `define_part {part: 'Wheel_FR', parent: 'Axle_F', translation: [0, 0, -0.72], rotation: [0, 1, 0, 0], instance_of: 'Wheel_FL', moves: true}`, `keep_level {level: 0}`, and a `get_parts` result line. `define_mesh`'s and `check`'s descriptions gain one sentence each.
- **Tests (`unit`, in-process through M-20's harness):** the rover's tree built through the tools from `RoverSpec`'s numbers, with `get_parts` matching `RoverSpec` field for field; every refusal; undo of each new tool; an edit on `Wheel_FL` seen in `get_parts`' world bounds of all four wheels; `check` on the rover with `Axle_F` lowered 0.3 m giving exactly T-91's warning; `tools/list` schemas for the three new tools.

## Notes

- **Sets a public API** (tool names, arguments and texts that agents learn): Opus.
- **File ownership in its wave:** a new `PartTools.cs`, `DefineMeshTool.cs`, `CheckTool.cs`, and the tool list in `Program.cs`. `ModelStore.cs` is shared with T-92 for adds only. An edit tool's file is touched only where its description needs the shared sentence, and adds only.
- **Open rules: stop and ask.** No part removal or renaming in M-9: undo removes a part just made. If the work shows a need, it goes to the person. Also: whether `define_part` on an existing part may change `instance_of`, and anything the rulings leave open.
- **Write down for dependents (As built):** the tool texts and the `get_parts` result shape, for T-94, T-96 and T-98.
- **Carried:** `tools/list` unchanged for every existing tool apart from the agreed description sentences; each new check shown failing on a broken product, as a table; evidence at the absolute path the dispatch names; `finish` run inside the worktree, its output read whole and its `Merge task/T-93` line given; report a `verify` that waits more than a few minutes.

### As built

Opus 5.5, 2026-11-18 (two agents, both Opus 5.5). For T-94, T-96 and T-98.

- **Files.** `src/Carve.Server/PartTools.cs` (new): `PartTexts` (the agreed sentences, public), `DefinePartTool`, `GetPartsTool`, `KeepLevelTool`. Registered in `CarveServer.AddTools`, after `define_mesh` (`Program.cs` has no tool list of its own; it calls `AddTools`). `ModelStore.Change(tool, model => (next, problem), out problem)`, added: one step under the write lock. `MeshToolSupport.PartSpaceText` is `PartTexts.PartSpace`.
- **Texts.** Each tool's text is the `Text` constant of its class; the worked examples are `DefinePartTool.Example`, `KeepLevelTool.Example` (with its exact `Output:` line) and `GetPartsTool.ExampleEntry`, all checked against real calls on the rover in `PartToolsTests`.
- **`define_part {part, parent?, translation?, rotation?, scale?, envelope?, instance_of?, moves?}`** returns `{created, part}`, `part` being the get_parts entry. Arguments are read raw, so `parent: null` (top-level) and `envelope: null` (clear) differ from leaving them out. `envelope` is `{min:[x,y,z], max:[x,y,z]}`. A new name with no `instance_of` is an empty. Core's field `name` is reported as `part`; every other field is the argument's own name. **The person's ruling (2026-11-18):** an existing part's `instance_of` may not change: giving a different one is refused (`instance_of: 'Cab' already exists with geometry of its own, and an existing part's instance_of does not change; ...`); giving the same one is accepted.
- **`get_parts {}`** returns `{parts:[...]}` in pre-order, `[]` for an empty model. Entry, in this key order: `name, parent, translation, rotation [x,y,z,w], scale, envelope ({min,max} or null), instance_of, moves, geometry (owns geometry), vertices, faces (of the geometry shown: an instance's source's, 0 for an empty), levels (numbers shown, an instance's source's), local_bounds, world_bounds ({min,max} of the live vertices, null for an empty), warnings (that part's `EnvelopeWarning.Message`s)`. world_bounds are each live vertex through `Model.WorldTransform`, in float, so the example reads `0.29999998`.
- **`keep_level {level, parts?}`** returns `{level, kept:[{part, levels}]}` in tree order. With `parts` left out it keeps every part that owns geometry; an empty model is the usual "there is no mesh yet". Core's `part:` refusals are re-prefixed `parts[i]:`; every refusal in one call is listed; the whole call is refused if any part is.
- **`define_mesh`** gains `PartTexts.DefineMesh` and refuses an instance: `name: 'Wheel_FR' is an instance of 'Wheel_FL', with no geometry of its own; define_mesh name=Wheel_FL changes the geometry every instance shows`. An empty or a part with geometry already behaved as specified through T-91's `Model.With`.
- **Edit tools and select** gain `PartTexts.PartSpace` after their "Needs a mesh" sentence: bridge, define_target, delete, extrude, knife, loop_cut, merge_faces, move, plane_cut, select, subdivide. Targeting was already T-91's `Model.Target`; meshes are stored part-local, so edits were already in part space.
- **`check`** appends `Model.EnvelopeWarnings()` (the whole model's, whichever mesh is checked, since an empty such as `Axle_F` cannot be checked itself) after its own warnings, and `ok` is false when there are any. With no envelopes the output is as before.
- **`tools/list`**: `PartToolsListTests` pins a SHA-256 of every pre-T-93 tool's entry with the three agreed sentences removed, taken on the code before this member: first on a2b8878, then re-taken on `origin/milestone/M-9` at 6e2626f without T-93, after T-92 changed save's and open's texts. It passes, so nothing else changed. Since the digest takes the agreed sentences out, `Each_agreed_sentence_is_in_its_tools_description_once` pins each one in its tool's text, once.
- **Tests:** `PartToolsTests` (unit, on `ToolHarness`, `RoverSpec.cs` linked into `Carve.Server.Tests`) and `PartToolsListTests`.
- **Core's empty-part refusal, the person's ruling (2026-11-18):** T-91's text now names define_mesh's real argument: `mesh: 'Axle_F' is an empty part with no geometry; define_mesh name=Axle_F gives it some` (was `mesh=`). define_mesh's schema is unchanged. Pinned in `PartTreeTests`, `PartTreeFileTests` and `PartPieceToolTests`.
- **Rebase onto T-92:** one conflict, the test project's linked files (T-92's `PartTreeSample.cs`, T-93's `RoverSpec.cs`); both kept.
- **Break table:** `G:\My Drive\artifacts\carve\M-9\T-93\break-table.tsv`, twelve breaks of the product, each made, tested with `PartTools|PartPiece` in `Carve.Server.Tests`, and reverted. Every break fails at least one check: world bounds left in part space, get_parts out of pre-order, define_part resetting an unnamed field, an instance_of change allowed, a refusal not naming its argument, keep_level taking instances and empties, kept parts out of tree order, check dropping envelope warnings, define_mesh accepting an instance, a missing part-space sentence, a changed existing text, and Core's refusal back to `mesh=` (that one fails `PartPieceToolTests` in the server tests, and Core's own tests besides).
- **Not checked:** no real MCP client run; the checks are in-process through `ToolHarness`. `verify` is slow (over 10 minutes for server-unit).

---
description: >-
  define_part, get_parts and keep_level build and read a part tree, the edit tools act on a named part, and check warns when a part leaves its envelope
---
# T-93 — Part tools

**Status:** In Progress

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

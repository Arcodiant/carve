---
description: >-
  An agent selects vertices, edges or faces with select and a small where-expression, and gets ids the edit tools take
---
# T-22 — Select tool and query language

**Status:** In Progress

**Pri:** P1

**Milestone:** M-4

**Batch:** — (standalone)

**Depends on:** T-21

**Verify:** core-unit, server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent asks "all faces above y = 10" or "all unattached vertices" with one call and gets back ids in the shape `move` and `extrude` take, so a selection feeds an edit with no retyping. The server holds no selection.

## Scope

**Core: `src/Carve.Core/MeshQuery.cs`** (new; engine-free, no `System.Random`, no clock):

- `MeshQuery.Parse(string where)` gives a `QueryResult`-style value: a parsed expression or a list of `MeshError`s with element `where`. `MeshQuery.Select(PolygonMesh mesh, ElementKind kind, expression)` gives the matching ids: `int` ids for vertices and faces, `(Lo, Hi)` pairs for edges, ascending.
- **The grammar** (keywords and names in any case; whitespace free):

  ```text
  expr       := or
  or         := and ( 'or' and )*
  and        := not ( 'and' not )*
  not        := 'not' not | atom
  atom       := '(' expr ')' | 'all' | 'none' | flag | comparison
  comparison := property op number
  op         := '<' | '<=' | '==' | '!=' | '>=' | '>'
  property   := name | name '.' ('x' | 'y' | 'z')
  number     := a decimal, with an optional sign and exponent
  ```

  `not` binds tighter than `and`, which binds tighter than `or`.
- **Properties and flags, by kind.** `x`, `y`, `z` are the element's representative point: a vertex's position, an edge's midpoint, a face's centre (`PolygonMesh.FaceCentre`). `min.x`/`max.x` (and y, z) are the extent of the element's own vertices (for a vertex, equal to its position).

  | Kind | Numbers | Flags |
  |---|---|---|
  | vertices | `id`, `x`, `y`, `z`, `min.*`, `max.*`, `faces` (faces using it), `edges` (edges at it) | `unattached` (`faces == 0`), `boundary` (on an open edge) |
  | edges | `x`, `y`, `z`, `min.*`, `max.*`, `a`, `b` (the lower and higher vertex id), `length`, `faces` (faces using it) | `open` (`faces == 1`), `manifold` (exactly two, opposite), `nonmanifold` (`faces > 2`) |
  | faces | `id`, `x`, `y`, `z`, `min.*`, `max.*`, `normal.x`, `normal.y`, `normal.z` (unit, Newell), `vertices` (corner count), `area` | `non_planar` (the `MeshReport` rule), `open` (has an open edge) |

  Comparisons are made **in float**, both sides, as B-1 decided for `target`: the literal is narrowed to `float` first, so `y == 1.02` matches a vertex stored as `1.02f`.
- **Errors** are `MeshError`s with element `where` and a one-based character position: `at character 11: '>>' is not an operator; use <, <=, ==, !=, >= or >`; `at character 1: 'centre' is not a property of faces; faces have id, x, y, z, min, max, normal, vertices, area, non_planar, open`; `at character 8: 'normal.w' is not an axis; use .x, .y or .z`; `at character 5: a flag cannot be compared with a number`; `at character 14: expected a number`; `at character 20: expected ')'`; `where is empty`. Every structural error that can be found is listed, as `define_mesh` does.

**Server: `src/Carve.Server/SelectTool.cs`** (new), registered in `Program.cs`'s `WithTools` list, closed schema:

- `select(kind, where)`: `kind` is `vertices`, `edges` or `faces`; `where` is the expression. Both required.
- Result JSON, ids ascending: `{"kind":"faces","faces":[3,12,18],"count":3,"of":70}`; for vertices `"vertices":[...]`; for edges `"edges":[[5,9],[9,13]]` (each `[lo, hi]`, ordered by lo then hi). `of` is the number of elements of that kind. A query matching nothing is a success with `count` 0, not an error.
- Tool errors, the mesh untouched: no mesh yet (the usual wording); an unknown kind; a missing or non-string `where`; every parse error above, prefixed `where: `; an argument select does not take.
- **The description, as a priority** (D-1: the text is the interface): the grammar as above, the property table in words, that `x/y/z` is the vertex, the midpoint or the centre, that comparisons are in float, that the result's lists are exactly what `move` takes (`vertices`, `edges`, `faces`) and `extrude` takes (`faces`), that ids never change so a selection stays valid until an edit removes or reshapes what it names, and every error. Worked examples (each on `ExampleMarker` lines, parsed by the existing test):
  1. on the unit cube: `{"kind":"faces","where":"normal.y > 0.9"}` → `{"kind":"faces","faces":[3],"count":1,"of":6}`;
  2. `{"kind":"vertices","where":"y == 1 and x < 0.5"}` → the two top vertices on x = 0;
  3. `{"kind":"edges","where":"open"}` → `count` 0 on the closed cube;
  4. a select-then-move chain: select the top face, then `move {"faces":[3],"by":[0,0.5,0]}` with the ids from the result, written as two calls.

**Tests:**

- `tests/Carve.Core.Tests/MeshQueryTests.cs`, `unit`: every production, precedence (`a or b and c`, `not a and b`), parentheses, keywords in upper case, every error with its character position, every property and flag of every kind on the cube, the L-block, the crate (`TestMeshes.Crate()`) and an open box; float comparison (`y == 1.02` on a `1.02f` vertex matches; `y == 1.0199999` does not); `all`, `none`; the result lists are ascending.
- `tests/Carve.Server.Tests/SelectToolTests.cs`, `regression` (starts `carve`): the worked examples run as written and give the written results; every tool error above; the select-then-move chain through the client, where `move`'s `moved` equals the selection; `tools/list` lists `select` with a closed schema; `select` leaves the version and the undo stack unchanged.
- **Show each new check failing** on a broken product, as a table: precedence swapped, `not` dropped, comparison in double, edges unordered, a flag accepted in a comparison, `of` counting the wrong kind.

## Notes

- **Decided by the person at planning (2026-11-05):** one `select` tool with a small expression string, not structured filters and not a tool per question; selections are returned as id lists in the edit tools' shape, and the server holds none (T-11's decision stands).
- **Decided at planning on the planner's recommendation:** the grammar, the property names and the error wording above; comparisons in float. A name or flag not listed here is not added without asking.
- **Design-heavy: the grammar is a public interface agents learn.** Run it on the stronger model.
- **Open rules: stop and ask.** Adding a property, flag or operator not in the table. Any approximate comparison (`near`, a tolerance). Holding a selection on the server. Any change to `move` or `extrude`.
- **Writes down for T-27 and T-28:** the final grammar and property table, the result shape and the exact error wording, in an *As built* section.
- **Files owned in this wave:** `MeshQuery.cs`, `SelectTool.cs`, their tests. `Program.cs` is touched on one line (adding the tool to `WithTools`); T-23 adds its own line there, and whoever lands second keeps both. Nothing in `MeshToolSupport.cs`, `TestMeshes.cs` or the render files: helpers this task needs go in its own files.
- Run `finish` from inside the worktree and leave the folder.

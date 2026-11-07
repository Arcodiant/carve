---
description: >-
  An agent selects vertices, edges or faces with select and a small where-expression, and gets ids the edit tools take
---
# T-22 — Select tool and query language

**Status:** Done

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

### As built

Implemented on Opus 5.5 (model id `claude-opus-5-5`). `src/Carve.Core/MeshQuery.cs`, `src/Carve.Server/SelectTool.cs`, one entry in `Program.cs`'s `WithTools`; 161 tests in `MeshQueryTests`, 44 in `SelectToolTests`. `move`, `extrude`, `MeshReport.cs`, `MeshToolSupport.cs` and `TestMeshes.cs` are untouched. M-4's refactor pass moved `SelectTool`'s required-string reader into `MeshToolSupport.RequiredString`, which `PathArguments` now uses too; the error texts are unchanged.

**For T-27 and T-28: the interface.** The grammar and the property table are the Scope's, with nothing added. The result is `{"kind":K,K:[...],"count":n,"of":m}`, key order as written, `K` being `vertices`, `edges` or `faces`; edges are `[lo, hi]` by lo then hi. A refused call's text is `select failed with N problem(s); the current mesh is unchanged:` then one problem per line; no mesh is `select: there is no mesh yet; call define_mesh first, or open to read a saved .carve file`.

| Error | Wording |
|---|---|
| kind | `kind: required but missing`, `kind: expected a string, got 3`, `kind: 'bones' is not a kind; use vertices, edges or faces` (exact case: `FACES` is refused) |
| where argument | `where: required but missing`, `where: expected a string, got 5` |
| unknown argument | `limit: not an argument of select; its arguments are kind, where` |
| empty | `where: where is empty` (blank text too) |
| operator | `where: at character 11: '>>' is not an operator; use <, <=, ==, !=, >= or >` |
| name | `where: at character 1: 'centre' is not a property of faces; faces have id, x, y, z, min, max, normal, vertices, area, non_planar, open` |
| name lists | vertices `id, x, y, z, min, max, faces, edges, unattached, boundary`; edges `x, y, z, min, max, a, b, length, faces, open, manifold, nonmanifold` |
| axis | `where: at character 8: 'normal.w' is not an axis; use .x, .y or .z` |
| flag compared | `where: at character 5: a flag cannot be compared with a number` |
| number | `where: at character 14: expected a number` |
| bracket | `where: at character 20: expected ')'` |
| *added:* no axis | `where: at character 1: 'min' needs an axis; use .x, .y or .z` |
| *added:* bare number | `where: at character 1: 'area' is a number; compare it with <, <=, ==, !=, >= or >` |
| *added:* range | `where: at character 5: 1e99 is out of range: a number must lie within ±3.40282347E+38` |
| *added:* atom | `where: at character 10: expected a property, a flag, all, none, not or '('` |
| *added:* trailing | `where: at character 7: expected 'and', 'or' or the end of the expression` |
| *added:* depth | `where: at character 201: nested too deeply; 'not' and '(' go 200 levels deep at most` |

**Readings the spec left open, for the person to confirm.** None is on the stop list; each is small to change.

- **The six added errors.** The grammar has inputs the Scope's list does not name, so these wordings are mine. An axis on a name with none reuses the property wording (`'id.x' is not a property of faces`).
- **The depth limit of 200 is a rule the spec does not have.** The parser recurses, and a stack overflow ends the process with no way to catch it, so a long run of `(` could otherwise take the server down. A chain of `or` or `and` is not nesting and has no limit.
- **A literal beyond float range is refused**, in `define_mesh`'s wording, rather than narrowed to infinity. One too small narrows to 0, so `y == 1e-99` matches y = 0.
- **Positions.** Each points at the thing at fault. The Scope's `at character 8: 'normal.w'` fits the axis letter, so that is the axis error's position. `at character 5: a flag...` fits `not open > 1` with the position at the flag, not the operator; `open>1` would also fit it, so this one is a choice.
- **`Parse` has no kind but the name error needs one.** `Parse(where)` stays as specified and holds the grammar errors. `QueryResult.ErrorsFor(kind)` adds the name errors, in one list by position. So `Parse("centre > 1")` succeeds alone, and `Select` throws `ArgumentException` if given an expression the kind cannot run; the tool always asks `ErrorsFor` first.
- **Whitespace free stops at words:** `y==1and x<0.5` reads, but `y==1andx<0.5` is the name `andx`. The description says so.

**Notes.** `non_planar` asks `MeshReport` for its warnings rather than repeating the rule, so the two cannot drift and `MeshReport.cs` stays unchanged. Midpoints, lengths and areas are worked in double and narrowed once. Literals go through double to float, as `define_mesh` does. The description's worked results are on `Result: ` lines after their `Example: ` lines; the fifth example is the chain's `move` call.

**Not fully as the Scope asked.** "`select` leaves the version unchanged" is not observed directly: the version is not on the wire and the test assembly does not compile the tools. It rests on undo through the client (a write moves undo) plus a source check that `SelectTool.cs` uses only `store.Current`. The `select` writes the store row below shows both failing.

**Each check failing on a broken product.** One break at a time, built, then both new classes run; an unbroken control first gave 0 failures of 161 and of 44. Full results are in `artifacts/review/T-22/round-1/breaks2.txt` under the milestone worktree, with `breaks.py` beside it.

| The break | Failed in Core / Server | The checks that failed, among others |
|---|---|---|
| precedence swapped | 2 / 0 | `Not_binds_tighter_than_and_which_binds_tighter_than_or` |
| `not` dropped | 13 / 0 | `Every_production_selects_what_it_says`, `Not_binds_tighter_than_and_which_binds_tighter_than_or` |
| comparison in double | 5 / 1 | `A_literal_is_narrowed_to_float_before_it_is_compared`, `A_computed_property_is_narrowed_to_float_before_it_is_compared` |
| edges unordered | 5 / 2 | `The_result_lists_are_ascending_and_of_counts_the_kind`, `A_result_is_the_list_named_for_its_kind_with_count_and_of` |
| a flag accepted in a comparison | 4 / 1 | `An_error_gives_its_character_and_says_what_is_wrong`, `A_bad_call_is_a_tool_error_and_the_mesh_is_untouched` |
| `of` counting the wrong kind | 4 / 6 | `The_result_lists_are_ascending_and_of_counts_the_kind`, `The_worked_examples_run_as_written_and_give_the_written_results` |
| `boundary` counts non-manifold edges | 1 / 0 | `A_vertex_on_a_nonmanifold_edge_alone_is_not_boundary` |
| `manifold` is any two faces | 1 / 0 | `Two_faces_running_the_same_way_along_an_edge_are_neither_manifold_nor_nonmanifold_nor_open` |
| only the first error kept | 2 / 0 | `Every_error_that_can_be_found_is_listed_in_order_of_position` |
| names case-sensitive | 5 / 0 | `Keywords_and_names_are_in_any_case` |
| edge midpoint is the low end | 3 / 2 | `Edges_on_the_cube`, `What_select_returns_is_what_move_takes` |
| no nesting limit | 2 / 1 | `Text_nested_past_any_stack_is_a_tool_error_and_the_server_stays_up` |
| `select` writes the store | 0 / 2 | `Select_leaves_undo_as_it_was`, `Select_reads_the_store_and_never_writes_it` |
| a worked result written wrong | 0 / 1 | `The_worked_examples_run_as_written_and_give_the_written_results` |
| an unknown kind taken as faces | 0 / 3 | `A_bad_call_is_a_tool_error_and_the_mesh_is_untouched`, `Every_problem_of_a_call_is_listed_at_once` |

Two caveats on that table. The `no nesting limit` row was made by raising `MaxDepth`, and its Core tests build their text from `MaxDepth`, so those two likely failed on that rather than on overflow; whether the server test failed on a crash or on the message was not separated. A sixteenth break, nudging the literal by one bit in double before narrowing, passed everything: it almost never changes the float, so it is not a break, and `comparison in double` is the real check of that rule.

A first run of this table reported 0 failures on every row, because the runner read only stdout and xunit's `[FAIL]` lines are on stderr. That run was discarded, not used.

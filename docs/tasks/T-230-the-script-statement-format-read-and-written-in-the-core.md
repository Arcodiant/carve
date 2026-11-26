---
description: >-
  Carve.Core reads and writes scripts: one tool call a statement, named arguments with JSON values, bit-exact numbers, errors that name the line
---
# T-230 — The script statement format, read and written, in the Core

**Status:** Open

**Pri:** P1

**Milestone:** M-34

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

`Carve.Core` can read a `.carvescript` into a list of statements, each a tool name with named JSON arguments and the line it starts on, and write such a list back as text that reads back to the same statements, every number to the bit.

## Scope

- **The format** (the person, 2026-11-26): a statement is `tool(name: <json>, name: <json>, ...)`. The tool name is `[a-z_][a-z0-9_]*`. Each value is one JSON value (RFC 8259: number, string, true, false, null, array, object), written exactly as the MCP call took it. A tool with no arguments is `tool()`. Whitespace, newlines included, is allowed anywhere between tokens, so one statement may span lines. Statements are separated by a newline. Nothing else: no comments, no variables, no `let`, no loops, no expressions (D-8, *The language*, stays Open; see Notes).
  ```
  define_mesh(
    name: "wall",
    vertices: [
      [0, 0, 0],
      [4, 0, 0],
      ...
    ],
    faces: [
      [0, 3, 2, 1],
      ...
    ]
  )
  loop_cut(mesh: "wall", edge: 12, cuts: 2)
  extrude(mesh: "wall", faces: [5, 9], distance: 0.3)
  ```
- **Types**, in a new `Carve.Core.Scripts` namespace (one file each): `ScriptStatement` (tool, the arguments in the order written as `(string Name, JsonElement Value)` pairs, the 1-based line the statement starts on), `CarveScript` (the statements), `ScriptReader` (`Read(string text) → CarveScript` or a `ScriptError`), `ScriptWriter` (`Write(CarveScript) → string`), `ScriptError` (line, column, message). `System.Text.Json` is the BCL's and allowed in Core; no package.
- **Reading.** Errors name the line and column and say what was expected, e.g. `line 4, column 18: expected ':' after the argument name 'faces'`. The cases: an unknown character, a name that is not an identifier, a missing `(`, `:`, `,` or `)`, a value that is not JSON (the JSON reader's own message, with the line and column mapped back to the script's), a repeated argument name in one statement, two statements on one line, text after the last `)`. An empty file or one of only whitespace is a script of no statements. A UTF-8 BOM is skipped. CRLF and LF both read; the column counts characters, not bytes.
- **Writing** (the person: inline, wrapped): LF line ends, a final newline. A statement whose one-line form is 120 characters or less is written on one line, `tool(a: 1, b: [1, 2])`, with `, ` and `: ` as the separators and JSON values in their compact form with `, ` between items. Longer, it is written one argument a line indented two spaces, and a list argument whose items are themselves lists or objects one item a line indented four spaces, as the Carve file writes vertices and faces. Strings are written with `System.Text.Json`'s escaping, non-ASCII left as it is (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` or the Carve file's own choice, whichever `CarveFile` uses; match it).
- **Numbers are never reformatted**: the writer copies each number token's raw text (`JsonElement.GetRawText()`), so `0.1`, `0.30000000000000004`, `-0`, `5e-324`, `1E308` and `3.0` come back as written and parse to the same double. Say in `### As built` how the wrapped writer keeps raw tokens (it must not round-trip through `double`).
- **Tests** (`unit`, `tests/Carve.Core.Tests/Scripts/`):
  - Round trip: `Read(Write(s))` equals `s` statement by statement and argument by argument (names, order, raw value text after normalising JSON whitespace), over a table: no arguments, every JSON kind, nested lists, a 2,000-vertex `define_mesh`, unicode and escaped strings (`"é"`, `"é"`, `"\n"`, `"\""`), the awkward numbers above, an argument named like a JSON keyword (`null: 1`).
  - `Write(Read(t)) == t` for text already in the canonical form; and for a hand-written form (odd spacing, CRLF, a statement over three lines) the canonical text the writer gives, pinned.
  - Every error case above, each with its exact message, line and column. Line numbers of a statement after a 40-line `define_mesh` are right.
  - A break table in `### As built`: each check shown failing on a planted fault (the writer reformatting a number through `double`, the reader taking the line of the statement's last line, an off-by-one column).
- **D-8**: add a decision, *The statement format* (Certainty Settled for M-34's statements, Strictness Rule), stating the grammar above in a few lines and that M-35 decides everything around it. Link it from *The language*'s working assumption.

## Notes

- **Decides a format others build on**, and design-heavy: T-232 writes and T-233 reads with these types, and M-35 extends the grammar. Write the public API (type and member names, the error type) in `### As built` for them.
- Runs beside T-231, which works only in `Carve.Server`: do not touch `src/Carve.Server`, `README.md` or `CarveServer.cs`.
- The reader knows no tool names: whether `loop_cut` exists or takes `edge` is the tool's business when T-233 runs it. Do not add a tool list to Core.
- The 120-character width and the four-space item indent are the planner's choice; a different width is fine if the Carve file already has one, and say which you took. Anything else the grammar does not settle (a trailing comma, a statement split before its `(`) goes back to the person, not into the code; the default is to refuse it with a clear message.
- **Do not close D-8's open language question by accident**: no comment syntax, no keywords. The person decided "nothing more yet".
- Read the milestone's *Decided at planning (2026-11-26)* first.
- **Ask, don't land.** A rule neither this file nor the milestone settles, or a check that would be loosened, goes back to the person. Search `tests/` and `scenarios/` for what reads what you change before changing it. Show each new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

---
description: >-
  Carve.Core reads and writes scripts: one tool call a statement, named arguments with JSON values, bit-exact numbers, errors that name the line
---
# T-230 — The script statement format, read and written, in the Core

**Status:** Done

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

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`), 2026-11-27. Core only: `src/Carve.Core/Scripts/`, `tests/Carve.Core.Tests/Scripts/`, D-8. Nothing reachable changed, so `README.md` needed nothing; T-232 and T-233 bring the tools and their README lines.

**Public API** (`Carve.Core.Scripts`, for T-232 and T-233):

- `record ScriptStatement(string Tool, IReadOnlyList<(string Name, JsonElement Value)> Arguments, int Line = 0)`: `Line` is the 1-based line of the tool name, 0 for a statement not read from text (one T-232 builds from the journal).
- `record CarveScript(IReadOnlyList<ScriptStatement> Statements)`.
- `record ScriptError(int Line, int Column, string Message)`; `ToString()` is `line 4, column 18: expected ':' after the argument name 'faces'`. Column in UTF-16 chars, 1-based.
- `ScriptReader.Read(string text) → (CarveScript? Script, ScriptError? Error)`: exactly one is null; stops at the first error. `ScriptReader.IsName(string)` is the name rule `[a-z_][a-z0-9_]*`, used for tool and argument names alike.
- `ScriptWriter.Write(CarveScript) → string` and `ScriptWriter.Write(ScriptStatement) → string` (one statement, no line end; for an error that quotes a statement). `ScriptWriter.Width = 120`. Throws `ArgumentException` for a tool or argument name that is not a name, or an argument given twice, since that text would not read back.

**Reading.** A hand-written parser over the text's UTF-8 bytes (every grammar token is ASCII); each value is handed to `Utf8JsonReader` with `AllowMultipleValues` and `JsonElement.ParseValue`, which stops at the end of one value. A number or literal is first cut to its run of letters, digits, `+`, `-` and `.`, because the JSON reader refuses a `)` straight after a number. A JSON error keeps the reader's own message (its `LineNumber | BytePositionInLine` tail cut, line ends in the quoted text flattened to spaces) and its position, mapped back to the script's line and character column. The messages, every one pinned in `ScriptReaderTests.Errors`: `unknown character '#': expected …`, `'Loop_cut' is not a tool name: a name is lower-case letters, digits and '_', and does not start with a digit`, `expected '(' after the tool name 'x'`, `expected ':' after the argument name 'x'`, `expected ',' or ')' after the value of 'x'` (`, found the end of the file` at the end), `expected a JSON value for 'x'`, `the value of 'x' is not JSON: <reader's message>`, `the argument 'x' is given twice in 'tool'`, `a second statement on the line of 'tool': statements are separated by a newline`, `expected a newline after the ')' that ends 'tool'`.

**What the grammar did not settle, refused** (the Notes' default; not asked, since refusing is the stated default and either can be loosened later without breaking a script): a trailing comma, `expected an argument name after ',' in 'f': a trailing comma is not allowed`; a newline between the tool name and its `(`, `expected '(' on the line of the tool name 'f': a statement may not break before its '('` (spaces there are allowed, as between any tokens). Argument names follow the tool-name rule; the Scope's "a name that is not an identifier" covers both, and D-8's decision says so.

**Writing.** Width 120 (the Carve file has no width of its own; its items are one a line). A list argument is wrapped one item a line when any item is a list or an object (so a vertex list with `null` tombstones wraps, as the Carve file writes it); any other value stays compact on the argument's line however long. **Raw tokens:** `Compact` walks the `JsonElement` and writes a number, `true`, `false` and `null` as `GetRawText()`, never through `double`; strings are re-escaped with `JsonSerializer.Serialize(s, UnsafeRelaxedJsonEscaping)`, the Carve file's `StringOptions`, so `"é"` is written `"é"` (same characters, so it reads back equal; only a number's text is kept byte for byte). The wrapped form uses the same `Compact` for each item, so it keeps raw tokens the same way.

**Checks** (47 tests, unit): round trip over no arguments, every JSON kind, nested lists, unicode and escaped strings, the awkward numbers (`0.1`, `0.30000000000000004`, `-0`, `5e-324`, `1E308`, `3.0`, `-1.5e+10`, a 30-digit integer) by raw text and by double bits, `null: 1`; a 2,000-vertex `define_mesh` one vertex a line; canonical text written back as it was; a hand-written form (BOM, odd spacing, CRLF, a statement over three lines) pinned to its canonical text; 120 characters on one line, 121 wrapped; every error case with message, line and column; a statement after a 40-line `define_mesh` on line 41.

**Break table** (each fault planted alone, the Scripts tests run, then reverted):

| Planted fault | Fails |
|---|---|
| The writer formats a number through `double` (`GetDouble().ToString("R")`) | 3: the awkward-numbers round trip, `Numbers_come_back_as_written_and_to_the_same_double`, the 2,000-vertex mesh (`0e-3` became `0`) |
| The reader gives a statement the line of its last line | 2: the 40-line `define_mesh` (42 for 41) and the CRLF spacing test |
| The column off by one (no `+ 1`) | 26: every error case and the 40-line one |
| The column counted in bytes, not characters | 2: the two `é` error cases |
| The writer's width test `<` for `<=` | 1: the 120/121 test |

Not checked: a surrogate pair counts two columns (UTF-16 units); a string holding a lone surrogate escape (`"\ud800"`) would throw on writing (`GetString`), which no MCP call is known to send.

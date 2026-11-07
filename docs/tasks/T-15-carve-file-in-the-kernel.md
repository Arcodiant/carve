---
description: >-
  Carve.Core reads and writes the Carve file, JSON geometry that reloads bit-exact with every id kept
---
# T-15 — Carve file in the kernel

**Status:** Done

**Pri:** P1

**Milestone:** M-3

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

`Carve.Core` turns a `PolygonMesh` into the text of a Carve file and back. A file read back gives a mesh equal to the one written: every position bit for bit, and every vertex and face at its id.

## Scope

**`src/Carve.Core/CarveFile.cs`** (new), engine-free like the rest of the Core:

- `CarveFile.Write(PolygonMesh mesh) -> string` and `CarveFile.Read(string text) -> CarveFileResult`. `CarveFileResult` is a mesh or a list of errors, on the model of `MeshResult`.
- **The format** (agreed at planning: JSON text, geometry, not operations):

  ```json
  {
    "carve": 1,
    "vertices": [[0, 0, 0], [1, 0, 0], ...],
    "faces": [[0, 3, 2, 1], ...]
  }
  ```

  - `"carve"` is the format version, an integer, and is written first. Version 1 is the only one.
  - A vertex is `[x, y, z]`. A face is its vertex-id loop in Carve's winding (counter-clockwise seen from outside), exactly as `PolygonMesh.Faces` holds it.
  - Each float is written as the shortest text that reads back to the same `float` (`float.ToString("R", CultureInfo.InvariantCulture)`, or the .NET default shortest round-trip; check which is exact under a test). So `1` stays `1` and `0.12` stays `0.12`, and the file diffs well in git.
  - The layout is stable: two-space indent, one vertex or one face per line, keys in the order above, a newline at the end. A mesh always writes to the same bytes.
  - Undo history is **not** saved.
- **Reading** goes through `PolygonMesh.Create`, so a file is held to exactly what `define_mesh` accepts, and its warnings come back as they would there. Refusals, each naming where it is in the file (for example `vertices[3]`, `faces[2][1]`, `carve`):
  - text that is not JSON (name the line and column the parser gives);
  - a missing or non-integer `carve`, or a version other than 1 (say which versions this build reads);
  - a missing `vertices` or `faces`, or one that is not an array;
  - a vertex that is not three finite numbers;
  - a face that is not an array of integers;
  - an unknown top-level key. M-6 and M-7 will add keys and bump the version. Until then an unknown key is an error, so a newer file is never half-read.
  - Every error `PolygonMesh.Create` gives, passed through with its element name.
- Use `System.Text.Json` (in the BCL). No new package.

**Tests** in `tests/Carve.Core.Tests/CarveFileTests.cs`, `Tier` `unit`:

- Round trip of the example cube, the L-block and the crate (`CrateTests`' mesh): the same vertex count and face count, every `Vector3` equal bit for bit (compare with `BitConverter.SingleToInt32Bits`), and every face loop equal.
- Round trip of awkward floats: `0.1f`, `1.0199999f`, `-0f`, `float.Epsilon`, `1e-30f`, `3.4e38f`. `-0` must read back as `-0`, or the test records that it does not and why that is harmless; ask before accepting a loss.
- `Write` is stable: the same mesh twice gives identical text, and the cube's text matches a literal in the test (the format's worked example).
- One test per refusal above, checking the element name in the error.
- A mesh that `Create` accepts with warnings (an open box) reads back with the same warnings.
- **Show each new check failing** on a broken writer or reader: write with `"G6"`, drop the version check, swap two faces. Give the table of checks and the break that failed each one, not a summary.

## Notes

- **Decided by the person at planning:** JSON text, geometry not operations, versioned, no undo history in the file, floats exact.
- **Design-heavy: this task decides a format** that T-17's tools, the M-6 UV work and the M-7 material work build on. Run it on the stronger model.
- **Open rules: stop and ask.** Any change to the layout above; a float that does not survive the round trip; accepting an unknown key or another version; any tolerance in a comparison (there is none: round trips are exact).
- **Write down for T-17:** the public API (`Write`, `Read`, `CarveFileResult`), the exact error wording, and the cube's file text. T-17 quotes that text in the `save` description.
- This task touches `src/Carve.Core` and `tests/Carve.Core.Tests` only. T-16 adds a separate file to the Core in the same wave (`FlatShading.cs`); neither edits the other's file.
- `CoreIsEngineFreeTests` must still pass: `System.Text.Json` is BCL, not engine.
- Nothing here is seen on screen, so there are no frames.
- Run `finish` from inside the worktree and leave the folder; do not delete it by hand.

### As built

Run on **Claude Opus 5.5** (`claude-opus-5-5`).

**Public API** (`src/Carve.Core/CarveFile.cs`):

- `public static class CarveFile`
  - `const int Version = 1`: the version `Write` writes.
  - `static IReadOnlyList<int> ReadableVersions` = `[1]`: the versions `Read` reads.
  - `static string Write(PolygonMesh mesh)`: the file's text. The same mesh always gives the same bytes.
  - `static CarveFileResult Read(string text)`.
- `public sealed class CarveFileResult`: `PolygonMesh? Mesh`, `IReadOnlyList<MeshError> Errors`, `IReadOnlyList<MeshWarning> Warnings`, `bool Succeeded`. On failure `Mesh` is null and `Warnings` is empty. On success `Warnings` is `MeshReport.Create(mesh).Warnings`, the list `define_mesh` reports for the same mesh. `Create` itself returns no warnings; `MeshReport` gives them.
- Errors reuse `MeshError(Element, Message)`; `ToString()` is `"<element>: <message>"`.

**Layout.** As the Scope says:

- The keys are `carve`, `vertices` and `faces`, in that order.
- Two-space indent, with items at four spaces, one per line.
- An item is written `[x, y, z]` or `[a, b, c, ...]`, with `", "` between numbers.
- Lines end in `\n` always (never `Environment.NewLine`), and the file ends with a newline.
- An empty list, which `Create` never makes, would be `[]`.

**Floats.**

- Written with `float.ToString("R", CultureInfo.InvariantCulture)`. A test checks that this equals the .NET default `ToString()` on every awkward value and parses back to the same bits.
- It can write exponents (`1E-45`, `1E-30`, `3.4E+38`), which are valid JSON numbers.
- `-0f` writes as `-0` and reads back as `-0` (bits `0x80000000`). Nothing is lost.
- Reading parses each coordinate's own token text straight to `float` (`float.Parse`, not via `double`), so it is rounded once.
- Reading takes the keys in any order, with any whitespace.

**The cube's file text** (`CarveFile.Write(TestMeshes.Cube())`, the literal `CarveFileTests.CubeText`):

```json
{
  "carve": 1,
  "vertices": [
    [0, 0, 0],
    [1, 0, 0],
    [1, 1, 0],
    [0, 1, 0],
    [0, 0, 1],
    [1, 0, 1],
    [1, 1, 1],
    [0, 1, 1]
  ],
  "faces": [
    [0, 3, 2, 1],
    [4, 5, 6, 7],
    [0, 1, 5, 4],
    [3, 7, 6, 2],
    [0, 4, 7, 3],
    [1, 2, 6, 5]
  ]
}
```

**The exact error wording.** Each row gives the element, then the message. `{…}` marks a part that is filled in.

| Element | Message |
|---|---|
| `file` | `the text is not JSON: at line {L}, column {C}: {parser's message}`, for example `the text is not JSON: at line 5, column 15: ',' is an invalid start of a value.` L and C are one-based: the parser's zero-based `LineNumber` and `BytePositionInLine`, plus 1. The column counts UTF-8 bytes. The parser's own position suffix is cut off. |
| `file` | `the file is a JSON {array / string / number / boolean / null}; a Carve file is a JSON object` |
| `carve` | `missing: the file has no format version; this build reads version 1` |
| `carve` | `the format version is {1.5 / a string / …}, not an integer; this build reads version 1` |
| `carve` | `version {n} is not one this build reads; it reads version 1`. **Returned alone**: a file of another version gets no other errors, since its other keys may mean something else. |
| `vertices` / `faces` | `missing: the file has no vertices array` / `missing: the file has no faces array` |
| `vertices` / `faces` | `vertices is {an object / 3 / a string / …}, not an array` (and `faces is …`) |
| `vertices[i]` | `vertex is {a string / 7 / an object / …}; a vertex is [x, y, z]` |
| `vertices[i]` | `vertex has {n} coordinates; a vertex is [x, y, z]` |
| `vertices[i][k]` | `coordinate is {null / a string / …}, not a number` |
| `vertices[i][k]` | `coordinate {raw text} is not finite: it is outside the range of a float`, for example for `1e39`. JSON has no NaN or Infinity literals. |
| `faces[f]` | `face is {7 / a string / …}; a face is an array of vertex ids` |
| `faces[f][k]` | `index is {7.5 / a string / 4294967298 / …}, not an integer vertex id`. Any value outside `int` is refused here. |
| `{key}` | `unknown key: a version 1 Carve file has only the keys "carve", "vertices" and "faces"` |
| `{key}` | `key appears more than once`. This one is not in the Scope's list: a repeated key is refused rather than one copy silently winning. |
| `keys[n]` | `the key is not valid text: it holds a lone surrogate escape (a \ud800 to \udfff with no partner)`. `n` is the key's zero-based position among the object's keys, since its text cannot be named. This element name is a reading the spec did not settle. |
| any | Every `PolygonMesh.Create` error, passed through unchanged (`faces[3][3]: index 8 is out of range 0..7`, `faces: mesh has no faces`, …). Its element names are already the file's paths. |

Values in messages are as written in the file (`GetRawText`). Strings, arrays and objects are named by kind. Every structural error is collected (`EveryErrorIsNamed`), and `Create` is reached only when there are none.

**Checks and the break that failed each one.** Each break was applied to `CarveFile.cs` alone. Then `CarveFileTests` was run and the file restored. The script is kept in `%TEMP%\t15\breaks.py`, not committed.

The float cases are named by value here. The test run names them by bits:

| Bits | Value |
|---|---|
| `1065520987` | `1.0199999f` |
| `-2147483648` | `-0f` |
| `1` | `float.Epsilon` |
| `1036831949` | `0.1f` |
| `228737632` | `1e-30f` |
| `2139081118` | `3.4e38f` |

| Break | Checks that failed |
|---|---|
| write with `"G6"` | `AnAwkwardFloatRoundTripsBitForBit(1.0199999f)`, `TheCrateRoundTrips` |
| write `-0` as `0` | `AnAwkwardFloatRoundTripsBitForBit(-0f)`, `MinusZeroIsWrittenAsMinusZero` |
| swap faces 0 and 1 in the writer | `TheCubeRoundTrips`, `TheLBlockRoundTrips`, `TheCrateRoundTrips`, all six `AnAwkwardFloatRoundTripsBitForBit`, `WriteIsStable`, `MinusZeroIsWrittenAsMinusZero`, `ShortFloatsStayShort` |
| swap faces 0 and 1 in the reader | `TheCubeRoundTrips`, `TheLBlockRoundTrips`, `TheCrateRoundTrips`, all six `AnAwkwardFloatRoundTripsBitForBit`, `TheWorkedExampleReads`, `AFileWithNoFacesGetsCreatesError` |
| indent items by 3 spaces | `WriteIsStable`, `MinusZeroIsWrittenAsMinusZero`, `ShortFloatsStayShort` |
| a call counter makes each write differ | `WriteIsStable` |
| drop the version check | `AnotherVersionIsRefused(2)`, `AnotherVersionIsRefused(0)`, `AnotherVersionIsRefusedBeforeItsKeys` |
| drop the missing-version check | `AMissingVersionIsRefused` |
| drop the integer-version check | `ANonIntegerVersionIsRefused` (both cases) |
| let a parse error escape the reader | `TextThatIsNotJsonIsRefusedWithItsLineAndColumn` |
| give the column zero-based | `TextThatIsNotJsonIsRefusedWithItsLineAndColumn` |
| drop the top-level object check | `ATopLevelThatIsNotAnObjectIsRefused` |
| drop the missing-vertices check | `MissingVerticesAreRefused` |
| drop the missing-faces check | `MissingFacesAreRefused` |
| drop both not-an-array checks | `VerticesThatAreNotAnArrayAreRefused`, `FacesThatAreNotAnArrayAreRefused` |
| drop the vertex-is-an-array check | `AVertexThatIsNotThreeNumbersIsRefused` (the string case) |
| drop the three-coordinates check | `AVertexThatIsNotThreeNumbersIsRefused` (2 and 4 coordinates), `EveryErrorIsNamed` |
| drop the coordinate-is-a-number check | `ACoordinateThatIsNotAFiniteNumberIsRefused` (null and string) |
| drop the finite check | `ACoordinateThatIsNotAFiniteNumberIsRefused` (`1e39`) |
| drop the face-is-an-array check | `AFaceThatIsNotAnArrayIsRefused` |
| drop the integer-index check | `AFaceIndexThatIsNotAnIntegerIsRefused` (all three cases), `EveryErrorIsNamed` |
| accept unknown keys | `AnUnknownKeyIsRefused`, `EveryErrorIsNamed` |
| drop the repeated-key check | `ARepeatedKeyIsRefused` |
| drop `Create`'s errors | `CreateErrorsPassThroughWithTheirElements`, `AFileWithNoFacesGetsCreatesError` |
| drop the warnings | `AnOpenBoxReadsBackWithItsWarnings` |

**Not shown failing:**

- `RoundTripFormatIsShortestAndExact` checks the BCL (`"R"` against the default, and the parse back), not Carve's code.
- `ACleanMeshReadsBackWithNoWarnings` guards against invented warnings, and no break was tried against it.
- Parsing a coordinate as `double` and then rounding it to `float` failed no check. The writer's shortest texts never hit a double-rounding case. The reader parses straight to `float` by design, but no test guards that.

**Changed outside `CarveFile.cs`:** the crate's construction is `TestMeshes.Crate()` (`tests/Carve.Core.Tests/TestMeshes.cs`; first it was `CrateTests.Build()`), so the round trip uses the same mesh. `TheCrate` checks what it checked before.

**A reading (RV-14).** A face index of `1.0` is refused in a file but accepted by `define_mesh`. T-15's refusal list says a face is an array of integers, and `Write` never emits `1.0`, so only a hand-edited file meets it. No change needed.

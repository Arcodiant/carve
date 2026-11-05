---
description: >-
  Carve.Core reads and writes the Carve file, JSON geometry that reloads bit-exact with every id kept
---
# T-15 — Carve file in the kernel

**Status:** In Progress

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

---
description: >-
  An agent exports the current mesh with export to a .glb with flat normals and one matte material, which Empyrean's glTF parser reads back exactly
---
# T-16 — Export to glb

**Status:** In Progress

**Pri:** P1

**Milestone:** M-3

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent calls `export` and gets a self-contained `.glb` of the current mesh that a game loads as is. Its triangles, normals and winding are exactly what Carve draws, so a game sees the model as `render` shows it.

## Scope

**One source of drawn and exported triangles.** `src/Carve.Core/FlatShading.cs` (new): from a `PolygonMesh`, the flat-shaded triangle list. For each triangle in `Triangulator` order, three positions in model space in Carve's winding (counter-clockwise from outside) and the face's Newell normal (`FaceNormal`). `MeshBridge.ToMeshData` is rewritten on top of it, keeping its drawn-space transform and its winding reversal. **Every existing frame must stay byte-identical**: check the `scenario-cube` and `scenario-crate` frames by hash, before and after.

**The writer**, `src/Carve.Server/GlbExport.cs` (new), on **SharpGLTF.Toolkit 1.0.7**. Pin `SharpGLTFVersion` in `Directory.Build.props` with a comment; `Carve.Server` is the only project that references it apart from tests.

- One scene, one node and one mesh, both named after the file name without extension (`crate.glb` gives `crate`). The node has an identity transform.
- One primitive: `POSITION` and `NORMAL` only, triangles, indexed. Positions are exactly the mesh's floats (pass-through, with no scale, axis change or recentring); normals come from `FlatShading`. glTF winding is counter-clockwise, as Carve's is, so it is written as is.
- One material named `carve`: metallic-roughness, base colour `(0.72, 0.72, 0.70, 1)` (Carve's render surface), metallic 0, roughness 1, single-sided. The planning probe found that glTF's default material is metallic 1, so these values must be set explicitly.
- The same mesh always writes to the same bytes (the probe found SharpGLTF 1.0.7 deterministic; keep a test).

**Paths: `src/Carve.Server/ToolPaths.cs`** (new). This task owns it, and T-17's `save` and `open` use it. It carries the rules agreed at planning:

- A relative path resolves against carve's working directory, and every result gives the absolute path.
- Missing parent folders are created.
- An existing file is replaced, and the result says `replaced`.
- The extension is checked: here `.glb`, case-insensitive. Any other is a tool error naming the extension wanted.
- An empty path, or one with invalid characters, is a tool error. An I/O failure (access denied, a locked file) is a tool error that names the path and the system's message, and leaves no partial file: write to a temporary file in the same folder, then move it into place.

**The tool `export(path)`**, registered in `Program.cs`'s `WithTools` list, closed schema like the others:

- Before `define_mesh` it is a tool error saying so. A mesh with warnings exports, and the warnings are repeated in the result.
- The result is one text line, for example: `exported crate.glb to D:\game\Content\models\crate.glb (replaced): 140 triangles, 1 material 'carve', size 1.04 x 0.8 x 1 m, bounds (-0.02, 0, 0) to (1.02, 0.8, 1)`. Numbers are culture-invariant, as elsewhere. Use the real triangle count, and say "1 unit = 1 m" in the description, not in every result.
- **The description** is the interface (D-1's content). It says what is written (flat normals, every edge hard, one matte grey material), the axes (+Y up, +Z front, metres, the origin where it was authored, nothing converted), the path rules, every error, and a worked example: export to `<game>/Content/models/crate.glb`, which a game loads as `models.LoadModel(new ContentPath("models/crate.glb"))`. It says the `.glb` is for the game and the `.carve` file (T-17) is the one to keep working in.
- `export` does not change the mesh, the undo stack or the version.

**Tests:**

- `tests/Carve.Server.Tests/GlbExportTests.cs`, `unit` tier, with no process and no GPU. Export the cube, the L-block and the crate, then parse the bytes with **Empyrean's own glTF parser**, `IModelGeometryLoader.Parse(bytes, ModelFormat.Gltf)`. The probe built it from `AddLogging()`, `AddEmpyreanCore()` and `AddEmpyreanGeometry()`. Assert:
  - the triangle count;
  - every triangle's three positions and normals equal to `FlatShading`'s, bit for bit, with the engine's winding flip (the parser makes triangles clockwise). Compare in order if SharpGLTF keeps triangle order, otherwise as a multiset, and say which;
  - one submesh material: base colour `(0.72, 0.72, 0.70, 1)`, `Pbr` true, metallic 0, roughness 1;
  - two exports of one mesh are byte-identical.
- `ToolPaths` tests, `unit`: relative and absolute paths, a created folder, `replaced`, a wrong extension, an empty path.
- `tests/Carve.Server.Tests/EditToolTests.cs` or a new `ExportToolTests.cs`, `regression` tier (it starts `carve`):
  - export through the real tool;
  - export before `define_mesh`;
  - a wrong extension;
  - the description's worked example run as written, into a temp folder;
  - `tools/list` lists `export`.
- **Show each new check failing** on a broken product: no normals written, winding reversed, metallic left at 1, the triangulation order changed, a path not created. Give the table.

## Notes

- **Decided by the person at planning:**
  - SharpGLTF.Toolkit;
  - flat normals with every edge hard in M-3 (smoothing control is later);
  - pass-through scale and axes with no convention checks;
  - one plain material as above;
  - the path rules above.
- **Design-heavy: it decides the export's shape and `ToolPaths`**, which T-17 builds on. Run it on the stronger model.
- **Open rules: stop and ask.**
  - Any change to an existing frame (the bridge refactor must be invisible).
  - A material value other than the above.
  - Any convention check or conversion.
  - Smoothing.
  - Writing UVs or any attribute beyond `POSITION` and `NORMAL`.
  - Any tolerance in the parse comparison (it is exact).
- **A fixer may not change** `MeshBridge`'s drawn space, the light or the framing to make a frame match. If a frame changes, stop and report.
- **Write down for T-17 and T-18:** the `ToolPaths` API and its error wording; the SharpGLTF pin's property name; and the code that builds an `IModelGeometryLoader` without a GPU.
- This task owns, in this wave: `Directory.Build.props`, `Program.cs`'s tool list, `MeshBridge.cs`, the new Core file `FlatShading.cs` and `ToolPaths.cs`. T-15 is in the Core in the same wave, in a different file.
- No new frames. The export is first seen through the engine in T-18 and T-19.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on **Claude Opus 5.5** (`claude-opus-5-5`).

**Files.** `src/Carve.Core/FlatShading.cs` (`FlatShading.Triangles(mesh)` gives `FlatTriangle(A, B, C, Normal, Face)` in `Triangulator` order, model space, Carve's CCW winding, the face's `FaceNormal`); `MeshBridge.ToMeshData` now reads it, keeping `DrawnSpace.Apply` and the (A, C, B) reversal. `src/Carve.Server/GlbExport.cs` (`GlbExport.Write(mesh | triangles, name)` gives the `.glb` bytes), `src/Carve.Server/ToolPaths.cs`, `src/Carve.Server/ExportTool.cs` (the tool and its description), registered last in `Program.cs`'s `WithTools` list. Tests: `tests/Carve.Core.Tests/FlatShadingTests.cs`, `tests/Carve.Server.Tests/GlbExportTests.cs` and `ToolPathsTests.cs` (unit), `ExportToolTests.cs` (regression). The test project compiles `GlbExport.cs` and `ToolPaths.cs` as links, as it does `MeshBridge.cs`.

**For T-17 and T-18.**

- **SharpGLTF pin:** `SharpGLTFVersion` (1.0.7) in `Directory.Build.props`; referenced as `SharpGLTF.Toolkit` by `Carve.Server` and `Carve.Server.Tests` only.
- **`ToolPaths` API** (`namespace Carve.Server`):
  - `ToolPath? ToolPaths.Resolve(string tool, string? path, string extension, out string? error, string? baseDirectory = null)`: `extension` with its dot (`".glb"`, `".carve"`); `baseDirectory` null means `Environment.CurrentDirectory` (tests pass a temp folder). Returns `ToolPath(FullPath, FileName)` with `Stem` (file name without extension).
  - `bool ToolPaths.Write(string tool, ToolPath path, ReadOnlySpan<byte> bytes, out bool replaced, out string? error)`: creates missing folders, writes `.<file>.<pid>.tmp` in the same folder, then `File.Move(..., overwrite: true)`; the temp file is deleted on failure. `open` (T-17) needs only `Resolve`; a read helper is T-17's to add.
  - Every error is one line, `<tool>: ...`, returned as the tool error as is:
    - `<tool>: path is empty; give a file path ending in .glb`
    - `<tool>: path "<path>" holds the character '|', which a file path may not` (or `the control character U+0000`; folder names are checked too, so `*`, `?` and a second `:` are caught)
    - `<tool>: path "<path>" is not a valid file path: <system message>`
    - `<tool>: path "<path>" names a folder; give a file path ending in .glb`
    - `<tool>: path "<path>" ends in ".obj"; <tool> writes .glb files, so the path must end in .glb` (or `has no extension; ...`)
    - `<tool>: could not write <full path>: <system message>` (or `: it is a folder`)
  - The "writes" in the extension error is fixed wording; `open` will want "reads" (a parameter T-17 may add).
- **The export result line**: `exported crate.glb to <full path> (replaced): 140 triangles, 1 material 'carve', size 1.04 x 0.8 x 1 m, bounds (-0.02, 0, 0) to (1.02, 0.8, 1)`. ` (replaced)` appears only when a file was replaced; a new file has no parenthesis. Warnings follow as `; N warning(s): a; b`. Floats are shortest round-trip, invariant.
- **An `IModelGeometryLoader` without a GPU** (from `GlbExportTests`; the test project needs `Microsoft.Extensions.Hosting` for `AddLogging`/`BuildServiceProvider`):

  ```csharp
  var services = new ServiceCollection();
  services.AddLogging();
  services.AddEmpyreanCore();
  services.AddEmpyreanGeometry();
  var parser = services.BuildServiceProvider().GetRequiredService<IModelGeometryLoader>();
  ParsedModel parsed = parser.Parse(bytes, ModelFormat.Gltf);
  ```

**What the parse showed.** SharpGLTF 1.0.7 keeps triangle order, so the comparison is **in order**, exact to the bit (positions and normals compared as `SingleToInt32Bits`): Carve's (A, B, C) reads back as (A, C, B), the order `MeshBridge` draws. The parser reports no warnings. The material reads back as base colour (0.72, 0.72, 0.70, 1), `Pbr` true, metallic 0, roughness 1, not double-sided. Two writes are byte-identical for the cube, L-block and crate.

**Each check failing on a broken product** (unit checks, run by hand with the break applied and then reverted; the regression checks were not broken one by one):

| Break | Checks that failed |
|---|---|
| No normals written (`VertexPosition` only) | `GlbExportTests`: bit-for-bit readback (cube, L-block, crate), and the one-primitive `POSITION`+`NORMAL` check |
| Winding reversed (B and C swapped) | bit-for-bit readback, all three meshes |
| Metallic left at 1 | `One_matte_material_carve`, all three meshes |
| Triangulation order changed: export writes triangles reversed | bit-for-bit readback, all three meshes |
| Triangulation order changed: `FlatShading` reverses `Triangulator` order | `FlatShadingTests`, cube and L-block |
| Path not created (no `Directory.CreateDirectory`) | `ToolPathsTests.A_write_creates_missing_folders_and_says_when_it_replaced_a_file` |

**Frames.** HASHES_PLACEHOLDER

**Not checked.** No `.glb` has been loaded by a running game or rendered through the engine; per the spec that is T-18 and T-19. The regression tests were run only through `verify`, not each against a break.

### Hand-off

Run on Claude Opus 5.5. **Done:** the code and tests, committed and rebased onto `origin/milestone/M-3` (after T-15, with no conflicts); the unit tiers pass (core 131, server 119 before the rebase); the break table above; `### As built`. **Left:** `cairn task T-16 verify` has not completed yet. Every run so far has waited for a GPU slot: first on other Empyrean jobs, now on `verify M-151`, and the credential cannot queue `any-gpu`, so it runs here. Once it passes, fill in **Frames** in As built (the frames before the change, hashed from the carried 9bb593520 artifacts, are in `$TEMP/t16-before.sha`; compare them with `cairn test artifacts scenario-cube` / `scenario-crate`), then run `cairn lint` and `finish`. **Checked:** the readback is bit-exact and in order for the cube, L-block and crate. **Not checked:** the regression tier (`ExportToolTests`) and the frame hashes after the change.

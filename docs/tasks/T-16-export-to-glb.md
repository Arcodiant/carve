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

---
description: >-
  render draws a material whose slots name graph outputs, evaluated on each mesh's UVs and bakes, re-read and re-baked when the graph files, the mesh or its UVs change, and reports what it baked and how long it took
---
# T-163 — Graph materials evaluated and cached for render

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-160, T-161

**Verify:** core-unit, server-unit, server-regression, scenario-materials, scenario-planks

## Goal

`render` draws a material whose slots name graph outputs. Each slot is evaluated at its size, on the UV layout and
bakes of the mesh that wears it when the graph reads the mesh, kept until something it depends on changes, and baked
again on the next render after an edit to the mesh, its UVs or a graph file. The answer says what was baked and how
long it took.

## Scope

- **The bake of a slot** (`src/Carve.Graphs`, engine-free, new files only): given a graph reference (T-160), a `Model`
  and a mesh name, it reads the graph with `TexGraphFile.ReadFile` (so nested files resolve beside it, T-140's As
  built), validates it against the registry with T-161's nodes bound to a `MeshBakeSource` for that mesh, evaluates
  the output at the slot's size and gives the PNG bytes the slot's texture is.
  - **What each slot takes**: `base_colour_texture` and `emissive_texture` a colour output (a grey one is written as
    grey RGB); `normal_texture` the `normal` role or a colour output; `metallic_roughness_texture` the output
    `roughness`, packed as patina's `roughness.png` is (G roughness, B metallic when the graph has a `metallic` output,
    T-134); `occlusion_texture` a grey output in R. The bytes come from `Patina.Graphs`' own packing
    (`OutputImage`), so **a graph with no mesh node gives exactly the PNG patina's `export` writes**: a test holds
    `planks.texgraph`'s four outputs at 256 equal to `samples/graphs/planks/*.png` byte for byte.
  - A graph that reads the mesh is padded 8 texels past island borders after evaluation (`Bake.Pad`), as M-24's
    `bake` pads; one that does not is left as evaluated (it tiles).
  - `reads_mesh` is true when the expanded graph has a mesh node. `define_material` and `get_mesh` report it per
    slot (the field T-160 left out).
- **The cache** (in `Carve.Server`, in memory, never in the Carve file): keyed by content, so nothing has to be told
  that something changed.
  - The key of a slot that does not read the mesh: a hash of the graph file's bytes and every nested file's
    (`GraphValidator.Uses(...).Files`), the output and the size. One entry serves every mesh.
  - The key of a slot that reads the mesh adds the mesh's vertices, faces, UVs, seams, groups, face materials,
    shading and hard edges, and the vertices and faces of every other mesh of the model (occluders). So a `move`,
    `unwrap`, `set_uvs`, `define_group`, `mark_edges` or an edit to another mesh makes the next render bake again, and
    `undo` back to a state already baked is a hit.
  - The graph files are read and hashed at every render and export. Capped at 512 MB of PNG and decoded bytes,
    least recently used dropped first.
- **`render`**: a mesh's faces under a graph material are drawn with the baked textures through `PbrMaterials`, as a
  PNG material's are (the bake's PNG bytes stand where a `MaterialTexture`'s do, so upload, mipmaps and the sampler
  are T-78's). A mesh-reading material used by two meshes is two engine materials, one per mesh; `PbrMaterials.Keep`
  releases a bake's textures when no drawn mesh uses them.
  - Bakes run before the frame is queued, off the frame thread, and honour the call's cancellation token between
    tiles and between maps.
  - The text result gains `baked: [{material, mesh, slot, size, ms}]`, one entry per slot baked in this call, empty or
    absent when everything came from the cache. A mesh-independent slot gives `mesh: null`.
  - **A warning when one material's bakes took over 5 s in this call**, naming the slot and node that cost most and
    saying to lower `size` or the node's `rays`: `material 'weathered-staves' on 'barrel' took 7.9 s to bake
    (base_colour_texture 1024: mesh_occlusion 'ao' 6.1 s); lower its size or the node's rays`. Nothing is refused.
  - Every look that draws materials is covered: the shaded look, a sheet, a posed or clip frame (the bake is of the
    mesh at rest), a part tree (instances of one mesh share its bake) and a kept level (its own mesh, its own bake).
  - A slot that cannot be baked (file gone, a fault, no UVs, overlapping UVs, an unknown output) fails the render with
    one error through a single helper, `GraphSlotProblem` (a file of its own), giving material, slot and the cause's own
    message. T-166 owns its wording after this task; here it is enough that nothing is drawn stale or blank.
  - `render`'s description gains the paragraph on graph materials: evaluated per mesh, baked on first use and after
    edits, `baked` and the warning. T-160's interim refusal goes.
- **Tests**:
  - `graphs-unit`: the slot table above, the planks byte equality, padding, `reads_mesh`, and that the key changes
    for each listed edit and for a one-byte change to a nested file, and does not change for an edit that cannot
    matter to a mesh-independent slot.
  - `server-unit` (`ToolHarness`): `baked` on the first render and absent on the second; a `move`, an `unwrap`, a graph
    file rewritten on disk and a nested file rewritten each bake again; `undo` is a hit; the 5 s warning through an
    injected clock in the test, never the wall clock in the Core.
  - `server-regression`, one new class: a graph material on the raised-hoop barrel (built through the tools in the
    test, T-162's graphs copied beside it) drawn at 512; the frame equals, under D-6's frame rule, the frame of the
    same model with the bake's PNGs named as ordinary PNG textures, with a control that must fail (the PNGs of the
    neutral graph instead). A sampling test in the manner of `RenderGlbSamplingTests` covers the baked upload.
- Every existing frame of `scenario-materials` and `scenario-planks` stays byte-identical (by hash, from runs on one
  runner, fetched by job id).
- CLAUDE.md's `src/Carve.Graphs` and `src/Carve.Server` rows, and the architecture rule: a graph slot's bake is made
  in `Carve.Graphs`, cached by content in the server and never stored in the Carve file.

## Notes

- **Decided at planning (2026-11-20, by the person):** graph files are read again at each render and export, and a
  change to their content re-bakes; one bake per mesh for a material that reads the mesh; `baked` always reported and a
  warning past 5 s, nothing refused; the bake never stored in the Carve file (CLAUDE.md).
- **The orchestrator's calls, each a stop if it proves wrong:** the slot table (which output kinds a slot takes); the
  cache's key contents and its 512 MB cap; 8 texels of padding for mesh-reading outputs; bakes off the frame thread.
- **Planning's times** (Debug, the barrel, 34 to 98 faces): at 1024, curvature 0.6 s and occlusion 3.5 to 5.6 s; at 512,
  occlusion 1.3 to 1.4 s. M-24 measured 7.4 to 8.8 s for occlusion on the wrench at 1024, and its ORM ran past a 30 s
  answer window under load. So the tests and every worked example use 256 or 512, and the 5 s warning is expected to
  fire on a real 1024 occlusion bake.
- Design-heavy; it decides how T-164, T-165 and T-166 reach a bake: Opus, planned as two agents (the bake and cache with
  their unit tests, then render and the regression class).
- **Write down for them** (As built): the call that gives a slot's PNG for a mesh, the cache and its key, `GraphSlotProblem`
  and every cause it carries, how `baked` and the timing are gathered, and the measured bake times of T-162's two
  graphs on the raised-hoop barrel at 512 and 1024.
- Runs beside T-162, which owns patina and `samples/graphs/`; this task takes test graphs from its own test folder
  until T-162 lands, then switches the regression class to the committed ones (expect a hand-off there, as M-27's T-113
  waited on T-112).
- **Stop and ask:** an existing frame or export hash that changes; a determinism rule that would have to bend (a
  parallel evaluator, a wall-clock read in an engine-free project); a mismatch with the PNG path under D-6's rule that
  is not explained.

### Hand-off

First agent (Opus 5.5, `claude-opus-5-5`), at its ~60-call checkpoint. Builds; `graphs-unit` passes (40); nothing else run.

**Done.**
- `src/Carve.Graphs/GraphSlotBake.cs` (new): `GraphSlotFiles(Path, Files, Hash, ReadsMesh)`, `GraphSlotException`,
  `GraphSlotBake.Read(path)` (root + `GraphValidator.Uses(...).Files`, SHA-256 of path+bytes; `ReadsMesh` from mesh node types
  in the root and every nested file), `Key(files, output, size, model, mesh)` (the listed mesh fields; occluders' vertices and
  faces), `Png`/`Rgba(MaterialGraph, slot, model, mesh?, cancel, wrap?)` (cancel checked per tile; padding with `Bake.Pad` 8
  when `mesh` is given), `PackingOf(slot, evaluator, output)` (the slot table). `MeshBakeSource.Layout(size)` made public (locked).
- `tests/Carve.Graphs.Tests/GraphSlotBakeTests.cs`: planks' four outputs at 256 byte-equal to `samples/graphs/planks/*.png`,
  slot table, padding (fbm with a stray mesh node == `Bake.Pad` of the tiled one), nested reads_mesh, key per edit, nested
  one-byte change, mesh-independent key unmoved.
- `src/Carve.Server/GraphBakes.cs` (new, DI singleton in `CarveServer.AddTools`): LRU cache by key, 512 MB of PNG + size²·4;
  `Resolve(model, cancel)` returns `Drawn(Model, Key, Baked, Warnings)`: graph slots become `MaterialTexture`s of the bake;
  a mesh-reading material becomes `<m>@<mesh>` per mesh (kept level `<m>@<part>@<level>`, baked on `model.With(part, levelMesh)`)
  with faces renamed; memo by `Model` reference (`ConditionalWeakTable`), same reference kept when every key is unchanged.
  `Peek`, `DrawnOf` (frame thread; bakes only on a race, `shortcut:` comment). `TimedMaps` wraps the source to name the
  costliest mesh node. `Text(baked)` gives `\nbaked: [...]` and `\nwarning: ...` lines. Clock injectable (`Func<double>` ms).
- `src/Carve.Server/GraphSlotProblem.cs` (new): `Of(material, slot, cause)`, `IsCause`, `GraphSlotProblemException`.
- `RenderPump(queue, store, scene, GraphBakes? bakes)`: draws `bakes.DrawnOf(snapshot.Model)`; re-shows the mesh when
  `Peek` of the shown model changes (`_bakedFor`/`_shownBakes`, set on the fault path too, so no loop). `RenderSystem` takes
  `GraphBakes`. `RenderTool.Run` takes `GraphBakes bakes`, resolves inside `RenderTiles` (Task.Run, before posting), returns a
  `GraphSlotProblemException` as the error, appends `GraphBakes.Text(baked)` to both captions. Render's interim refusal removed.

**Left.**
- Export's interim refusal (`ExportTool.cs:146`) left in place on purpose: export is T-164's and would otherwise drop graph
  slots silently. Decide/ask; if it stays, `MaterialJson.GraphsNotDrawn` stays and `GraphSlotToolTests` line ~214 keeps
  only export's half (render's half of that test must now change).
- define_material's sentence "Until graph materials are drawn, ..." (MaterialTools.cs ~84) and README's interim sentence;
  render's description paragraph (keep < 4,096; retake `PartToolsListTests.Before` digest and say so); `reads_mesh` per slot
  in `define_material` and `get_mesh` (`MaterialJson.Of`), using `GraphSlotBake.Read(path).ReadsMesh` (catch for a missing file).
- server-unit tests of `GraphBakes` (no engine in `ToolHarness`: drive the model through tools, call `Resolve` directly):
  baked then absent, move/unwrap/file/nested rewritten bake again, undo a hit, 5 s warning by injected clock, 512 MB cap.
- server-regression class (raised-hoop barrel at 512, baked vs PNG-named frame under D-6, neutral-graph control, a
  sampling test), first on test graphs, then T-162's `weathered-iron`/`weathered-staves` once on `origin/milestone/M-29`.
- CLAUDE.md rows and architecture rule; `**Verify:**` gains graphs-unit (and any suite checking tool text); measured times
  of T-162's graphs at 512 and 1024; `### As built`.

**Not checked.** No render run at all yet; frames of scenario-materials/planks not compared (models without graph
materials take the old path unchanged: `DrawnOf` returns the model itself, `Peek` null).

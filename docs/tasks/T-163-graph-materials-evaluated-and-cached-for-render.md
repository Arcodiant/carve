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

**Verify:** core-unit, graphs-unit, server-unit, server-regression, scenario-materials, scenario-planks

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

First agent (Opus 5.5, `claude-opus-5-5`) built the bake, the cache and render's path (see its list in git history, commit
`f190ec6`). Second agent (Opus 5.5, `claude-opus-5-5`), at its checkpoint (past 60 calls), after rebasing onto T-162's landing.

**Done (both agents).**
- `GraphSlotBake` (`src/Carve.Graphs`): `Read(path)` -> `GraphSlotFiles(Path, Files, Hash, ReadsMesh)`, `Key(files, output, size,
  model, mesh?)`, `Png`/`Rgba(MaterialGraph, slot, model, mesh?, cancel, wrap?)`, `PackingOf(slot, evaluator, output)`,
  `GraphSlotException`. `MeshBakeSource.Layout(size)` made public (T-161's API change).
- `GraphBakes` (`src/Carve.Server`, DI singleton): `Resolve(model, cancel)` -> `Drawn(Model, Key, Baked, Warnings)`; `Peek`;
  `DrawnOf` (frame thread, **never bakes**: an unresolved model is drawn with its graph slots empty, and the pump re-shows when
  `Peek` changes; fixed by the second agent, since the pump shows every store change and so baked on the frame thread before
  render did, racing it and leaving `baked` short); `Text(baked)`; one `MeshBakeSource` shared per drawn mesh within a
  `Resolve` (staves' occlusion was evaluated once per slot); the warning counts a material's copy whole (mesh-independent
  slots of a mesh-reading material included). `GraphSlotProblem.Of/IsCause`, `GraphSlotProblemException`.
- `reads_mesh` per graph slot in `MaterialJson.Of` (define_material, get_mesh; null when the file cannot be read).
- Interim wording narrowed to export (orchestrator's answer): `define_material`'s sentence, README, `MaterialJson.GraphsNotDrawn`
  doc; `GraphSlotToolTests.Export_refuses_a_model_with_a_graph_slot_until_it_writes_graph_materials` holds export's refusal.
  **T-164 must clear:** `ExportTool.cs:146` (the refusal), `MaterialJson.GraphsNotDrawn`, that test, `define_material`'s
  sentence "export refuses a model with one until it writes them", the README's "`export` does not write one yet ...".
- render's description gained the paragraph (render's text was ~18,600 characters before this task, far over the 4,096 cut,
  like save's and open's; not held by the length test, and **a question for the person**). tools/list digest
  (`PartToolsListTests.Before`) retaken to `D533456632C9A323AF5BA5E04D34B8357B06400D6B6EA0F975FDA0CFEEEDA596`; only
  define_material's sentence and render's paragraph changed against the milestone.
- `ProductReferenceGuard`'s allowed list has `Carve.Graphs` (merged with T-162's same line on rebase).
- Tests: `GraphBakesTests` (server-unit: unresolved draws empty and bakes nothing, baked then a hit, move/unwrap/root file/nested
  file bake again, undo a hit, the per-mesh copy, 5 s warning by injected clock, a missing file's problem, reads_mesh);
  `GraphMaterialRenderTests` (server-regression, on T-162's committed graphs: iron on the hoops, staves elsewhere, 512, against
  the bakes as PNGs and the neutral control; a fine fbm 1024 sampling test on a box face; a 1024 bake of both, which asserts all
  6 slots are baked by render). The graph frame and the PNG frame were byte-identical (frames tsv, run before the DrawnOf fix).
- `scenario-materials` (24 PNGs) and `scenario-planks` (17) byte-identical by hash: jobs J-20261124-063731-0ce200 /
  J-20261124-063729-38333b (this task) against J-20261124-065300-1481c6 / J-20261124-065301-20fd62 (origin/milestone/M-29),
  all on Mantorok (before the rebase onto T-162 and the DrawnOf fix; models without graphs take the old path).
- CLAUDE.md rows and the architecture rule; `**Verify:**` gained graphs-unit.
- Last local runs (uncommitted then, now committed): server-regression PASS (245), server-unit PASS.

**Left.**
- Measured times for As built: the only 1024 numbers are from a run *before* the DrawnOf fix (frame thread baking concurrently,
  so inflated): iron 14.6 s (base_colour 8.6 s, mesh_curvature 1.4 s), staves 13.7 s (base_colour 11.5 s, mesh_occlusion 8.9 s);
  512 before the source sharing: iron 2.8 s, staves 5.8 s. Take clean ones: the regression test writes the captions to
  `artifacts/graph-material-render-tests/barrel-{512,1024}-baked.txt` (not collected by the runner's artifacts, which take
  PNGs only), so read them from a failing assert message or a local run's artifacts folder.
- `cairn task T-163 verify`, `### As built` (fold this section in), commit, `cairn lint`, `finish`.

**Not checked.** The 512 MB cap's eviction (no test); a kept level or part tree drawn with a graph material; cancellation
mid-bake; frames by eye.

---
description: >-
  A missing graph file lists the .texgraph files in its folder, a graph fault names its node, an unknown output lists the outputs, and check warns of every graph material that cannot be baked
---
# T-166 — Graph material errors and check

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-163

**Verify:** core-unit, graphs-unit, server-unit

## Goal

Whatever is wrong with a graph material, the agent is told what and where in words it can act on, at the first call
that could know: `define_material` when it is defined, `open` and `check` at any time, `render` and `export` when the
bake fails. No call draws or writes a stale or blank texture in place of an error.

## Scope

- **One wording, in `GraphSlotProblem`** (T-163's helper, owned by this task from here), used by every tool. Each
  message starts with the argument or `materials['<name>'].<slot>` and gives the cause:
  - **a missing graph file**: `no file at <path>; the .texgraph files in <folder> (3) are: iron.texgraph, ...`, the
    missing-texture error's shape (T-81, `DefineMaterialTool.Missing`), with its "folder does not exist", "no
    .texgraph files" and "and N more" cases;
  - **a nested graph missing**: the same, naming the `graph` node and the file that uses it (`Patina.Graphs`' fault,
    as it stands);
  - **a graph fault**: `Patina.Graphs`' fault text unchanged, which names the node and its port or parameter
    (`nodes['wear'].params['width']: is 0, but must be a number from ...`), every fault listed, not the first;
  - **an unknown output**: `the graph has no output 'wera'; its outputs are base_colour, metallic, roughness, wear`;
  - **an output the slot cannot take** (T-163's slot table): what the slot takes and what the output carries;
  - **a mesh node that cannot be fed**, naming the node: the mesh has no UVs (`BakeLayout.NoUvsMessage`), overlapping
    UVs with the faces named (`BakeLayout.OverlapMessage`), or `mesh_mask` names a group or material the mesh does not
    have, with the mesh's groups and materials listed. Each names the mesh.
- **`define_material`** validates each graph slot fully when it is called (the file, its faults, the output, the slot
  table) and refuses with everything that is wrong, changing nothing. It cannot check mesh nodes against a mesh, since a
  material has none until `set_material`; it says in its result when a slot reads the mesh.
- **`set_material`** warns, without refusing, when it puts a mesh-reading material on a mesh that cannot feed it (no
  UVs on the faces, overlapping UVs, a `mesh_mask` name the mesh lacks).
- **`open`** warns for each graph slot that cannot be read or validated, and still opens (T-160).
- **`check`** gains material warnings, by material as its occlusion warning is (`materials['weathered-iron']`): each
  cause above that holds for the model as it is now, per mesh that uses the material; and
  `faces[..] of 'barrel' are unmapped, so 'weathered-iron' bakes nothing onto them`. `check` validates and builds
  layouts but casts no rays and evaluates no graph, so it stays fast: a test holds it under 1 s on the raised-hoop
  barrel. Its description lists each new warning with what to do about it.
- **`render` and `export`** fail with the same messages, prefixed by the material and the mesh, when a bake fails after
  the material was defined (a file deleted or broken since, UVs made to overlap). The error says the model is
  unchanged and that the material can be defined again or the file restored.
- **Tests** (`server-unit`, `ToolHarness`): a table with one row per cause and per tool that reports it, each asserting
  the whole message; the listing's four cases; two faults reported together; a graph broken on disk between two
  renders; `check` silent on a healthy graph material and on every committed sample; every worked example in the
  changed descriptions run as written.

## Notes

- **From the milestone:** a missing graph file lists the graph files in its folder, as a missing texture does (T-81),
  and a graph error names the node.
- **The orchestrator's calls, each a stop if it proves wrong:** `set_material` and `open` warn rather than refuse;
  `render` of a model with one unbakeable graph material fails whole rather than drawing the rest; `check` casts no
  rays.
- Sonnet. Runs beside T-164 and T-165: it owns `GraphSlotProblem`, `MaterialTools.cs`, `CheckTool.cs`,
  `MeshHealth.cs` and `OpenTool.cs`, and touches neither the export files nor `RenderTool.cs` (the render and export
  paths already route through the helper).
- **Pins:** the tools/list digests move with the descriptions; re-take only those, and say which in As built.
- **Write down for T-167 and T-168** (As built): the table of causes and messages, as the tests hold it.
- **Stop and ask:** a cause the list above does not cover; a message that would need `Patina.Graphs`' fault text
  changed (M-28's tests hold it).

### As built

Sonnet 5.5 (`claude-sonnet-5-5`) ran both agents (the first to its 60-call checkpoint, this one to finish).

**Built.** `GraphSlotBake.Validate` (file, every fault, output, slot table; `GraphMissingException` and `GraphOutputException` joined `GraphSlotException`) and `MaskNames` in `Carve.Graphs`. `GraphSlotProblem` is the one wording: `Of(material, slot, e)` gives `materials['m'].<slot>: <cause>`, `Cause`, `Failed` (the render/export tail), `Slots`, `OnMesh` and `Missing` (moved from `DefineMaterialTool`). `define_material` validates fully and says when a slot reads the mesh; `set_material` and `check` (`GraphMaterialWarnings` in `CheckTool.cs`, no rays, no evaluation) warn per mesh; `open` warns through `BrokenGraphs`. Render and export both catch `GraphSlotProblemException` from `GraphBakes.Resolve`, so export carries render's wording (test `export_fails_with_the_words_render_uses_when_the_graph_has_gone`). The slow-bake warning names the slot and node costing most. Rebased onto T-164: kept its removal of the interim refusal (no "not drawn" sentence remains) and this member's description changes.

**Causes and messages** (held whole by `GraphMaterialErrorsTests`, `GraphSlotToolTests`, `GraphBakesTests`; `<root>` is the folder):

| Cause | Message (after `materials['m'].<slot>: `; define_material says `<slot>.graph:` or `<slot>.output:` instead) |
|---|---|
| graph file missing | `no file at <root>\good.texgraph; the .texgraph files in <root> (N) are: a, b` / `there are no .texgraph files in <root>` / `the folder <root> does not exist` / `..., and N more` |
| nested graph missing | `the graph has faults: node 'g' parameter 'path': no file at <root>\gone.texgraph; the .texgraph files in <root> (5) are: ...` |
| graph fault(s), all listed | `the graph has faults: node 'a' input 'in': output needs it connected, and it is not; node 'b' input 'in': ...` |
| unknown output | `the graph has no output 'wera'; its outputs are base_colour, roughness` |
| grey into a colour slot / colour into a grey slot | `'occlusion' is a grey output; normal_texture takes the 'normal' output or a colour one` / `'base_colour' is a colour output; occlusion_texture takes a grey one` |
| metallic_roughness | `metallic_roughness_texture takes the 'roughness' output (with 'metallic' packed in B), not 'base_colour'` |
| no UVs (check, set_material; render/export) | `mesh 'strip': the mesh has no UVs, so it has nothing to bake into; give it UVs first (unwrap, project_uvs or set_uvs)` |
| overlapping UVs | `mesh 'strip': the UVs overlap, so a texel would show two faces: faces[0] and faces[1]; move the islands apart (unwrap or set_uvs) and bake again` |
| mesh_mask names nothing | `nodes['k']: mesh 'strip' has no group 'rim'; its groups are none and its materials 'm'` |
| unmapped faces (check, set_material, no slot) | `materials['m']: faces[2, 3] of 'strip' are unmapped, so 'm' bakes nothing onto them; give them UVs with project_uvs, unwrap or set_uvs` |
| render/export, after the fact | the cause above, then `; the model is unchanged: define the material again with a graph that bakes, or restore the file` |
| open | the cause above, then `; the slot is kept, and render and export fail on it until the file is there and right, or define_material names another` |

**Tools/list digest** (`PartToolsListTests.Before`): the rebase conflicted with T-164's value; both sides' text kept, rebuilt, retaken as `58132D06...C1D5`. A diff of the Server sources' descriptions against `origin/milestone/M-29` shows only this member's changes: `define_material` (the errors sentence), `set_material` (one sentence), `check` (the graph warnings paragraph), `open` (the graph-warnings sentence). Estimated description lengths from the string literals, on `origin/milestone/M-29` and now: `define_material` about 3,555 and 3,653 (under the 4,096 cut), `check` about 5,980 and 6,725, `open` about 5,560 and 5,620 (the last two over the cut already; the first agent's 8,310 for `check` was not reproduced); `set_material` is well under. Not measured at run time.

**For T-165.** `GraphSlotProblem.Of(<graph file name>, "image", e)` still compiles and works, but reads `materials['<file>'].image: <cause>`, which is wrong for an `image` that belongs to no material; I left T-165's call site alone. A word like `image: <cause>` (a `Of` overload with no material) would read right.

**Checked.** `server-unit` (core-unit via verify); `check` silent on every committed sample (none has a graph slot) and under 1 s on the barrel with `weathered-iron` on every face; a graph broken or deleted between two bakes. **Not checked.** Descriptions' worked examples were not run as written (the descriptions carry no new worked example beyond the error tables); the mesh causes on a kept part level; a nested-graph `mesh_mask`; no timing on a named model beyond the barrel above.

**README.** Nothing needed to change: line 147 describes graph slots and says nothing of their errors, which the tool results carry.

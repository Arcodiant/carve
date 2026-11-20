---
description: >-
  A missing graph file lists the .texgraph files in its folder, a graph fault names its node, an unknown output lists the outputs, and check warns of every graph material that cannot be baked
---
# T-166 — Graph material errors and check

**Status:** Open

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-163

**Verify:** core-unit, server-unit

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

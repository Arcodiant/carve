---
description: >-
  A kernel registry of per-element data layers, each with rules for what happens on create, move and reshape, so every edit carries colours, groups, weights and hard edges without handling each one
---
# T-71 — Attribute layers with interpolation rules

**Status:** Open

**Pri:** P1

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** T-42

## Goal

Every topology edit carries a mesh's data (colours, groups, weights, hard edges, and later UVs and seams) through one kernel mechanism. Subdivide, the M-5 cuts and M-6's layers then add a rule, not a special case in each edit.

## Scope

- **Core:** a layer registry in `Carve.Core`. A layer has a name, an element kind (vertex, edge as a vertex pair, face, or face corner), a value type, a default, and its rules:
  - **create from sources:** a new element gets its value from the elements it was made from. Typical rules are lerp, copy from a source face, inherit from the split parent edge, and the weights rule (blend, keep the top four, normalise, as `VertexWeights` does);
  - **move:** what a vertex's corner and vertex values do when its position changes;
  - **reshape:** what happens when a face's loop changes, and when an edge-keyed value's vertex pair ends up between different faces.

  The rules are deterministic, with a fixed order for ties and no `System.Random`.
- **Edits take provenance:** creating vertices and faces in `MeshEdits` says which element(s) each new one comes from, and the registry fills in every layer. `MeshEdits` does not reference `FaceStyle` or `VertexWeights` today.
- **Move onto it:** face colours and groups (`FaceStyle`), weights (`VertexWeights`), and T-42's hard edges and shading. Their public tool behaviour stays the same.
- **Extrude and move use it.** For example, an extruded wall takes the colour and groups of the face it came from. If that changes what extrude does today, stop and ask before landing it.
- **File:** a round trip of every existing sample and fixture (the leaky shed v1 and v2, `gardener.carve`) is unchanged by content. Add a format change only if one is needed, and keep older versions readable.
- **Tests:** `unit` tests for each rule, including:
  - lerp at the ends and the midpoint;
  - weights capped at four and summing to 1;
  - a split edge inheriting hard;
  - a moved vertex;
  - a reshaped face;
  - determinism.

  Plus the byte-identical guard on every existing frame.

## Notes

- **Decided in the discussion of 2026-11-10, by the person:**
  - a standalone task, not placed in M-18;
  - intended to land before T-43 (subdivide), so subdivide is written against the registry rather than carrying colours, groups, hard edges and weights by hand.
- **Where it came from:** a discussion of agent-defined mesh operations (a stack language over mesh primitives). The person asked whether that adds value, and we decided not to build it now. The layer registry was the part that stands on its own, because T-43, T-65 to T-68 (cuts, merge, bridge) and T-53/T-54 (UVs, seams) all need it.
- **Finding to honour:** a paper run of an agent-written bevel showed that rules on creation alone are not enough. In the bevel, a reused end vertex was moved, and its UVs stayed put and stretched. A hard flag keyed on the bevelled edge's vertex pair stayed on that pair, now between the top face and the chamfer, while the new edge came out soft. The move and reshape rules exist for this, and the same thing will hit a native bevel (M-14).
- **Open rules: stop and ask.** Any rule D-6 does not settle (the hard-edge outcome under reshape above is one), any change to visible behaviour of an existing tool, any loosened check.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; each new check shown failing, as a table.
- Run `finish` from inside the worktree and leave the folder.

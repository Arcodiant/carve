---
description: >-
  A kernel registry of per-element data layers, each with rules for what happens on create, move and reshape, so every edit carries colours, groups, weights and hard edges without handling each one
---
# T-71 — Attribute layers with interpolation rules

**Status:** In Progress

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

### As built

Run on Opus 5.5.

**What was built (all in `Carve.Core`, public):**

- `AttributeLayer` and four typed layers, `VertexLayer<T>`, `EdgeLayer<T>` (keyed by the vertex pair, lower id first), `FaceLayer<T>` and `CornerLayer<T>` (a face has a value at every corner or none). Each has a name, an element kind (`LayerElement`), a value type, a default, a reader and a writer over the storage the mesh already holds, and its rules as delegates: create (vertex, face), split (edge), move (vertex, corner), reshape (edge, face), and for corners a combine (the lerp) and an agreement comparer for merges. `Carried(source, edited, provenance)` gives a layer's values without writing them, which is how the tests drive a layer of their own.
- `EditProvenance`: what an edit says its elements came from. A `VertexOrigin` is `Blend` (a stencil over source vertices) or `Offset` (one source vertex, displaced). A `FaceOrigin` names the source face and a `CornerOriginKind`: `Position`, `Vertex` (a cut), `Stencils` (subdivide), `Agree` (merge) or `None` (walls, quads). Split edges carry their chains.
- `Stencil`, made public out of `Subdivision.Stencil`, with a `Divisor`, so subdivide's mean and midpoints sum first and then divide, as they did.
- `LayerRegistry`, immutable, with `Standard` holding colour, material, groups, weights, hard, uvs and seams, in that order. `With` appends a layer and refuses a duplicate name. `Carry` runs the layers in order.
- `LayerRules`: `Keep` (every move and reshape), `CopyFromSourceFace`, `InheritFromSplitEdge`, `NoValue`, `Lerp`, `BlendWeights` (blend, keep the top four with ties to the lower bone, normalise), `ReblendWeights` and `SameBits`.
- `StandardLayers`: the seven layers on `FaceStyle`, `VertexWeights`, `MeshShade` (the shading mode is per mesh and carries with the hard layer) and `MeshUvs`.

**Edits now on it:** move, extrude, subdivide (every level), `FaceSplit` (so plane cut, loop cut and knife), merge, bridge and delete. Each edit builds an `EditProvenance` and goes through `EditResult.From(..., provenance)` or `LayerRegistry.Standard.Carry`. These per-edit copies are gone: `EditResult.Carried*`, `FaceSplit.Carry*`, the attribute half of `Subdivision.Once`, `FaceStyle.Carry` and `Without`, `MeshUvs.Without` and `VertexWeights.Without`. `MeshEdits` names neither `FaceStyle` nor `VertexWeights`. Plane cut's hard cap rims are still the cut's own mark, made after the carry.

**For the tasks after this one:** a new edit says where its elements came from (`new EditProvenance().Vertex(...).Face(...).Split(...)`) and passes that to `EditResult.From`. A new per-element attribute (M-6's layers, a native bevel's UV rule) is a new `*Layer<T>` with its rules, added to `LayerRegistry.Standard`, plus its storage on the mesh. No edit changes. Subdivide's `Level` now carries a `PolygonMesh` holding every layer.

**Decisions the Scope left open:**

1. (The person, 2026-11-18.) An extrude's ring vertex is declared `Offset` from the corner below it, and the weights rule gives an offset nothing, so the ring stays unweighted, as before.
2. (The person, 2026-11-18.) The hard-edge outcome under reshape, the bevel finding: a mark stays on its vertex pair while the pair is an edge, whatever faces it now lies between, and the cap's new rim edge is soft, as before. `EdgeLayer.ReshapeRule` receives the faces before and after, so a later rule (M-14's bevel) can choose otherwise.
3. (Mine, no visible change.) A moved vertex keeps its corner UVs (`LayerRules.Keep`), so the stretched-UV half of the bevel finding is still open; `CornerLayer.MoveRule` is the hook for it. A group left with no face goes, which matches the old behaviour because a group can never be defined empty.
4. (Mine.) No file format change: the layers read and write the storage that already existed.

**Tests:** `AttributeLayerTests` (21, `unit`). They cover lerp at the ends, the midpoint and over a divisor, and a cut lerping UVs; weights capped at four and summing to 1, a tie for fourth going to the lower bone, an unweighted source, a cut blending by fraction, and the extrude ruling; a split hard edge inheriting to its pieces, the split rule's order, and subdivide's halves; a moved vertex keeping its weights and UVs, and the move rules running only for moved vertices and corners; a reshaped face (the cap's look and UVs, the walls' look and groups unmapped), the reshape rule running only for the reshaped face, the hard-rim bevel case, merge agreement, a changed corner count, and deletion; the registry order and the duplicate refusal; and determinism (the same carry twice, the provenance in reverse order, and through the edit, all equal). The existing 1030 core tests pass unchanged, and so do the round-trip tests of the samples and fixtures (leaky shed v1 and v2, gardener and the rest), since `core-unit`, `server-unit` and every scenario suite pass.

New checks shown failing (each mutation applied, the tests run, then reverted):

| Mutation | Caught by |
|---|---|
| `Lerp` ignores the divisor | Lerp_over_a_divisor_sums_first_then_divides_as_a_mean_does |
| weights not capped at four | Blended_weights_keep_the_top_four_and_sum_to_one; ..._break_a_tie_for_fourth_to_the_lower_bone |
| a tie for fourth goes to the higher bone | Blended_weights_break_a_tie_for_fourth_to_the_lower_bone |
| split-edge pieces do not inherit | Each_piece_of_a_split_hard_edge_is_hard...; Subdivide_carries_a_hard_edge_to_both_halves; Carrying_is_deterministic... |
| move rule run for every vertex | The_move_rules_run_only_for_a_vertex_that_moved |
| reshape rule never run | A_reshaped_face_runs_the_reshape_rule_and_an_untouched_face_does_not |
| offset ring vertex blended | An_extrudes_ring_vertex_is_an_offset_and_starts_unweighted |
| merge ignores disagreement | A_merged_face_keeps_corner_uvs_only_where_its_faces_agree |
| split chain given from the higher end not reversed | The_split_rule_sees_the_parent_and_each_piece_in_order |

**Byte-identical guard.** The 13 scenarios (cube, crate, rig, shed, cuts, sky, uvs, export, gatehouse, character, soft, materials, orcward) were run by hand on this machine (Xellotath) twice. The first run was at the merge base `5a6d61d`, exported with `git archive` to a temp folder; the second was on this task's tree. Every output was hashed. The queue's frames could not be paired by job for both commits, so both sides were run on one GPU. Results: all 456 frames, 21 exported `.glb`, 7 saved `.carve` and 3 `.tsv` files are byte-identical. The 11 `export-*.txt` messages differ only in the absolute run-folder path they print. `cairn task T-71 verify` passed every suite (core-unit, server-unit, server-regression, every scenario suite, and viewer-cube carried).

**Not verified:** the frames were compared by hash only, so none was opened; nothing can be seen to have changed. The viewer steps of the export and materials suites ran in verify, not in the hash guard. The provenance of the plane cut's caps and of loop cut and knife is checked only through `FaceSplit` and the existing tests of those tools.

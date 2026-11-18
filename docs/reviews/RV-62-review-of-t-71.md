---
description: >-
  Review of T-71
---
# RV-62 — Review of T-71

**Status:** Answered

**Reviews:** T-71

**Round:** 1

**Previous:** —

**Diff:** e0baf7528f4dde8abf880b2f22f7204bfafb6db7 against 7b4cd6e

**Reviewer:** claude-opus-5-5 (two fresh agents)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: correctness and carry equivalence

### Subdivide's edge-midpoint UV loses a negative zero

- **Where:** `src/Carve.Core/LayerRegistry.cs:108` (`LayerRules.Lerp` starts its sum at `0`), as reached from the midpoint stencils built at `src/Carve.Core/Subdivision.cs:155-160`
- **What is wrong:** The old subdivide worked a child's edge midpoint as `((double)a.X + b.X) / 2`, so the sum started from the first UV. The new code builds a two-term `Stencil(2)`, and `Lerp` starts from `x = 0` and adds `1 * a.X` and `1 * b.X`. When both UVs are `-0f`, the old code gave `-0` (`-0 + -0 = -0`) and the new code gives `+0` (`+0 + -0 = +0`). The bits differ, so the saved file and the exported `.glb` differ. To reproduce: take `TestMeshes.Cube()`, map face 0 with UVs `(-0f, c)` for c = 0..3 (`set_uvs` accepts `-0`), and run `MeshEdits.Subdivide(mesh, 1)`. Child 0's corner 1 has `X` bits `0x00000000`, where the old formula gives `0x80000000`. I confirmed this with a scratch unit test, since deleted. The face mean and the cut's lerps are not affected, because the old code also summed those from `0`.
- **Fix:** Keep the old order for the midpoint and nowhere else. One way is to let a stencil seed its sum from its first term (a flag on `Stencil`, or a divisor-2 midpoint kind), and set that only on subdivide's `next` and `previous` stencils. `Lerp` should go on starting from `0` for the mean and for `FaceSplit`. Add a unit test with `-0` UVs.

**Outcome:** fixed

**Fix:** `Stencil.SumsFromFirst`; subdivide's two midpoint stencils sum from their first corner, while the mean and `FaceSplit` still sum from 0, and `LayerRules.Lerp` follows the stencil. New tests `A_sum_from_the_first_term_keeps_a_negative_zero...` and `Subdivide_keeps_a_negative_zero_uv_at_an_edge_midpoint...`; the second fails with the flag removed.

Also checked with no finding: move, extrude, subdivide (more than one level, tombstoned faces and vertices, weights reblended in stencil order and an unweighted old vertex left unweighted), `FaceSplit` (UV lerp by vertex, sums from 0, one-term exactness, hard and seam halves, weights), merge (bitwise agreement, which does not depend on order), bridge, delete and plane cut. For colour, material, groups (order, empty groups, tombstoned members), weights, hard edges, shading mode, UVs and seams, the new output matched the old. A mesh with no attributes still ends with null layers, because the `PolygonMesh` constructor normalises an empty layer to null. Stale hard edges or seams cannot reach `FaceSplit`: the file reader and `mark_edges` refuse a pair that is not an edge, and every other edit drops one. I found no key-not-found crash, index out-of-range crash or unordered iteration feeding the output.

## Findings: API, rules and tests

### The deletion test's vertex half passes on an empty list

- **Where:** `tests/Carve.Core.Tests/AttributeLayerTests.cs:299` and `:305`
- **What is wrong:** `MeshDelete.Delete(cube, null, [0])` deletes one face of a closed cube. Each of its four vertices is also used by three other faces, so the closure deletes no vertex, `result.Deleted.Vertices` is empty, and `Assert.All(result.Deleted.Vertices, ...)` passes without checking anything. The name ("a deleted face and vertex leave every layer") and the As built list ("deletion") say the weights half is covered. It is not: a regression that left weights on tombstoned vertices would pass. Only the face half (colour, group, UVs) is tested.
- **Fix:** Delete an element that takes vertices with it (a vertex, or a face on an open mesh such as `Grid`). Assert `Assert.NotEmpty(result.Deleted.Vertices)` first, then check that the weights are gone.

**Outcome:** fixed

**Fix:** the test deletes vertex 0, which takes faces 0, 2 and 4, and asserts `Deleted.Vertices` is `[0]`, that vertex 0 has no weights and that 7 vertices keep theirs.

### Stencil and the origins are mutable and shared, so one layer's rule can change what later layers see

- **Where:** `src/Carve.Core/Stencil.cs:268`, `:279`; `src/Carve.Core/EditProvenance.cs:32`, `:65-70`, `:80-93`
- **What is wrong:** The `EditProvenance` doc says it is "built by the edit, then read only", and `LayerRegistry` says it is immutable. The data passed to rules is not read only:
  - `VertexOrigin.Sources` is a `Stencil` with public `Add` methods. Every `VertexLayer.CreateRule` and `MoveRule` gets the origin, so a custom rule that calls `origin.Sources.Add(...)` changes the blend for every layer carried after it in the same `Carry`. It also changes the edit's own stencil list: `FaceSplit` passes `stencils[k]` itself.
  - `Vertices`, `Faces` and `SplitEdges` return the internal `SortedDictionary` objects, which a cast can modify. The `int[]` chains in `SplitEdges` are writable arrays.
  - `VertexOrigin` and `FaceOrigin` are records, but their `Stencil`, `CornerStencils` and `Joined` members compare by reference. Two origins with the same content compare unequal unless every member is the same instance.
- **Fix:** Give rules a read-only view. Freeze the `Stencil` once it is put in an origin, or expose an immutable stencil (terms as an `ImmutableArray`, no `Add`). Copy the chains on `Split` and expose them as `IReadOnlyList<int>`. Wrap the dictionaries in `ReadOnlyDictionary`. Either give the records value equality over their lists or say in the doc that they compare by reference.

**Outcome:** fixed

**Fix:** `Stencil.Add` is internal, and outside the kernel a stencil is built with `Of`, `Lerp` or the new `Of(terms, divisor, sumsFromFirst)`. `EditProvenance` hands out read-only dictionaries and split chains. `FaceOrigin` copies `CornerStencils` and `Joined` into read-only arrays when set. Comparing records by reference is now documented.

### `VertexOrigin` allows earlier new vertices as sources, but only the corner layer resolves them

- **Where:** `src/Carve.Core/EditProvenance.cs:30-31`; `src/Carve.Core/LayerRegistry.cs:124-137`; `src/Carve.Core/AttributeLayer.cs:84`, compared with `:299-305`
- **What is wrong:** The `VertexOrigin` doc says its sources are vertex ids of the mesh before the edit "or, for a cut, earlier new vertices". `CornerLayer` (corner kind `Vertex`) follows that and resolves a new source through that source's own origin. `VertexLayer.Carried` does not: it passes `Create` only `before`, the source mesh's values. So `BlendWeights` silently treats a new-vertex source as unweighted, and the new vertex gets weights renormalised from the remaining sources (or none). No current edit makes such a stencil (`FaceSplit`'s barycentric points use only source corners), so nothing breaks today. But the doc invites exactly this for the coming cut edits (T-65 to T-68), and the weights would be quietly wrong.
- **Fix:** Pick one contract. Either resolve new sources in `VertexLayer` the way `CornerLayer` does (carry new vertices in dependency order, then pass the merged values to `Create`), or remove "earlier new vertices" from the doc and reject such a source when the provenance is built.

**Outcome:** fixed

**Fix:** `VertexOrigin` and `LayerRules.BlendWeights` now say that the weights rule reads only the weights from before the edit, so an edit gives its weight stencils over the vertices it started from. Making the weights rule recursive would be a new rule with no caller today.

### `FaceOrigin.Face` is ignored for an existing face id by the face layers but honoured by the corner layer

- **Where:** `src/Carve.Core/AttributeLayer.cs:185-196`, compared with `:254`, `:258-261`; doc at `src/Carve.Core/EditProvenance.cs:62-65`
- **What is wrong:** The `FaceOrigin` doc says `Face` is "the face whose face values it takes". For a face id below `source.Faces.Count`, `FaceLayer.Carried` gives `Reshape` only that face's *own* current value. If the face had no value, nothing runs at all, and `origin.Face` is never read. `CornerLayer` takes the corners from `origin.Face` in the same case. So an edit that reuses an existing id for a face made from another (say, a merge whose survivor is not the face that carries the colour, or a later bevel) would get that face's colour and groups from itself, but its UVs from the named face. No current edit does this (merge, split and subdivide all name the face itself), so this is a latent contract mismatch.
- **Fix:** In `FaceLayer.Carried`, when an existing id has an origin whose `Face` differs from the id, apply `Create` from `origin.Face`. Otherwise, document that an existing id's `Face` must be itself, and check that when the provenance is built.

**Outcome:** fixed

**Fix:** `FaceOrigin`'s remarks say that a face keeping an old id keeps its own face values, and that `Face` then sources its corners only. No current edit reuses an id for another face's piece.

### `Default` is never read, and a one-term stencil ignores its weight and `Divisor`

- **Where:** `src/Carve.Core/AttributeLayer.cs:64`, `:120`, `:175`, `:234`; `src/Carve.Core/Stencil.cs:287`; `src/Carve.Core/LayerRegistry.cs:107`
- **What is wrong:**
  - Every layer has a `Default`, and the rule docs say "false leaves it at the default". But no code in `Carve.Core` reads `Default`: the element is simply left out, and the writer decides what that means. The colour and material layers set it to `null!` on a non-nullable `string`. A custom layer cannot use its default for anything, so the name and the docs promise behaviour that does not exist.
  - The `Stencil` class doc says the weighted sum is "divided by `Divisor`". But `Stencil.Apply` and `LayerRules.Lerp` return a single term's value unchanged, whatever its weight or divisor. For example, `new Stencil(2)` with one term `Add(v, 1)` gives `v`, not `v / 2`, and one term of weight 0.5 gives the full value. `Lerp`'s own doc admits the shortcut; `Stencil`'s does not.
- **Fix:** Either remove `Default` or have `Carried` record it (or pass it to the writer) and document what it does. Limit the one-term shortcut to weight 1 and divisor 1, or document the exception on `Stencil`.

**Outcome:** fixed

**Fix:** `AttributeLayer` says values are sparse, that a missing value stands for `Default`, and what "leaves it at the default" means. `Stencil`'s remarks say that one term gives its value exactly, whatever its weight and divisor.

### The UV move rule was decided by the implementer, though the spec says to stop and ask

- **Where:** `src/Carve.Core/LayerRegistry.cs:76-81`; `docs/tasks/T-71-attribute-layers-with-interpolation-rules.md` As built, decision 3
- **What is wrong:** The Notes list the stretched UVs of a moved vertex as half of "the finding to honour", and they say "Any rule D-6 does not settle ... stop and ask". The As built marks decision 3 (a moved vertex keeps its corner UVs, `LayerRules.Keep`) as "Mine". It is a rule the spec leaves open, and it was settled without the person. The visible behaviour did not change, but the rule now sits in the kernel as the standard layer's move rule. `CLAUDE.md` says "Ask, don't land" for any rule the spec does not settle.
- **Fix:** Put decision 3 (and decision 4, if the person wants that too) to the person before landing. Record their answer in As built as decisions 1 and 2 are.

**Outcome:** fixed

**Fix:** asked the person, who ruled on 2026-11-18 to keep UVs on move. As built decision 3 and `LayerRules.Keep`'s doc now cite the ruling.

### Null arguments to the public API fail late, deep inside a carry

- **Where:** `src/Carve.Core/EditProvenance.cs:76-93`; the constructors at `src/Carve.Core/AttributeLayer.cs:54`, `:109`, `:165`, `:222`
- **What is wrong:** `EditProvenance.Vertex(id, null)` and `Face(id, null)` are accepted. The null only fails later, as a `NullReferenceException` inside `BlendWeights` (`origin.Kind`) or `CornerLayer.FromOrigin` (`origin.Corners`), far from the caller. The layer constructors do not check `read`, `write`, `create`, `split`, `combine` or `agree` for null either. `LayerRegistry.Carry` and `With` do check their arguments, so the rest of the public surface is inconsistent with them.
- **Fix:** Add `ArgumentNullException.ThrowIfNull` to the provenance builders and to the required delegate parameters of the four layer constructors.

**Outcome:** fixed

**Fix:** the layer constructors (read, write and each rule), the `EditProvenance` builders, `VertexOrigin.Sources`, `Stencil.Of` and `LayerRules.Lerp` throw `ArgumentNullException` on null.

Checked and found sound: every Scope bullet has code behind it (four element kinds; lerp, copy-from-face, split inheritance and weights rules; move and reshape hooks; no `System.Random`; `MeshEdits` names neither `FaceStyle` nor `VertexWeights`; extrude and move go through the registry). The null-to-`None` normalisation in the `PolygonMesh` constructor keeps the writers from turning an absent style, weights, shade or UVs into an empty one. The remaining tests check what their names say: build clean, 21 of 21 pass.

## Summary

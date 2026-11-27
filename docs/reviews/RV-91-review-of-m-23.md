---
description: >-
  Review of M-23
---
# RV-91 — Review of M-23

**Status:** Open

**Reviews:** M-23

**Round:** 1

**Previous:** —

**Diff:** e5c020d31d15002c34ee2bae0cc9e4f918839e0d against origin/main

**Reviewer:** claude-opus-5-5 x3 (grouping and smooth rule; verbosity, help and unwrap; the whole diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: grouping and the smooth rule, T-197 and T-198 (claude-opus-5-5)

### Export drops a flat mesh's not-planar warnings when another mesh in the model is smooth

`src/Carve.Server/ExportTool.cs:181` and `:362` (Static and Tree build their warnings from `MeshReport.Create(model.Combined)`), with `src/Carve.Core/MeshReport.cs:115` and `src/Carve.Core/Model.cs:691`/`:734`. Rubric 1 (Correctness). Severity: Major. Ruling 2 says "a flat-shaded mesh warns as before". But `Combined` takes `ShadingMode.Smooth` if any occurrence is smooth (only `FlatFaces` says which faces are flat), and `MeshReport` decides by `mesh.ShadingMode` for the whole joined mesh. So the flat mesh's not-planar faces are moved out of `Warnings`. Failing case: with no skeleton, `define_mesh` a mesh `A` of four lifted quads (flat), `define_mesh` a mesh `B`, `set_shading {"mesh":"B","shading":"smooth"}`, then `export`. The result has no not-planar warning, while `check {"mesh":"A"}` warns of four. Before T-198 the export warned. The skinned path (`:302`, which reads per mesh) is right, so the two export paths now disagree. `SmoothNotPlanarToolTests` uses only one-mesh models, so nothing catches this. A fix: decide per face from `FlatFaces` when it is set, or build the static warnings per mesh as `Skinned` does.

**Outcome:** —

### A UV-overlap group counts and lists only the lower-id face of each pair, and crosses 3/4 on warnings, not faces

`src/Carve.Core/MeshWarningGroups.cs:27` and `:41`-`:43`, with `src/Carve.Core/MeshUvHealth.cs:70`. Rubric 1 and 2. Severity: Major. `MeshUvHealth` warns once per overlapping pair, on `faces[ids[i]]`, the lower id. `Lines` decides whether to group by the number of warnings, while the line counts distinct `Element`s. The overlapping partners are never counted. Failing cases:
- `faces[0]` overlaps `faces[1]` to `faces[4]` (same material). That is 4 warnings, so the kind groups to `1 faces have UVs that overlap another face: e.g. faces[0]`. The count is 1 where five faces overlap, the plural is wrong, and the four partners named by the old per-line output are gone. By the ruling's 1-to-3 rule a kind about 1 face should be listed.
- The pairs (0,1), (2,3), (4,5), (6,7) give `4 faces ...` where 8 overlap.

The pin `MeshWarningGroupsTests.A_face_warned_twice_counts_once` (`tests/Carve.Core.Tests/MeshWarningGroupsTests.cs:77`) fixes the wrong number in place. Faces 3, 7, 12, 20 and 21 overlap, and it expects `4 faces`, leaving out `faces[21]`. A fix is to count, sample and threshold on the union of both faces of each pair, or at least to threshold on distinct elements.

**Outcome:** —

### The smooth-mesh note says "1 faces are not planar"

`src/Carve.Core/MeshWarningGroups.cs:34` and `:43`. Rubric 1. Severity: Minor. `SmoothNote` uses the grouped line "at any count" (T-198's As built: "N faces, even N = 1 or 2"), and `Says` has only plural nouns and verbs. Failing case: a smooth mesh with one lifted quad gives the note `1 faces are not planar: worst faces[0], ...; e.g. faces[0]; select faces where non_planar names them; expected on a smooth mesh, which export triangulates`. With 2 faces, `e.g.` names only two. The plural is the builder's choice, not one of the person's rulings. The same plural bug reaches `Lines` through the UV-overlap case above.

**Outcome:** —

### The per-mesh grouped line of a skinned export lost its only pin on real output

`src/Carve.Server/ExportTool.cs:302`, `scenarios/Carve.Scenario.Soft/SoftScenario.cs` and `scenarios/Carve.Scenario.Morph/ArmStages.cs`. Rubric 2 and 6 (a check loosened while moving). Severity: Minor. Before, Soft and Morph ran `NotPlanarFold.Check` on the real skinned export, with its `mesh 'Name': ` prefix. Both models are now smooth, so they run only `CheckSmoothExport`, which asserts that there is no warning. `NotPlanarFold.Check` and `CheckExport` are now called only by `NotPlanarFoldCheckTests`, on hand-written strings. `NotPlanarFoldToolTests` and `SmoothNotPlanarToolTests` export without a skeleton, and `SubdivideToolTests`' skinned export asserts no warning text. Failing case: changing line 302 to drop the prefix, or to pass `Verbosity.Full`, fails no test.

**Outcome:** —

### The export's "N warning(s)" counts lines, and the grouped line contains the "; " that separates them

`src/Carve.Server/MeshToolSupport.cs:26`. Rubric 1 (a reader that still expects one line per warning). Severity: Nit. `WarningsSuffix` joins the warnings with "; ", and a grouped line holds two or three "; " of its own. Failing case: a flat export with one limit warning and 252 not-planar faces reads `2 warning(s): <limit>; 252 faces are not planar: worst ...; e.g. ...; select faces where non_planar names them`. Splitting it on "; " gives 4 pieces where the result says 2. `NotPlanarFold` needed a regex and marker to get around this, which shows the cost. No other reader in the repository splits the text this way.

**Outcome:** —

## Findings: verbosity, help and unwrap, T-199 to T-202 and T-206 (claude-opus-5-5)

### `full` is checked on 4 of the 35 tools; a tool that drops its level passes every test (Medium)

tests/Carve.Server.Tests/VerbosityToolTests.cs:137-145; tests/Carve.Server.Tests/DenseRunProcessTests.cs:93-100. Rubric 2 (tests cover the Scope: T-200's "`full`: every `MeshWarning` one per line", T-201's "`full` is `summary` unless the tool shortens a list"). The theory only looks at `full` when `full.Text != plain.Text`. Thirty of its 34 rows start from the warning-free cube, so for them `full` equals `summary` whether or not the tool passes its level. The dense run checks `full` only for the `delete` and `check` keys (`counts`). The facts cover `delete`, `check` and `displace`, and the theory's `subdivide` row shows a difference. Nothing covers `open`, `undo`, `define_mesh`, `move`, `merge_faces`, `export` (Static, Tree, Skinned, Piece), and so on. `Report(..., Verbosity level = Verbosity.Summary)` (MeshToolSupport.cs:212), `Edit(..., level = Summary)` (:231) and `Skinned(..., level = Summary)` (ExportTool.cs:261) all default to `summary`, so a dropped argument still compiles. Failing case: change UndoTool's `Report(model.Meshes[0].Mesh, level)` to `Report(model.Meshes[0].Mesh)`, or ExportTool.cs's `Skinned(..., level: level)` to `Skinned(...)`. `undo` or `export` at `full` on the 798-open-edge cage then still returns the grouped "798 edges belong to only one face: ..." line, and every test still passes. The code itself passes the level on every path I read, so the gap is in the tests only.

**Outcome:** —

### The warnings topic puts MaterialNoEffect under "only for a mesh that has UVs", and lists it twice (Low)

src/Carve.Server/WarningsTopic.cs:19, :24, :26. Rubric 1 (the topic's text disagrees with what the tools say). Line 19 opens "UV warnings, only for a mesh that has UVs on some face (a mesh with none gets none)", and MaterialNoEffect is listed under it (:24). But MeshHealth.cs:103-108 adds `materialWarnings` to the UV list after `MeshUvHealth.Create`, whether or not the mesh has UVs. Failing case: on define_mesh's cube with no UVs, `define_material {"name":"m","occlusion":0.5}`, then `set_material {"material":"m","faces":[0]}`, then `check`. check warns `materials['m']: occlusion is 0.5 but the material has no occlusion_texture, ...`, on a mesh the topic says gets no such warning. Line 26 then lists "an occlusion strength other than 1 with no occlusion_texture does nothing" as a separate *model* warning "which check adds". It is the same MaterialNoEffect warning, and no model-level occlusion warning exists (`EnvelopeWarnings` and `GraphMaterialWarnings` are the only model ones). Related: line 11 says these are "the same ones define_mesh and the edits report". But the UV kinds and MaterialNoEffect come only from `MeshHealth` (check). `Report` uses `MeshReport.Create`, so define_mesh, the edits and open never carry them, yet open's new pointer calls the topic "every warning in its report". All of this was carried over from check's old description; reading it as a ruling is the person's call.

**Outcome:** —

### unwrap's tests leave T-202's "`full` equal entry for entry" untested, and check `flipped` only as 0 = 0 (Low)

tests/Carve.Server.Tests/UnwrapToolTests.cs:90-121. Rubric 2. T-202's Scope asks for "`full` equal entry for entry to the result before this task". No test reads the fields of an `all` entry (the only reader of `all` is :113, which checks its count and order). If `all` held `largest`'s entries re-sorted, or entries missing `rect`, `stretch` or `flipped`, the test would still pass. The `flipped` total check (:116) compares 0 with a sum of zeros, since every island is a flat quad strip, so `["flipped"] = 0` would pass too. unwrap is also not a row of `VerbosityToolTests`' theory, so its `silent` is never checked for the change being made (only the text is), nor for an error at `silent` coming back whole. Nit in the same test: the comment at :90 ("a strip of 7 two-face blocks and one single, cut by seams") does not match `sizes = [1, 3, 2, 3, 1, 4, 2, 1]`, separate blocks with no seams, and `seams` (:96) is never used.

**Outcome:** —

### help with a non-string topic does not list the topics, unlike T-199's Scope and As built (Low)

src/Carve.Server/HelpTool.cs:76; tests/Carve.Server.Tests/HelpToolTests.cs:148. Rubric 7 (As built matches the diff) and rubric 2. T-199's Scope and its As built both say "Both arguments, a non-string or an unknown topic is a tool error listing the topics". A non-string goes through `Failed`. Failing case: `help {"topic":3}` answers "help failed with 1 problem(s); the current mesh is unchanged:\ntopic: expected a string, got ...", which does not list the topics. The test only asserts `Contains("topic: expected a string")`, so it passes either way. The tool's own description does not promise this, so either the As built or the code needs to change.

**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

### Static export silences a flat mesh's not-planar faces when any other mesh is smooth

`src/Carve.Server/ExportTool.cs:181` and `:208`, `src/Carve.Core/Model.cs:691,734`, `src/Carve.Core/MeshReport.cs` (`if (mesh.ShadingMode == ShadingMode.Flat) warnings.AddRange(nonPlanar)`). Rubric 1 (correctness). Ruling 2 says "a flat-shaded mesh warns as before". A model with no skeleton exports through `Static`/`Tree`, which report on `model.Combined`. `Model.Joined` gives the combined mesh `MeshShade.Of(ShadingMode.Smooth, ...)` when *any* occurrence is smooth, and T-198's rule reads only `mesh.ShadingMode`. So the flat mesh's not-planar faces drop out of export's warnings. Failing case: a model with a smooth `body` and a flat `crate` whose faces[0] has a lifted corner. `check {"mesh":"crate"}` warns "faces[0]: face is not planar ...", but `export` says nothing about it. The skinned path is right, because it reports per piece (`ExportTool.cs:302`). The new description sentence at `ExportTool.cs:96` ("per mesh, ... no not-planar on a smooth mesh") is untrue for this path. It was also untrue before M-23 for the ids, which are the combined mesh's. No test has a mixed-shading static export.

**Outcome:** —

### The grouped UV-overlap line counts only the lower face of each pair

`src/Carve.Core/MeshWarningGroups.cs:27,41`, with `src/Carve.Core/MeshUvHealth.cs:70`. Rubric 1. `MeshUvHealth` files each overlapping pair once, under the lower id. `Lines` groups on the number of *warnings* (`kind.Count`), but `Grouped` counts and samples distinct *elements*. Failing case: faces[0] has UVs that overlap faces[1] to [4]. That is 4 warnings, all `faces[0]`, so they are grouped into "1 faces have UVs that overlap another face: e.g. faces[0]". Five faces overlap, and the line names none of the four others. Listed, the same warnings would have been 4 lines naming every pair. In general the count is "faces that are the lower id of some pair", not the faces that overlap. The milestone sketch's "136 faces have UVs that overlap" and README's "(280 overlaps)" read as overlap counts. `DenseRunProcessTests`' full-level check (`lines >= n`) cannot see this, because pairs are never fewer than lower faces. The grouping reviewer may report this too; it is listed here because it is where the topic's "1 to 3 warnings" (warnings) meets the line's count (elements).

**Outcome:** —

### check's description and its help entry still say no argument but mesh and targets is accepted

`src/Carve.Server/CheckTool.cs:14` (description) and `:29` (`Reference`): "Takes the optional argument mesh ... and the optional targets; any other argument is a tool error". Rubric 1 / docs untrue. `check` now takes `verbosity` (`CheckTool.cs:55`). `undo`'s identical sentence was fixed by T-200 (`UndoTool.cs:14`), but check's was missed. The same `Reference` also gives the result shape as `{ok, problems, warnings:["element: message"], notes, stats}` with no word of `silent`'s `{"ok":...}`. It mentions grouping only under "face is not planar" (`:45`: "past 3 faces it is one line"), which reads as if only that kind groups; open edges, UV overlaps and the other kinds group too. On the known item: the repetition of the topic's wording in `CheckTool.Reference` is not a finding by itself. The entry is check's own reference and carries what the topic does not (thresholds, the overlap tolerance, the seam advice), and the two agree today. The finding is that the entry was not brought up to date where M-23 changed check.

**Outcome:** —

### README says full changes nothing on the twenty tools, but displace groups and full lists

`README.md:150`: "`summary` is the result as it was, and `full` the same, since none of these shortens a list of warnings". Rubric: docs untrue. The list includes `displace`, which returns a mesh report grouped at `summary` (`DisplaceTool.cs`: `Report(edited, level)` and `WarningLines(..., level)`). Its `full` lists every warning, and `VerbosityToolTests.Full_on_displace_lists_every_open_edge` holds exactly that. Failing case: displace on the holed cage. At `summary` it gives one open-edge line; at `full` it gives every edge.

**Outcome:** —

### The Measured table's export row, a Retrospective sentence and the cited TSV do not hold

`docs/milestones/M-23-quiet-on-dense-meshes.md:114,120,163`, and `README.md:168`. Rubric 7 / docs untrue.
- "export the cage, skinned: 2,038 to 1,039" compares different calls. At planning, export ran on the 590-face cage (listed before `subdivide`). `DenseRunProcessTests.Sequence` exports after two subdivides and a move (18,976 triangles, as the Retrospective itself says). The README table repeats the pairing.
- The Retrospective's "At `silent` every result is `{"ok":true}`" is untrue for `check` on a broken mesh, which answers `{"ok":false}`. Line 114, the README and `DenseRunProcessTests` (`Silent(tool, ok)`) all say so; T-206's own table shows 12 characters after the delete and the projection.
- Line 114 cites `artifacts/M-23/T-206/dense-run.tsv` for `full` and every call. No such file is in the worktree: `artifacts/M-23/` holds rescope, retro and review files. `branches/task/T-206/` has no `artifacts/` either. The test writes the TSV to its bin folder.

**Outcome:** —

## Summary

Three reviewers on Claude Opus 5.5, run in parallel on dd624e84 after the refactor pass, split by concern: the warning grouping and the smooth rule in Core and its callers (T-197, T-198); verbosity, help and unwrap (T-199 to T-202, T-206); and one pass over the whole diff for what falls between members, M-29's additions, the docs and the tracker.

- I reviewed `MeshWarningGroups`, `Measure`/`Limit`, `MeshReport.NonPlanarFaces`, the flat-only fold, and their callers (Report, check, export, subdivide, displace, select), along with their pins. The not-planar and non-manifold worst cases, the tie going to the lowest id, the edge order, where each grouped line stands, `select`'s use of `NonPlanarFaces`, and the samples' exemption in `MorphTargetFileTests` all hold. There are two Major findings. Export decides flat or smooth from the combined mesh, so a flat mesh beside a smooth one loses its not-planar warnings. The UV-overlap group counts only the lower face of each pair and decides whether to group by its warning count, which gives wrong counts and "1 faces", and a test pins the wrong number. There are also two Minor findings (the singular in the smooth note, and the skinned per-mesh grouped line no longer pinned on real output) and one Nit (the "; " separator and the line count in export). I ran no tests; each failing case comes from reading the code.

- I found no correctness bug in the code under review. All 35 tools that take `verbosity` (34 in the theory plus `unwrap`) add it to their known arguments and their schema (bake's and define_material's `Known` arrays included), read it before their `Failed` return, and answer `{"ok":true}` only after the edit or write succeeded. `Edit` returns `silent` after its refusals, and `Quiet` passes every `IsError` result through whole. check answers its own `ok` after its errors. Export threads the level through Static, Tree, Piece and Skinned, and wraps both of its success returns. save, bake and undo are covered, and undo's "nothing to undo" stays an error. help's `tool`/`topic` validation (type, unknown topic, both arguments) behaves as described. The topic's quoted wording matches MeshReport, MeshUvHealth and MeshHealth, and its grouping example matches `MeshWarningGroups`' format. unwrap's `largest` (faces descending, ties to the lowest face), `stretch.worst`/`lowest_face` (ties to the lowest), `single_face_islands`, `flipped` and `all` (order of lowest faces) are computed as ruled. The tools/list guard uses the server's own `AddTools`, so it misses no tool. The verbosity, help and unwrap test classes pass (64 tests, run here). Findings: 1 Medium (the `full` level is effectively untested on most tools, so a dropped level would pass) and 3 Low (the warnings topic's placement and duplicate of MaterialNoEffect, unwrap's untested `all` entries and vacuous `flipped` check, help's non-string topic error not matching the As built). No check was loosened beyond the moves the rulings call for (check's definition test now also reads the topic).

- Five findings. Two are correctness bugs that fall between members. First, T-198's smooth rule reads one shading mode, and static export reports on the combined mesh, which is smooth if any piece is, so a flat mesh's not-planar faces vanish from export in a mixed model. Second, T-197's grouped UV-overlap line counts the lower face of each pair, so one face overlapping four reads "1 faces". Three are docs that are untrue: check's description and help entry still refuse any argument but mesh and targets, now that verbosity exists; the README says `full` changes nothing on the twenty tools, though `displace` is one where it does; and the milestone's Measured export row compares different calls, a Retrospective sentence overstates `silent`, and the cited TSV does not exist. On the known item, `CheckTool.Reference` repeating the topic is not a finding in itself; its stale argument list and result shape are. The README's Dense meshes section otherwise reads as one account and matches the code. The M-29 tools (graph materials, bakes, get_mesh include/faces, displace) are all grouped or take verbosity as ruled; the tracker headers and tests.json (unchanged, samples/*.carve already inputs) are in order; and no Core engine dependency or stdout write was added. Tests were not run.

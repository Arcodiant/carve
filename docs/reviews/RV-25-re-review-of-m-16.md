---
description: >-
  Re-review of M-16
---
# RV-25 — Re-review of M-16

**Status:** Answered

**Reviews:** M-16

**Round:** 2

**Previous:** RV-24

**Diff:** e036717f737489666ddce5a960e224ef026f97e0 against 620d4a0

**Reviewer:** claude-opus-5-5 (one reviewer, checking RV-24's answers and the fix diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### R1. D1's version check refuses a render that gives `mesh` but no ids

**Severity:** Minor

**Where:** `src/Carve.Server/RenderTool.cs:367`, `src/Carve.Server/RenderTool.cs:379`, `src/Carve.Server/RenderTool.cs:424`

`shiftedFor` is set whenever `mesh` is given and the model has several meshes. That happens even when `wantsIds` is false, so no label or highlight is drawn. render's own `mesh` text allows this ("May be left out ... when neither labels nor highlight is asked for", so it may also be given). Then `ShiftedIdsRefusal` fires on any version change between the call's snapshot and the drawn frame.

Failing case: `crate` and `lid` are defined. `render {"mesh":"lid"}` (no labels, no highlight) is queued when `extrude {"mesh":"crate",...}` lands. The call fails with "the ids given for mesh 'lid' may name other elements", but no ids were given and the frame is correct. The same refusal also fires on a `set_colour`, `define_clip` or `set_weights` landing during the draw, and on an edit to a mesh after `lid`. None of those move lid's ids. The last cases are conservative and arguably fine, but the no-ids case is a plain false refusal. Set `shiftedFor` only when `wantsIds` is true.

**Outcome:** fixed

Answer: 2c2d157, RenderTool sets shiftedFor only when wantsIds, so a render naming a mesh with no labels or highlight is never refused by D1's check; new NamedMeshTests.A_render_with_the_lids_ids_drawn_on_a_later_model_is_refused_and_one_with_no_ids_is_not renders {"mesh":"lid"} 10 times while the crate is defined again and again; break shown: before the fix it failed with "The model changed while the frame was drawn, so the ids given for mesh 'lid' may name other elements; ..." on the no-ids render

### R2. render's description does not list D1's new refusal

**Severity:** Nit

**Where:** `src/Carve.Server/RenderTool.cs:228`

render's error list names the sheet race and quotes its text ("The mesh changed while the sheet was drawn, ..."). It does not name the new refusal, "The model changed while the frame was drawn, so the ids given for mesh '<name>' may name other elements; ...". An agent reading the description does not learn that a single-frame render with `mesh` can fail this way, or that the fix is to call again.

**Outcome:** fixed

Answer: 91a8550, render's error list names the refusal of labels or highlight given with mesh among several when an edit lands while the frame is drawn, quoting its text; no other text lists render's errors

### R3. export's description and SkinnedExport's doc still state the material rule without B4's fallback

**Severity:** Nit

**Where:** `src/Carve.Server/ExportTool.cs:31`, `src/Carve.Server/SkinnedExport.cs:24`

Both say a primitive's material is `'<mesh>:<group>' when a face group holds exactly that colour's faces, else '<mesh>:<rrggbb>'`. Since 0052527 that is no longer always so. A group whose name is another primitive's `<rrggbb>` gives way to the colour name. T-34's As built and the Retrospective were updated, but the agent-facing text and the class doc were not. Failing case (the new unit test's own): in `body`, green faces 0-1 are group `ff0000` and face 2 is red. The description promises `body:ff0000` for the green primitive, and the file has `body:00ff00`.

**Outcome:** fixed

Answer: a52faae, export's description and SkinnedExport's class doc say a primitive also takes '<mesh>:<rrggbb>' when its group's name is another colour's rrggbb in that mesh; T-34's As built and the Retrospective already said so; D-6's "named <mesh>:<group> or <mesh>:<rrggbb>" is still true and was left

### R4. Texts on mesh names still describe the rule from before the ':' refusal

**Severity:** Nit

**Where:** `src/Carve.Core/Model.cs:7`, `src/Carve.Core/Model.cs:104`, `src/Carve.Server/OpenTool.cs:23`

`Model`'s class doc says "Names are non-empty and unique". `Model.Create`'s doc lists the name problems as "a mesh name that is empty or given twice". Both now also refuse a ':' (`NameProblem`, used at `Model.cs:118`). open's description says a version 1 file opens "with mesh given, as that one mesh under the name given, whatever it is". `open {"path":"old.carve","mesh":"a:b"}` is now refused with "mesh: a mesh name must not contain ':'", and that refusal is not in open's error list. define_mesh's `name` text is the only one updated.

**Outcome:** fixed

Answer: 279284c, Model's class doc and Model.Create's doc name the ':' refusal; open's mesh text and version 1 sentence say the name given must be one define_mesh takes (not empty, no ':'), and open's error list adds it; T-29's two "non-empty and unique" lines note it since RV-24 B4; the ruling quoted in CarveFileVersion2Tests' comment was left as the ruling's words

### R5. The no-meshes refusal still says a Carve file holds at least one mesh

**Severity:** Nit

**Where:** `src/Carve.Core/CarveFile.cs:379`

After B2/D5 a clip library is a Carve file with `"meshes": []`. Reading `{"carve": 2, "meshes": []}` (no skeleton) still refuses with "the file has no meshes; a Carve file holds at least one". The text is now untrue, and it does not tell the reader that a skeleton would make the file readable. The text is pinned by `CarveFileV2Tests.cs:433` and `ClipLibraryFileTests.cs:38`, so a rewording changes both.

**Outcome:** fixed

Answer: ac764c5, the refusal now reads "the file has no meshes and no skeleton; a Carve file holds at least one mesh, or a skeleton (a clip library)"; pinned tests updated, expected text only: CarveFileV2Tests (the {"carve": 2, "meshes": []} case, :433) and ClipLibraryFileTests.No_meshes_with_a_null_skeleton_is_still_refused; open's error list already said "a file with no meshes and no skeleton"

### R6. open with `mesh` on a clip-library file lists no meshes with a dangling "its meshes are"

**Severity:** Nit

**Where:** `src/Carve.Core/Model.cs:160`

B2/D5 makes a file with no meshes openable. `open {"path":"anim.carve","mesh":"Body"}` on such a file reaches `WithMeshFrom`, which says "mesh: the file has no mesh named 'Body'; its meshes are " and then nothing, since `NameList.Quoted` of an empty list is "". `Model.Target` (`Model.cs:219`) already has a separate "has no meshes yet" wording for this case, and `WithMeshFrom` needs the same.

**Outcome:** fixed

Answer: fab163d, WithMeshFrom says "mesh: the file has no mesh named '<name>'; it has no meshes (a clip library)" when the file has none; open's error list says so; new ClipLibraryFileTests.Taking_a_mesh_from_a_clip_library_says_it_has_no_meshes; break shown: with Model.cs before the fix it failed, actual "...its meshes are "

## Summary

All 30 RV-24 answers hold: A1-A7, B1-B5, C1-C8 and D1-D10. In each, the commit does what its Answer says and the defect is gone. Where an Answer claims a break, the break is credible, and B1's race test passes locally in 233 ms. Two hold with a caveat their Answers state themselves. D1's only test is on `SheetVersions.ShiftedIdsRefusal`, so dropping the call at `RenderTool.cs:424`, or passing it `current.Version` twice, fails no test. C4's held-later-key side was not shown failing on its own. The fixes add six new findings. One is Minor: R1, D1's version check refuses a render that gives `mesh` but asks for no ids. Five are Nits: R2, render's description omits the new refusal; R3, export's description and SkinnedExport's doc omit B4's material fallback; R4, Model's docs and open's version 1 sentence predate the ':' refusal; R5, the no-meshes refusal still says a file holds at least one; R6, open with `mesh` on a clip library ends "its meshes are " with nothing after it. Not covered: no suite was run and no frame looked at (verify had passed, as the brief says). Only `ToolPathsRaceTests` and `SheetOnOneMeshTests` were run. C1, C5, B3 and A3's breaks were not reproduced. C4's drum points were not projected by hand. The tests of D4's every-tool sweep and the clips-export key checks were read only in the Answers and the diff headers, not line by line.

**Answers, 2026-11-09.** All six fixed by one claude-opus-5-5 fixer (2c2d157..fab163d). D1 gap: fixed (2c2d157, the same NamedMeshTests test renders {"mesh":"lid","labels":"faces"} while the crate is redefined and requires the refusal within 10 tries; breaks shown: the RenderTool call removed and the call passed current.Version twice each failed it with "no labelled render of the lid was refused in 10 drawn during edits to the crate"; with the code as committed it passed 3 runs of 3 locally) C4: skipped: showing the held-later-key break needs a scenario-character run with Clip.cs changed, a queue slot of its own; not cheap, not done; the check was not changed After the fixes, `cairn milestone M-16 verify` passed every suite (5 ran, 4 carried) and all 105 frames and exported files are byte-identical, fetched by job id.

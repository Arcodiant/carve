---
description: >-
  Review of M-16
---
# RV-24 — Review of M-16

**Status:** Answered

**Reviews:** M-16

**Round:** 1

**Previous:** —

**Diff:** 143706d7d39414c600569920b726237efb279974 against 620d4a0

**Reviewer:** four claude-opus-5-5 reviewers, in parallel

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the Core model, weights, skinning and the sampler (T-29 to T-33) (claude-opus-5-5)

### A1. undo's description predates M-16: wrong list of steps, and wrong about the first mesh

**Severity:** Minor

**Where:** `src/Carve.Server/UndoTool.cs:10`, `src/Carve.Server/UndoTool.cs:16`

The text says undo takes back "the last define_mesh, open, move or extrude", and "the mesh before it is current again". M-16 added six more tools that each push one undo step: define_group, set_colour, define_skeleton, set_weights, define_clip and delete_clip (`ModelStore.Restyle`, `DefineSkeleton`, `SetWeights`, `DefineClip`, `DeleteClip` all go through `PushLocked`). An agent reading the description does not know that undo after `set_colour` takes back the colour, not its last extrude.

Line 16 also says that undo straight after the process's first mesh is a tool error. That is no longer true once `define_skeleton` can run on an empty model. Failing case: `define_skeleton` (example), then `define_mesh` (cube), then `undo`. `_history` holds two entries, so undo succeeds and returns `{meshes: [], undone: "define_mesh"}`, the model with a skeleton and no mesh. The description promises a tool error, or define_mesh's report.

**Outcome:** fixed

Answer: 413f644, undo's description names all ten step tools and says the first change of any tool has nothing before it; undo of a define_mesh/open made after define_skeleton gives {meshes:[], undone}; description only, no check added

### A2. define_skeleton's description leaves out the clips half of the skeleton-change rule

**Severity:** Minor

**Where:** `src/Carve.Server/SkeletonTools.cs:50`

The description says "Once any mesh has weights (set_weights), a skeleton with other bone names or another order is a tool error". T-33 added the same refusal for clips (`ModelStore.cs:166-171`), and `define_clip`'s own text states it (`ClipTools.cs:37`), but `define_skeleton`'s does not. Its error list on line 49 does not mention it either.

Failing case: `define_skeleton` with bones a, b; `define_clip` "wave" on b; no weights; `define_skeleton` with bones a, c. The call is refused ("delete_clip the clips first"), and nothing in the description of the tool that refused it says so. The doc comment on `ModelStore.DefineSkeleton` (`ModelStore.cs:151`) still reads "(Clips, in T-33, join the rule here.)", which is stale since T-33 landed them.

**Outcome:** fixed

Answer: e20aa4f, define_skeleton's description gives the clips half of the rule, adding/dropping a bone, and how to drop weights/clips; ModelStore.DefineSkeleton's doc no longer says clips are to come; text only

### A3. No test composes two non-commuting rotations, so reversing the quaternion product passes every unit test

**Severity:** Minor

**Where:** `src/Carve.Core/Skinning.cs:69`, `src/Carve.Core/Skeleton.cs:43`, `tests/Carve.Core.Tests/SkinningTests.cs:31-90`, `tests/Carve.Core.Tests/SkeletonTests.cs:38`

I checked `Rigid.Then`'s Hamilton product, the rotate formula, `Inverse` and posed x inverse-rest against hand-worked values, and they are right. But the tests could not catch a reversed product:

- Every hand-worked skinning test (`A_quarter_turn...`, `Two_equal_influences...`, `A_parents_turn...`) uses a chain whose rest rotations are identity, and turns only one bone.
- The only test with two non-identity rotations, `The_rest_pose_returns_the_rest_positions_exactly`, builds rest and posed with the same `Then`, so skin = identity whatever the order.
- `Rest_world_position_adds_the_parents_chain...` has one rotated bone under an identity parent, so `worldRotations[parent] * local` and `local * worldRotations[parent]` give the same result.

Failing case: change `Then` to compute child x this, or `Skeleton` to `local * worldRotations[parent]`. All of `SkinningTests` and `SkeletonTests` still pass. Yet a rig like base turned 90 deg about Z with joint turned 90 deg about X poses a joint vertex in the wrong place, and `get_skeleton`'s `restWorldPosition` is wrong for a grandchild under two rotated bones.

T-32's break table lists "composition order" as caught, but that is the posed-vs-inverse-rest order, not the quaternion product. The gardener scenario's independent matrix sampler (`scenarios/Carve.Scenario.Character/GardenerSpec.cs:210-225`) may catch the skinning half at its checked pixels. I did not verify that, and nothing checks `RestWorldPosition` with nested rotations.

**Outcome:** fixed

Answer: 42d9ac1, SkinningTests.A_turned_joint_under_a_turned_parent_composes_the_parent_after_the_child (base rests a quarter turn about Z, joint posed a quarter turn about X, vertex expected at (-1, 0, 1)) and SkeletonTests.Rest_world_position_under_two_turned_bones_applies_the_parents_turn_after_the_childs (grandchild under Z then X turns at (-1, 0, 1)); break shown: Rigid.Then with its operands swapped (child x this) and Skeleton with local * worldRotations[parent] fail both new tests, 2 of 515 Core tests

### A4. define_skeleton reports false problems after a malformed bone

**Severity:** Nit

**Where:** `src/Carve.Server/SkeletonTools.cs:131`

A bone that fails shape reading is replaced by a placeholder `BoneSpec("x", null, ...)` before `Skeleton.Create`. Only errors whose element is that bone are filtered (`shapeBad`), so the placeholder's name and the loss of the real name leak into other bones' errors.

- Failing case 1: `[{"name":"hips","position":[0,0,0]}, {"name":"x","position":[0,0,0],"rotation":[0,0,0,1]}]`. bones[0] lacks rotation. The error list also has "bones[1]: the name 'x' is already bones[0]'s; names must be unique", which is false.
- Failing case 2: a child whose parent is the malformed bone gets "unknown parent 'hips'".

The description promises a list of every problem by bone, and these two are not problems.

**Outcome:** fixed

Answer: 35f4750, ReadBone keeps a malformed bone's read name, parent, position and rotation, with an unreadable name replaced by a NUL-prefixed placeholder, and the shapeBad filter is gone, so only true problems are listed, incl. a malformed bone's own duplicate name and unknown parent; new SkeletonProblemListTests (regression, 3); break: the previous SkeletonTools.cs fails all 3

### A5. render's pose argument says it needs weights; it does not

**Severity:** Nit

**Where:** `src/Carve.Server/RenderTool.cs:50` (`PoseSchema` description)

The argument text says "Needs a skeleton and weights (set_weights)". The tool refuses only a missing skeleton (and unknown bones or non-unit rotations), as its main description says. With a skeleton and no weights, `render` with a pose succeeds and draws the rest pose unchanged (`Skinning.Posed` leaves unweighted meshes as they are). An agent that forgot `set_weights` gets an unposed frame and no error, while the schema text says it would be refused.

**Outcome:** fixed

Answer: a31a4e9, render's pose argument says it needs a skeleton only and an unweighted mesh is drawn at rest with no error; text only

### A6. ModelStore.DefineTarget is dead in the product, and its tests describe open's old behaviour

**Severity:** Nit

**Where:** `src/Carve.Server/ModelStore.cs:60`, `tests/Carve.Server.Tests/ModelStoreTests.cs:110-124`

No tool calls `DefineTarget` since T-35 moved `open` to `OpenModel`/`OpenMesh` (`OpenTool.cs:71-72`). `Define_target_with_no_name_replaces_the_only_mesh...` still tests it with tool name "open", so a reader takes it for a check on open's behaviour that open no longer has.

**Outcome:** fixed

Answer: e20aa4f, ModelStore.DefineTarget removed with its test and the open-labelled assertion in ModelStoreTests; nothing called it

### A7. As built notes of T-30 to T-33 say the file keeps no colours, skeleton, weights or clips

**Severity:** Nit

**Where:** `docs/tasks/T-30-face-colours-and-groups.md` (Readings: "the Carve file does not keep colours or groups"), `docs/tasks/T-31-skeleton.md` ("A skeleton is not saved by save/open"), `docs/tasks/T-32-explicit-weights-and-posing.md` ("weights are not saved by save"; "a new define_mesh or open of a name starts without weights"), T-33's As built ("clips are not saved by save")

T-35 superseded each of these: `CarveFile` now reads and writes styles, skeleton, weights and clips, and `open` with a name carries the mesh's weights across by bone name (`Model.WithMeshFrom`). Each note was true when its task landed. Read now, as the milestone's record of what was built, they contradict the merged code, and none points to T-35.

**Outcome:** fixed

Answer: a9233cd, As built of T-29 (export refuses several meshes; save/open pick by mesh), T-30 (file keeps no colours/groups; export of several refused), T-31 (skeleton not saved), T-32 (weights not saved; open of a name starts without weights), T-33 (clips not saved) each marked superseded by T-34 or T-35; docs only

## Findings: the file formats: Carve file v2 and the skinned and clips .glb (T-34, T-35, T-46, T-52) (claude-opus-5-5)

### B1. `overwrite: false` is a check, then a move that always overwrites

**Severity:** Minor

**Where:** `src/Carve.Server/ToolPaths.cs:101`, `src/Carve.Server/ToolPaths.cs:116`

Rubric: correctness, concurrency. `Write` checks `File.Exists` and refuses when `overwrite` is false. The bytes then go to a temp file, and the temp file is moved with `File.Move(temp, path.FullPath, overwrite: true)` whatever `overwrite` was. Nothing holds the refusal true between the check and the move. Two `export` calls to the same new path (no `overwrite`) can run at once, and the store's locks show calls do overlap. Both pass the `Exists` check. Both use the same temp name, `.{file}.{pid}.tmp`. The later one silently replaces the earlier file, and its result has no "(replaced)". That breaks T-52's rule: "an existing file without it is a tool error ... with the file unchanged". Another process writing the file during the window does the same. Fix: pass the flag through, `File.Move(temp, path.FullPath, overwrite)`. A file that appears in the window then raises an `IOException`, and that should be reported as the "already exists" refusal. The unit test (`ToolPathsTests.cs:57`) is sequential only, so it cannot see this.

**Outcome:** fixed

Answer: 93cad95, ToolPaths.Write moves with File.Move(temp, path, overwrite), an IOException on an existing file without overwrite is the "already exists" refusal with the temp deleted, and each write has its own temp name (counter); new ToolPathsRaceTests (unit, 40 rounds x 8 racing writers); break: File.Move(..., overwrite: true) gives "round 0: 4 writers reported success without overwrite"

### B2. Clips mode refuses a rig with no mesh as "no mesh yet", which its description does not list

**Severity:** Minor

**Where:** `src/Carve.Server/ExportTool.cs:89`

Rubric: behaviour against the description and D-6. `define_skeleton` and `define_clip` work on an empty model: `SkeletonTools.Run` and `ModelStore.DefineSkeleton` never check for a mesh. A pure clip library (a skeleton and clips, no mesh) is therefore something the tools build. `export` with `mode: "clips"` is the file for exactly that. It writes no mesh, and `ClipsExport.Write` needs none. But `Run` returns `export: <NoMesh>` at line 89, before the mode is looked at on line 94. The description says "Clips mode is refused, writing nothing, for a model with no skeleton or no clips". It does not say clips mode needs a mesh.

Failing case: `define_skeleton {bones:[{name:"Hips",parent:null,position:[0,1,0],rotation:[0,0,0,1]}]}`, then `define_clip {name:"idle",duration:1,channels:{}}`, then `export {path:"anim.clips.glb",mode:"clips"}` gives "no mesh yet". `save` refuses too, and the reader refuses a file with no meshes (as ruled), so that work cannot be kept or exported. Either move the mesh check after `if (clips)`, or rule that clips mode needs a mesh and say so in the description.

**Outcome:** fixed

Answer: 42f422a, the person's ruling of 2026-11-09: export clips mode skips the mesh check, save writes "meshes": [] when there is a skeleton, CarveFile.Read takes "meshes": [] beside a non-null skeleton, open reports meshes:[]; no meshes and no skeleton still refused with the old text; model mode still needs a mesh; export/save/open descriptions, CarveFile doc, D-6, T-35 and T-46 As built and the retrospective's rulings list updated; new ClipLibraryFileTests (unit, 2) and ClipLibraryToolTests (regression, 2); break: the pre-ruling ExportTool/SaveTool/OpenTool/CarveFile fail 1 of 2 unit and 2 of 2 regression

### B3. "One writes them twice" compares file lengths only

**Severity:** Nit

**Where:** `tests/Carve.Server.Tests/SkinnedExportToolTests.cs:164`

Rubric: a check that cannot fail on what it names. `Two_carve_processes_write_the_same_bytes_and_one_writes_them_twice` exports `chain.glb` and `again.glb` in one process. It checks only that the two files have the same length. The stems "chain" and "again" are both 5 characters, so any in-process difference that keeps the length is not caught: a reordered dictionary, or changed float bits in an accessor. The in-process byte identity T-34 asks for is covered only by the unit test `SkinnedExportTests.The_same_model_writes_the_same_bytes_twice_with_several_clips`, which never goes through the real `carve`. Exporting the second file to the same stem in another folder (`again/chain.glb`, as the clips-mode test does) makes this a byte comparison.

**Outcome:** fixed

Answer: 3d117b4, the in-process second export goes to again/chain.glb and is compared byte for byte; break shown: a static counter in SkinnedExport tilting every normal by 1e-4 on the second Write in a process keeps the file length; the old length check passed, the new byte check failed. A position nudge was not usable as a break: it changes the accessor min/max text and so the length

### B4. Skinned material names can collide within one mesh

**Severity:** Nit

**Where:** `src/Carve.Server/SkinnedExport.cs:149`

Rubric: a public-format detail a game may key on. A primitive's material is `<mesh>:<group>` when a group holds exactly its faces, else `<mesh>:<rrggbb>`. The two namespaces overlap, so two primitives of one piece can share a name. Example: in mesh `body`, colour `#00FF00` on faces [0, 1] with group `ff0000` = [0, 1], and colour `#FF0000` on face 2. Both materials are then named `body:ff0000`. A colon in a mesh or group name collides the same way: mesh `a` with group `b:c` against mesh `a:b` with group `c`. D-6 gives the names so "a piece's name survives in the file". A game that looks materials up by name gets the wrong one. The names are as ruled; the collision is not covered by the ruling or by a test.

**Outcome:** fixed

Answer: 0052527, by the person's ruling of 2026-11-09: SkinnedExport.Materials gives a primitive whose group name is another primitive's <rrggbb> in the piece its own <mesh>:<rrggbb> (a group named its own colour, or with colons, is kept), and Model.NameProblem refuses ':' in a mesh name, so define_mesh (name:), every tool's mesh argument (mesh:) and CarveFile.Read (meshes[i].name) refuse it with "a mesh name must not contain ':' (a skinned export names materials <mesh>:<group>)"; define_mesh's name description, its pinned schema test, T-34's As built and the retrospective's rulings list updated; ModelTests' old assertion that "a:b" is a good name replaced; new tests ModelTests.A_name_with_a_colon_is_refused and a CarveFileV2Tests row (unit), SkinnedExportTests.A_group_named_like_another_primitives_colour_falls_back_to_its_own_colour_name (unit) and NamedMeshTests.A_mesh_name_with_a_colon_is_refused_by_define_mesh_and_the_tools_that_name_a_mesh (regression); break: the previous Model.cs and SkinnedExport.cs fail all 4

### B5. Weight and clip refusal messages print numbers in the current culture

**Severity:** Nit

**Where:** `src/Carve.Core/VertexWeights.cs:70`, `src/Carve.Core/Clip.cs:124`, `src/Carve.Core/Clip.cs:182`

Rubric: refusal text. The reader and the tools share these refusals. They format the offending number with plain interpolation, `$"{weight} is not a weight"`, `$"{duration} seconds"` and `$"time {time} is outside the clip, which runs from 0 to {d}"`, with no culture given. Everywhere else in the code numbers are written with `CultureInfo.InvariantCulture`. On a de-DE machine, `open` of a file holding `"base": -0.5` reports `-0,5 is not a weight`, and the culture-sensitive expected text in `CarveFileV2Tests.BadClipsAreRefused` and `BadWeightsAreRefused` no longer matches. The `0.5` case in `BadClipsAreRefused` would fail there.

**Outcome:** fixed

Answer: 65a64a4, FormattableString.Invariant on the number refusals in VertexWeights.Raw, Clip.Create (duration, rotation length, key times x3), Skeleton.Create (rotation length) and RenderTool (pose rotation length, clip time); new RefusalCultureTests (unit, 3, under de-DE); break: the previous Core files fail all 3. RenderTool's two texts have no culture test: the carve process's culture cannot be set from a test

Checked with no finding:
- `CarveFile.Write` and `Read`: float and double round trip (`"R"` and parse of the raw text, including `-0`, subnormals and exponents); fixed key order; version 1 dispatch.
- Shared refusals: `Raw`, `Kept`, `Skeleton.Create`, `Clip.Create`, `FaceStyle`, `Model.Create`. Duplicate handling matches `set_weights` and `define_clip`.
- `Piece` and `WithMeshFrom`, and the open-with-name rulings.
- Inverse binds (composed in double, rigid inverse, row-vector layout).
- Node, skin and scene order.
- Weight narrowing.
- `Keyed`: keys at 0 and at the duration. It matches the Core sampler's hold before the first key and after the last.
- The empty-clip hold; limit constants at and past each limit.
- Overwrite ordering after the unweighted, clips and path refusals in every mode.

## Findings: the scenarios and checks: can each fail? (T-30, T-32, T-33, T-36 frames and the load check) (claude-opus-5-5)

### C1. No check compares the exported clip key values or joint indices with the model

**Severity:** Major

**Where:** `tests/Carve.Server.Tests/ClipsExportTests.cs:118`

Nothing in the diff reads a keyframe value or a non-root `JOINTS_0` index out of an exported `.glb` and compares it with the Carve clip or weights it came from:
- `ClipsExportTests.cs:104-116` compares the clips file with the model file. Both come from the same `GlbRig` writer, so a bug in that writer is in both. The block at 118-123 is commented "Spot checks that the channels are the clip's, not merely equal to each other", but it checks only `Times` and `Interpolation`, not `Values`.
- `SkinnedExportTests.cs:108-124` checks only that rotation keys are unit length. `:126-168` checks key counts and the min and max times. `:216-227` checks interpolation modes.
- `SkinnedExportTests.cs:196` asserts only that *some* coat vertex is `(1,0,0,0)` on joint 0. The coat vertices weighted to bone 1 are never checked for joint 1. `Four_influences_narrowed_to_float_still_sum_to_1` (`:205-209`) checks the weights of `Joints(...)` and never its `.Joints`.
- `tests/Carve.CharacterLoad.Tests/CharacterLoadTests.cs:50-92` checks bone names, clip names and durations only. The T-36 break table shows that a sampler holding the earlier key passed every load check.
- Every gardener and chain frame is drawn by Carve's own `Skinning.Posed`, never from the exported file. The retrospective confirms that no viewer draws the exported character.

Failing case: make `GlbRig` write each rotation key conjugated (`Quaternion.Conjugate(k.Value)`), write the identity for every key, or swap the order of translation keys. Or make `SkinnedExport.Joints` write the joint as `0` for every influence. Every test in `Carve.Server.Tests`, `Carve.CharacterLoad.Tests` and `scenario-character` still passes, and a game gets a walk that bends backwards, a frozen rig, or a mesh bound wholly to the root. Asserting the sampled values at known times (for example the gardener's `Walk` `UpperLeg_L` at 0 and 0.5 s) and the joint index of one vertex per bone would close this.

**Outcome:** fixed

Answer: 19df4ae, ClipsExportTests checks the clips file's key values: walk UpperLeg_L at 0, 0.2, 0.8 and 1.2 s (first key held from 0, last held to the end), UpperLeg_R's three keys, the Hips translation's four values in order, wave Shoulder_R at 1 s; rotations compared up to sign, |dot| > 1 - 1e-6, translations to 1e-6. The model file's channels already equal the clips file's, so both are covered. New SkinnedExportTests.Every_vertex_carries_its_own_bones_with_their_weights finds each written vertex in the model by position and compares its JOINTS_0/WEIGHTS_0 with the model's influences, every bone 0 to 4 included; Four_influences_narrowed_to_float_still_sum_to_1 now checks .Joints too; breaks shown, each failing: rotation keys conjugated, every rotation key the identity, translation values in reverse order (all fail the clips test), SkinnedExport.Joints writing joint 0 for every influence (fails both joint tests)

### C2. The refused define_skeleton "leaves the skeleton unchanged" check counts bones, and the refused set has the same count

**Severity:** Minor

**Where:** `tests/Carve.Server.Tests/SkeletonToolTests.cs:65`

`Refusals_list_every_problem_by_bone_and_leave_the_skeleton_unchanged` defines `Three` (hips, spine, head) and sends a refused set of three bones (`""`, `a`, `b`). It then asserts only `read.GetArrayLength() == 3`. If the refused bones were stored anyway, `get_skeleton` would still return 3 bones and the test would pass. So the "unchanged" half cannot fail. The check should compare names (or the whole `get_skeleton` text) with the text taken before the call.

**Outcome:** fixed

Answer: 0383bc9, the get_skeleton text after the refused call must equal the text before it; break shown: define_skeleton storing a three-bone x0, x1, x2 skeleton when it refuses: the old count check passed, the new check failed

### C3. Every can check samples a face the camera cannot see

**Severity:** Minor

**Where:** `scenarios/Carve.Scenario.Character/CharacterScenario.cs:256` (also `:246`, `:273`, `:301`)

`body.NearestFace("can", (-0.48, 0.39, 0.2))` returns the spout box's -X face, centroid (-0.455, 0.39, 0.20), at distance 0.025. The spout's +Z face is at 0.11 and the nearest drum side at about 0.15. `CameraPixels.Perspective` puts the +Z eye at the rest-bounds centre, x = (-0.52 + 0.415)/2 = -0.05. That is on the inner side of the plane x = -0.455, so the face points away from the camera. The same holds in the pose, where the face normal turns to about (-0.82, 0.57, 0) and the vector to the eye is about (0.70, 0.09, 2.3), giving a negative dot product.

So the "can face" checks at rest, in the pose and in Idle are each read through the spout's +Z face or the drum in front of the named point. The As built says "the centre of a front face of each colour", and these checks do not match that. They still fail when the can is ignored or mirrored, but they do not check the face or point the messages name. Choose the spout's +Z face (near (-0.435, 0.39, 0.30)) instead.

**Outcome:** fixed

Answer: cd3e50b, the four can points are now (-0.435, 0.39, 0.30), the centre of the spout's +Z face, normal +Z toward the +Z eye (the pose and Idle turn the can about Z, which leaves that normal as it is); the local run passed with it; no break: it aims the same checks at the face the messages name and adds no new way to fail

### C4. Idle at 1.5 s cannot tell interpolation from holding a key, and the As built says it was a key time

**Severity:** Minor

**Where:** `scenarios/Carve.Scenario.Character/CharacterScenario.cs:67`

Idle's `Shoulder_R` keys are at 0, 1 and 2 s (`GardenerSpec.cs:170`), so at 1.5 s it lies between keys at -6°. If the product holds the earlier key (-12°), the drum's centre moves to about (-0.55, 0.44) with radius 0.09. The expected can point at -6°, about (-0.52, 0.42, 0.2), then lies inside the -12° drum, so `CheckCan` reads "can" either way. The T-36 break table confirms this: "sampler holds the earlier key" passed until `gardener-walk-0.125-side` was added.

T-36's As built says "every other time was a key time", which is wrong for Idle at 1.5 s. It also says "Walk at 0.125 s (between keys) has the hand half way and not at either key's place". The code (`:89-91`) asserts only that the slerped hand point reads as skin. The "not at either key's place" check (`ExpectNot`) runs only at 0 and 0.5 s (`:92-94`). Either the text or the checks should change. The Idle 1.5 s frame adds no interpolation coverage.

**Outcome:** fixed

Answer: cd3e50b + 0f43318; I chose the check that can fail: at Idle 1.5 s the scenario now asks that the drum's outer side as posed by the 1 s key and its inner side as posed by the 2 s key are not "can"; each is about 0.14 m from the drum's centre at -6 degrees, radius 0.09. T-36's As built now says Walk 0.125 s checks the hand at its slerped place only (no "not at either key's place" check was added there: the break table already shows that frame failing a held key) and that Idle 1.5 s was between keys; break shown: the sampler holding the earlier key (Clip.cs interpolation returning the earlier value) fails at gardener-idle-1.5-front, "where the can's side is when the key at 1 s is held at (108, 331) reads as can". The held-later-key side was not shown failing on its own

### C5. The two-mesh frame test projects with the product's own ViewFraming, so its mirror claim cannot fail on a framing mirror

**Severity:** Minor

**Where:** `tests/Carve.Server.Tests/NamedMeshTests.cs:198`

`PixelOf` works out the expected pixel with `DrawnSpace.Of` and `ViewFraming.Frame(..., RenderView.Front)`, the same code `render` uses. The comment at `:233-234` says "a mirrored camera puts the tall box over there, and a flipped one puts the cube there". A mirror or flip introduced in `ViewFraming` (its look-at, `FrontDirection`'s X, or its projection) moves `PixelOf`'s points with the image, so `:235-238` still pass. Only a mirror after the framing (in readback or the PNG) would fail them. The T-29 break table has no camera row. The scenarios' `CameraPixels` is independent of the product; this test is not.

**Outcome:** fixed

Answer: d1ce8af, PixelOf now projects with the scenarios' CameraPixels (linked into Carve.Server.Tests), with the front direction (1.6, 1.2, 2.2) written out, not ViewFraming; break shown: the projection's M11 negated in ViewFraming (mirror) fails "something is drawn above the cube" with the new PixelOf and passes with the old; M22 negated (flip) fails "the cube's front face is missing"

### C6. The allow_unweighted scenario check for "bound to Hips" cannot fail

**Severity:** Nit

**Where:** `scenarios/Carve.Scenario.Character/CharacterScenario.cs:415`

`allowed.Contains("Hips")` is always true, because every skinned export result lists every bone by name (`ExportTool.cs:142`, `... bone(s) 'Hips', 'Spine_01', ...`). The failure message promises "the vertices bound to Hips". The check should look for `bound to the first root bone 'Hips'`. `SkinnedExportToolTests.cs:65` covers this on the chain, so this is a Nit.

**Outcome:** fixed

Answer: cd3e50b, the scenario looks for "bound to the first root bone 'Hips'"; break shown: ExportTool leaving out the allow_unweighted sentence fails the scenario at "export allow_unweighted: expected the vertices bound to Hips"; the old Contains("Hips") passed on it, since the result lists every bone

### C7. The reopen frame says it checks weights, but a rest frame cannot show weights

**Severity:** Nit

**Where:** `scenarios/Carve.Scenario.Character/CharacterScenario.cs:354`

"rest from the front, after save and open (weights and colours came back)": with every bone at rest, any weights give the rest positions. Only `walk2` (Walk at 0.25 s: hips and lower legs only) and the byte comparison of the re-saved file (`:364`) can catch lost weights. The arms' weights, for example, reach no frame. The message overstates what the frame checks.

**Outcome:** fixed

Answer: cd3e50b, the message now says colours and shape came back and that a rest frame cannot show weights, which walk2 and the re-saved bytes check; the message only, no check changed

### C8. Stale claims in GardenerSpec's header

**Severity:** Nit

**Where:** `scenarios/Carve.Scenario.Character/GardenerSpec.cs:19`

- "This file is linked into the scenario and into the checks beside the suite, so both read one source." `Carve.CharacterLoad.Tests.csproj` does not link it, and nothing outside the scenario uses `GardenerSpec`.
- Line 15 and the As built say "no trigonometry is evaluated here and the numbers are the same on every machine". `Rot` (`:78-83`) calls `MathF.Sin` and `MathF.Cos`, and only the rounding to 5 dp stands between that and the bit-exact sample comparison (`CharacterLoadTests.cs:95-101`).

**Outcome:** fixed

Answer: 0f43318, GardenerSpec's header no longer says the file is linked into the checks (the load checks compare the sample by content) and says Rot calls MathF.Sin and Cos with each component rounded to 5 dp; T-36's As built says the same; comments only

## Findings: the whole diff (claude-opus-5-5)

### D1. Multi-mesh highlight and label offsets are taken from the call's snapshot but drawn on a later one

**Severity:** Minor

**Where:** `src/Carve.Server/RenderTool.cs:370`

T-29's ruling has `render` aim `labels` and `highlight` with `mesh`. The ids are shifted by `OffsetOf(named)` and the `ElementWindow` is taken from `current`, the snapshot the call read (RenderTool.cs:336, 370-377). The frame thread draws whatever `store.Current` holds when it gets to the spec. `SheetVersions.OnOneMesh` only checks that the tiles agree with one another, not with the snapshot the call validated. The pump's guard (`RenderPump.cs:80`) only checks that the shifted ids exist in the combined mesh, and a shifted id usually does, in another mesh.

Failing case: `crate` and `lid` are defined. `render {"mesh":"lid","highlight":{"faces":[0]},"labels":"faces"}` is in the queue when `extrude {"mesh":"crate","faces":[1],"distance":0.2}` lands. Combined face 6 is now one of the crate's new walls. The image highlights a crate face and labels crate faces 6..11 as lid faces 0..5, and the call does not fail. Before M-16, edits only added ids, so a validated id still named the same element. Offsets into a combined mesh break that. Compare the frame's `Version` with `current.Version`, or carry the snapshot in the spec.

**Outcome:** fixed

Answer: 4aa6282, render refuses with "The model changed while the frame was drawn, so the ids given for mesh '<name>' may name other elements; call render again once the edits have settled." when it shifted ids for one mesh of several and the frames' version is not the snapshot it checked (SheetVersions.ShiftedIdsRefusal, called after OnOneMesh); new SheetOnOneMeshTests.Ids_shifted_for_one_mesh_of_several_are_refused_on_a_frame_of_another_version (unit); break: the helper made never to refuse fails it. No race test drives the tool itself

### D2. `undo`'s description still names only four step tools, and its first-mesh claim is no longer true

**Severity:** Minor

**Where:** `src/Carve.Server/UndoTool.cs:10`

The description says "the last define_mesh, open, move or extrude is taken back and the mesh before it is current again". But `set_colour`, `define_group`, `define_skeleton`, `set_weights`, `define_clip` and `delete_clip` all push a step through `ModelStore.PushLocked`, and undo takes them back. An agent reading this cannot tell that `undo` after `set_weights` removes the weights rather than a mesh edit.

Line 16 says "The first mesh a process has ... has nothing before it, so undo straight after an open that gave the process its first mesh is a tool error". That is false when the process ran `define_skeleton` (or anything else) first. That change is history[0], so the undo succeeds and returns a model with no meshes. The result is then `{"meshes":[],"undone":"open"}`, a shape the description does not give, because `Meshes.Count == 1` is false.

**Outcome:** fixed

Answer: 413f644, same as A1

### D3. `define_skeleton`'s description omits the clips refusal T-33 added, and the store's comment still says it is to come

**Severity:** Minor

**Where:** `src/Carve.Server/SkeletonTools.cs:50`

`ModelStore.DefineSkeleton` (ModelStore.cs:166) refuses a skeleton with other names or another order once the model has clips. The tool description gives only the weights rule ("Once any mesh has weights (set_weights), ... is a tool error"), and its error list does not name clips either. The XML doc at ModelStore.cs:151 still reads "(Clips, in T-33, join the rule here.)", left stale after T-33 landed.

Failing case: `define_skeleton`, `define_clip`, then `define_skeleton` with one bone renamed. The tool refuses with "bones: the model has clips (...)", which the description never mentions.

**Outcome:** fixed

Answer: e20aa4f, same as A2

### D4. The cross-tool argument rules have no tests for the seven new tools

**Severity:** Minor

**Where:** `tests/Carve.Server.Tests/NamedMeshTests.cs:71`

The test `Every_tool_aimed_at_one_mesh_refuses_to_guess_among_several_and_names_them` lists move, extrude, get_mesh, check and select. It does not list `set_colour`, `define_group` or `set_weights`, which T-30 and T-32 added later, and no other test checks that those three refuse an unnamed target among several meshes.

No test sends an unknown argument to any of `define_group`, `set_colour`, `define_skeleton`, `get_skeleton`, `set_weights`, `define_clip` or `delete_clip`. A grep for "not an argument of" finds only check, edit, export, save/open and select tests. Each tool keeps its own hand-written `known` list (e.g. SetWeightsTool.cs:54), so a name added to the schema but not to the list, or the reverse, would pass every test.

Separately, `EditToolTests.cs:168-169` still expects "its arguments are faces, distance, inset". That passes only because it is a prefix of the new "..., inset, mesh". The test no longer pins the message.

**Outcome:** fixed

Answer: d803765, the refuse-to-guess theory adds set_colour, define_group and set_weights (with a one-bone skeleton for set_weights); new NamedMeshTests.Every_tool_refuses_an_unknown_argument_and_lists_exactly_the_arguments_its_schema_has sends zz_unknown to every tool in tools/list and compares the listed arguments with the schema's properties as sets; EditToolTests now expects the full lists "faces, distance, inset, mesh" and "vertices, edges, faces, by, mesh"; breaks shown: set_weights' list without mesh (fails both tests), define_clip's list with an extra "speed" (fails the schema test), define_group guessing the first mesh (fails the theory row)

### D5. A skeleton and clips with no mesh can be built, but not saved or exported as clips

**Severity:** Minor

**Where:** `src/Carve.Server/ExportTool.cs:89`

`define_skeleton` and `define_clip` accept an empty model, since `ModelStore.DefineSkeleton` and `DefineClip` never check for meshes. `export` checks `model.Combined is null` before the mode is looked at, so `mode: "clips"` is refused with "there is no mesh yet". That mode writes no mesh, and D-6 describes it as one file of clips for every character that shares the skeleton.

`save` refuses the same model (SaveTool.cs:92), and `CarveFile.Read` refuses `meshes: []` (CarveFile.cs:373). The work cannot be kept at all.

Failing case: `define_skeleton {bones:[...]}`, `define_clip {...}`, then `export {"path":"lib.clips.glb","mode":"clips"}` gives "export: there is no mesh yet; call define_mesh first ...". The description lists only "no skeleton or no clips" as clips-mode refusals. Either clips mode should skip the mesh check, or the description should say a mesh is needed.

**Outcome:** fixed

Answer: 42f422a, same as B2

### D6. `render`'s no-Vulkan list of working tools is stale

**Severity:** Nit

**Where:** `src/Carve.Server/RenderTool.cs:232`

The description says "define_mesh, open, save, export, move, extrude, get_mesh, undo, select and check still work there". It leaves out the seven M-16 tools, which need no device either.

**Outcome:** fixed

Answer: a31a4e9, the no-Vulkan list names all 17 other tools; text only

### D7. Some new refusals format numbers in the current culture

**Severity:** Nit

**Where:** `src/Carve.Server/RenderTool.cs:349`

`$"time: {clipTime} s is outside the clip ... from 0 to {sampled.Duration} s"` and `{q.Length():G6}` (RenderTool.cs:116) use the machine's culture. So do `Clip.cs:124` (`{duration} seconds`) and `VertexWeights.cs:70` (`{weight}`). Every other number in a result is written with `InvariantCulture`. On a de-DE machine an agent reads "time: 2,5 s is outside the clip 'Walk', which runs from 0 to 1,2 s".

**Outcome:** fixed

Answer: 65a64a4, same as B5

### D8. `ModelStore.DefineTarget` has no caller, and its doc describes a `define_mesh` behaviour that does not exist

**Severity:** Nit

**Where:** `src/Carve.Server/ModelStore.cs:60`

`DefineTarget` resolves a null name to "the only mesh". Nothing calls it. `define_mesh` uses `Define` with the default name `mesh`, so `define_mesh` without a name on a model whose one mesh is `crate` adds a second mesh, as T-29's spec says. A reader of the store could take `DefineTarget` for the rule. It should be removed.

**Outcome:** fixed

Answer: e20aa4f, same as A6

### D9. T-46's As built still says clips mode replaces an existing file

**Severity:** Nit

**Where:** `docs/tasks/T-46-clips-only-export.md:43`

The note says "An existing file is replaced (reported), as for the model mode", and that the schema test expects `path, allow_unweighted, mode`. Since T-52, an existing file is refused without `overwrite: true`, and the schema has `overwrite`. The refactor pass (2358c52) edited this same paragraph and left both statements. The note should say they were superseded by T-52.

**Outcome:** fixed

Answer: a9233cd, T-46's As built marks the schema expectation and "an existing file is replaced" superseded by T-52; docs only

### D10. CLAUDE.md's layout table was not brought up to date for the samples and the character run

**Severity:** Nit

**Where:** `CLAUDE.md:77`

The `samples/` row names only `leaky-shed.carve`, "kept equal to `TestMeshes.LeakyShed()` by a test". It does not mention `gardener.carve`. Since T-35's ruling the shed sample is version 2, compared by content, with a version 1 fixture in `tests/Carve.Core.Tests/Fixtures`. `tests/Carve.CharacterLoad.Tests` and `scripts/run-character-scenario.ps1` have no rows of their own, while their export counterparts (`tests/Carve.ExportViewer.Tests`, `scripts/run-export-scenario.ps1`) do. That matters for the skip-when-unset behaviour of `CARVE_CHARACTER_RUN`.

**Outcome:** fixed

Answer: b6e047f, CLAUDE.md's samples/ row names gardener.carve and the version 2 shed compared by content with its version 1 fixture; new rows for tests/Carve.CharacterLoad.Tests (CARVE_CHARACTER_RUN, skipped when unset, a skip fails the script) and scripts/run-character-scenario.ps1; docs only

## Summary

Round 1 of M-16 (85 files, about 9,700 lines). The diff was split by area, since members overlapped in `src/Carve.Server`: (A) the Core model, weights, skinning and the sampler, T-29 to T-33; (B) the file formats, Carve file v2 and the skinned and clips `.glb`, T-34, T-35, T-46, T-52; (C) the scenarios and checks, read for whether each can fail; (D) the whole diff, for what falls between. Each reviewer was claude-opus-5-5, as strong as the strongest implementer. 30 findings: 1 Major, 14 Minor, 15 Nit. All 30 were fixed: product and docs by two claude-opus-5-5 fixer agents in the milestone worktree, tests and scenarios by a third in its own worktree. B2/D5 and B4 were decided by the person on 2026-11-09. After the fixes, `cairn milestone M-16 verify` passed every suite (7 ran, 2 carried), and all 105 frames and exported files are byte-identical to the refactor pass's list, fetched by job id at b6e047f.

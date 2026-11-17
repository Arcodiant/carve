---
description: >-
  Clips-only export, loop and in-place checks, and proof that a game's load of the .glb matches Carve's own preview
---
# M-19 — A clip library for a game

**Status:** Active

**One-liner:** *Clips-only export, loop and in-place checks, and proof that a game's load of the .glb matches Carve's own preview*

## What this milestone delivers

Orcward's *Done when*. Clips are checked and read back, a chosen piece exports to its own `.glb` for costumes swapped at runtime, and `render_glb` shows a `.glb` through the engine's own loader and skinning, so a loaded frame can be compared with Carve's preview. Fresh agents then do the whole job from the tool descriptions.

Content: the gardener's clip library, and the frames comparing game and preview.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06.

**Depends on:** M-17, the last before it (Orcward's order: M-16, M-18, M-17, then this).

**Order.** T-45, T-47 and T-50 side by side; T-48; T-49. Stronger model on T-47, Sonnet on the rest.

**The frame rule** is Orcward's, in D-6. Only the person may loosen it, on differences measured at T-48.

## Required tasks

- [ ] [T-45](../tasks/T-45-clip-checks-and-read-back.md) — get_clips reads clips back, and looping clips that do not close or clips that travel are warned about
- [ ] [T-47](../tasks/T-47-render-a-loaded-glb.md) — render_glb draws a .glb through Empyrean's own loader and GPU skinning at a clip and time, framed as Carve's preview is
- [ ] [T-50](../tasks/T-50-piece-export.md) — export writes chosen meshes to their own .glb with every bone in the same order and no clips, so a costume can be swapped at runtime on the body's palette
- [ ] [T-84](../tasks/T-84-the-gardener-s-shoulder-is-coloured-as-its-sleeve.md) — The gardener's shoulder is coloured as its sleeve (added 2026-11-12, the person's note in T-47's review)
- [ ] [T-82](../tasks/T-82-render-draws-with-the-engine-s-pbr-material-as-a-loaded-glb-does.md) — render draws with the engine's PBR material, as a loaded .glb does (added 2026-11-12, the person's ruling on T-47's finding)
- [ ] [T-83](../tasks/T-83-loop-closure-compares-every-axis-of-a-translation.md) — Loop closure compares every axis of a translation (added 2026-11-12, the person's ruling on T-45's reading)
- [ ] [T-48](../tasks/T-48-orcward-done-when-scenario.md) — One suite proves the request's done-when: both exports, Load, LoadAnimationSet, and a loaded frame matching Carve's preview
- [ ] [T-72](../tasks/T-72-fold-not-planar-warnings-in-check-get-mesh-and-export.md) — Fold not-planar warnings in check, get_mesh, export, open and move (pulled in 2026-11-13, the person's ruling on T-49's stumble)
- [ ] [T-49](../tasks/T-49-character-acceptance-run.md) — Fresh agents build, rig, animate and export an original character from the tool descriptions alone, across two sessions

## Retrospective

**As planned.**

- **Orcward's *Done when* passes in one suite.** `scenario-orcward` (T-48):
  - builds the gardener with both looping clips through the tools;
  - exports the model, the clips and the Hat piece with no warnings;
  - loads them through `IModelGeometryLoader.Load` and `LoadAnimationSet`, with the same 22 bones, inverse binds and clips;
  - compares `render_glb` with `render` at rest and at nine clip times, four of them between keys. Every good frame differs by 0 px, apart from one frame at 2 px.
- **Each planted fault fails:** reversed winding, a dropped piece, a swapped bone order, swapped inverse binds, and all five twisted hands.
- **The pieces of it:**
  - `get_clips` and the loop and travel warnings (T-45, T-83);
  - `render_glb`, the `.glb` drawn through the engine's own loader and GPU skinning (T-47). A `SkinnedPose` draws on the headless host, so no engine need was recorded;
  - piece export with every bone, in order, and no clips (T-50).
- **Fresh agents did the whole job from the tool descriptions** (T-49). There were two rounds, each with two `claude -p` sessions on Opus 5.5. Both rounds passed:
  - an original character was built, saved, reopened, edited and exported three ways;
  - all three files load;
  - `render_glb` matches `render` within 1 px over 14 pairs;
  - no vertex is left unweighted.
- **The person approved every frame set put to review:** T-47's (after T-84's fix), T-84's shoulder crops, T-82's two baseline sets, T-48's round 2 and T-49's character.
- **`cairn milestone M-19 verify` (2026-11-13, at 91bb472, before this retrospective)** passed every check but `review`, which comes after the person's go:
  - 9/9 members done and merged;
  - all 12 suites pass, 11 ran and 1 carried.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-45 | Sonnet 5.5 | 1 |
  | T-47 | Opus 5.5 | 2 (a hand-off at the checkpoint; the second was resumed once after a question) |
  | T-50 | Sonnet 5.5 | 2 (a question) |
  | T-83 | Sonnet 5.5 | 1 |
  | T-84 | Sonnet 5.5 | 1 |
  | T-82 | Opus 5.5 | 4 (two questions, then the checkpoint) |
  | T-48 | Sonnet 5.5 | 2 (a question; the second was resumed after an API error) |
  | T-49 | Sonnet 5.5 | 2 (the checkpoint, with three questions) |
  | T-72 | Sonnet 5.5 | 1 |

  No rework was traced to the cheaper tier. Every extra agent was a stop for the person or a checkpoint.

**Not as planned.**

- **The milestone grew from five members to nine.** Each new member was the person's ruling on something a member found:
  - **T-82, `render` goes PBR.** T-47 found the loaded `.glb` about 0.73 as bright as `render`, with about 12 % of pixels past 8/255. The file's material loads as the engine's PBR material, while `render` drew with `LitMesh`. The person ruled that the preview draws as the game will. This lifted the byte-identical guard once, for T-82 alone, and every shaded frame changed (122 frames). Exported files, samples and non-colour views stayed byte-identical.
  - **T-84, the gardener's shoulder.** The person rejected T-47's first frames for a skin-coloured patch at the top of the arm. `render` showed it too. The torso's shoulder-slope segment was grouped as skin. It was made a member before T-82, so the frames were re-baselined once.
  - **T-83, loop closure on every axis.** The orchestrator's option text for the tolerance said "on X/Z" for both checks, and T-45 read it literally for loop closure too.
  - **T-72, pulled in from off-milestone.** In T-49's run, `move`, `check`, `open` and `export` on a subdivided character returned 74 to 120 thousand characters of not-planar warnings, which the client cut.
- **T-82 took four agents and three rounds of rulings.** PBR divides the directional light by about π, and margin after margin failed in turn:
  - first `MinimumStep` and the soft thresholds;
  - then five more checks;
  - then one cube frame;
  - then two server tests that no scenario run reaches.

  The light model the third agent wrote predicts the frames within 1 step, and it should have come first. With it, every failing check could have been listed and put to the person in one question.
- **One loosening landed without asking:** `NamedMeshTests.IsOrange` (red at least 120, was over 150; red over green at least 20, was 40). The agent read it as covered by the highlight ruling. The person accepted it afterwards.
- **D-6's frame rule changed** (the person, 2026-11-13). A small twisted hand passed the whole-frame 0.5 % rule: 159 to 599 px, against 1,311 allowed. A frame now also fails on a connected patch of more than 50 differing px (8-connected). Good frames' largest patch is 2 px, and the smallest twist's is 130 px. A wrong file must fail at least one frame, not every frame, since a rest frame carries no palette.
- **Rulings the person gave during the milestone,** all recorded in the members' As built and in D-6 where they change it:
  - T-45: a loop closes, and a root stays in place, within 1° / 1 mm.
  - T-83: loop closure compares all three axes. T-83 read "within 1 mm" as a 3D distance.
  - T-50: the Hat piece lives in the Character scenario's content folder, not in `samples/`.
  - T-82:
    - PBR with the light unchanged, and the margins lowered;
    - the highlight is a PBR-shaded tint;
    - "re-pick points, else lower" for the further checks;
    - `IsOrange` accepted.
  - T-48: the cluster rule, and "at least one frame".
  - T-49:
    - the prompts as drafted, with the sessions on Opus;
    - the planted bone passes, since the agent caught it from `get_clips` before any error;
    - the acceptance check has no suite.
  - T-72: name 10 faces and fold the rest.
- **Asked before dispatch, as the last two retrospectives advised:** T-45's tolerance, T-49's prompts and session model, and T-72's fold count. The other stops were findings no reading ahead would have caught.
- **Infrastructure:**
  - Several `verify` runs were killed by the 30-minute background limit while queued; later briefs asked for up to 2 h.
  - One `server-regression` run took 45 minutes with four timeouts under load (T-47).
  - T-48's continuation stopped on an API authentication error and was resumed.
  - `cairn test artifacts` writes a `test-artifacts/` folder into the worktree; T-82 committed it once, then removed it.
- **`finish` again could not remove any member's worktree folder.** `branches/task/T-45`, `T-47` to `T-50`, `T-72` and `T-82` to `T-84` are unregistered from git and left on disk.

**Never verified.**

- **A real game.** Every file is proved through the engine's loaders and `render_glb`, not loaded and played in Orcward. The shared-palette swap of a piece at runtime was not run in a scene. T-48 checks the file side only.
- **Hash guards that were partial or argued, not run:**
  - T-45 and T-83 changed no render code and hashed nothing.
  - T-50 hashed the Character suite only.
  - T-49 compared no hashes, after showing `src/` unchanged since T-48. The orchestrator confirmed that.
  - T-72 compared no hashes, since only warning text changed.
  - T-48's guard was the full one: seven suites, every frame and `.glb`.
- **Weak or partial checks:**
  - T-84's shoulder check cannot catch a mirrored camera, since the shoulders are symmetric.
  - T-82's cylinder-ends check alone does not catch a mirror; the normals look's checks do.
  - T-82's break columns were worked in the light model and on frames, not by breaking the product and rerunning.
  - The soft torso's smooth run sits at its limit of 5 with no headroom in the model.
  - T-47's loader-warning path is tested only with a fake loader.
  - T-49's round 1 session-1 `.carve` file was not kept.
- **Readings members landed that the spec did not settle** (in their As built):
  - T-50: a piece gets no height warning, chosen meshes keep the model's order, and duplicates are ignored.
  - T-72:
    - the "first 10" are the lowest face ids;
    - the fold line goes last, singular for 1, and per mesh in an export;
    - because the report code is shared, `define_mesh`, the other edit tools and `undo` fold too.
  - T-84 added `samples/gardener.carve` to `server-regression`'s inputs.
- **The orchestrator opened no frame.** Every look above is a member's or the person's.

**Known and left.**

- **T-85** (off-milestone): an environment map in the render scene, from the person's question on T-82's review.
- **T-86** (off-milestone): `server-regression` timing out on a loaded machine (below).
- **T-51** (per-game export limits) is still open.
- `CLAUDE.md` and the acceptance check's files are inputs of no suite; the latter by ruling.

**Test report** (`cairn test report --since 3d`):
- `server-regression` is again about 90 % of the slot time. It caught 9 failures, 7 of them unique to it.
- It has the window's one flake: T-47's run, which timed out under load. That is filed as **T-86**.
- Nothing else to file.

**Next time.**

- When a member changes something every frame depends on (a material, the light), have it model the effect first and list every check it moves, then put them to the person in one question. T-82 asked three times.
- Write option texts for a ruling so that each clause names the one check it applies to. T-83 came from "on X/Z" read across two checks.
- Expect a first fresh-agent run to find product problems, not only description problems. T-49's fix was code (T-72); its description notes changed nothing.
- Keep naming the long `verify` limit and the `test-artifacts/` trap in every brief.
- Clean up the worktree folders `finish` leaves behind.

### The rebase onto main (after the person's go, 2026-11-14 to 2026-11-16)

- **Two rebases, by three Opus 5.5 agents.**
  - The first was onto `fb96f44` (0.8.2: M-5's cuts and gatehouse, B-2, T-51).
  - The second was onto `5bc4559` (M-6's UVs, 0.9.0).
- **Every conflict kept both sides:**
  - Export reads the game's limits (T-51) in every mode.
  - A piece warns on triangles and submeshes but not on height (`GameLimits.Warnings(..., wholeBody: !asPiece)`), which keeps T-50's rule.
  - After the limit warnings come the UV warnings, the clip warnings (T-45) and the folded not-planar lines (T-72).
  - The tool list keeps M-6's `set_uvs`, `unwrap` and `project_uvs`, and adds `get_clips` and `render_glb`.
  - `RenderSystem` keeps M-6's checker and seam lines beside T-47's glb fields, and a loaded glb hides the seams too.
  - M-6's checker look keeps its own `LitTextured` material, and `uvs` stays unlit. Only the model's own colours are drawn with PBR (T-82).
- **What the rebases broke, and the fixes:**
  - T-72's leaky-shed test now sends each face's `Loop`, because main's T-63 made faces objects.
  - M-5's colour checks (`ColourRule`, within 2) failed under PBR by 1 to 5 levels: PBR adds a specular term that does not scale with the colour.
    - **The person's rulings (2026-11-14):** first "Re-work the prediction", then "Yes, diffuse + specular".
    - `ColourRule` now predicts T-82's diffuse plus the standard PBR specular from the material's own settings (F0 0.04, roughness 1, light 0.9). The tolerance is still 2. The diffuse is not reduced by Fresnel; that was set by matching the frames.
    - The cuts, gatehouse and crate colour checks pass under it.
    - Shown failing in the model, not by breaking the product. A face left grey fails every check. Drawn without the specular, it fails every check but the pocket floor and the crate's +X band.
  - `knife-slab-recess`'s lower-left pocket wall is within 2 of the floor under PBR (n.l 0.655 against 0.621).
    - **The person's ruling (2026-11-16):** "Drop that wall from it".
    - The wall leaves the differs-from-the-floor check and keeps a colour check of its own (`ColourRule.Lit`): predicted (51, 84, 108), drawn (51, 84, 107).
  - M-6's byte-identity guard on `gardener.glb` held main's pre-UV hash, and T-84's shoulder fix changed the gardener's bytes.
    - The guard now holds M-19's pre-UV hash (181BE468...), still an exact check.
    - **The person accepted the new pin (2026-11-16).**
- **Frames:**
  - Against `main` at 5bc4559, 10 of 16 `scenario-cuts` frames and 23 of 26 `scenario-gatehouse` frames changed under PBR. `scenario-uvs` changed 0 of 22.
  - **The person approved 16 pairs** (`C:\Users\andre\artifacts\carve\M-19\rebase\round-1`) on 2026-11-16.
- **`cairn milestone M-19 verify` at df70148:** every check but `review` passes, across 14 suites (7 ran, 8 carried).
- **Not checked:**
  - The colour and wall breaks were worked in the light model, not by breaking the product.
  - In perspective frames, the specular uses the camera's direction, not each pixel's.

### Hand-off

Third rebase, onto `origin/main` 720a6fa (M-7, 0.10.0). **Rebased locally, not pushed** (`origin/milestone/M-19` is still 7ac5790). The earlier hand-off (the sky model, the eleven-row table) is in git at 6d575ac.

- **Ruling (the person), 2026-11-17, "Accept all":** every row of that table as proposed, D-6's rule to luma past 4, the gardener pin to M-19's hash.
- **Done by the fourth agent (Opus 5.5), commit 6335267** (build warnings-as-errors clean). Every changed check cites the ruling and date:
  1. Front views: `CheckFrontOrder` (cube, crate, crate-sheet-four; export via `CrateBuild.CheckFront`), top over +Z and each face over the background; the back view keeps `CheckOrder`. `RenderTests`' three-face test skips +Z over +X in the Front case only.
  2. `cube-zoom3-corner` expects 2 shades.
  3. Cylinder: `sideOther` is facet 1, the pair held to `SideEnds` 4 (flat, smooth, subdivided); the smooth facet-14 grade check removed (the flat plateau `CheckSame` kept); the soft-rimmed side's other end is facet 1 at mid height pulled to 0.9 radius (`softSideOther`), `MinimumStep` 6.
  4. Soft torso: `FacetStep` 5, `SmoothStep` 4; the span counts distinct luma shades, smooth 5 or more, flat 3 or fewer.
  5. `ColourRule.GreyRedLessBlue(normal, direction)` (and `ColourRule.Encode`); crate and shed "not highlighted" is R - B within 6 of it. `ColourRule.cs` is now linked by Shed, Character, Soft, Orcward and Uvs.
  6. `GardenerPixels.ExpectGroup(..., normal, direction)` classifies against the palette lit by `ColourRule.Lit`; used by both `CheckRest`s (each face's own normal, `MeshSpec.Normal`, Newell). The other `ExpectGroup` points keep the bare palette (they passed in the sweep).
  7. `Comparison.Differs`: luma (0.2126, 0.7152, 0.0722) of the sRGB difference past 4 (`PixelThreshold` 4.0); comments in the Orcward and Acceptance scenarios, `OrcwardLoadTests`, CLAUDE.md; D-6 records the rule, the date and the measured table.
  8. `ExportHashTests` pinned to `F67B98974F443A7BA59A8694E547FCD2416C3303232025E2A4319C468237BD63`. Probed in full, all 64 digits: M-19 now and 7ac5790 (a probe test in a temporary detached worktree, removed) both write it.
- **Shown failing** (local runs of cube, crate, shed, soft, character with a temporary `BreakProbe`, reverted, not committed; patch and log in `C:\Users\andre\artifacts\carve\M-19\rebase\round-3\breaks\`). Each broken frame is made from the run's own frame:
  - Row 1: cube-front flipped upside down fails (top 201 against +Z 201). Mirrored in X it is not caught (the check never told a mirror apart; +Z/+X did).
  - Row 2: the corner frame in one shade fails (1 shade, expected 2).
  - Row 3a: flat, smooth and subdivided sides in one shade fail (197/197). Measured 191 against 197 (6 apart, limit 4). Mirrored or flipped frames are not caught: those points still differ.
  - Row 3c: the soft side in one shade fails (199/199). Measured 188 against 199 (11, limit 6). Mirrored or flipped frames not caught.
  - Rows 4a/4b: the flat frame judged as smooth fails (step 7 > 4); the smooth one judged as flat fails (step 2 < 5).
  - Row 5: the crate's frame band with every face highlighted fails (R - B 104 against the grey's -47); the shed's 18 of 18 points fail the same way.
  - Row 6: the skin face's pixel read as boots fails in the character and both soft frames (reads skin, match 1.000).
  - Row 7: the twists themselves are the breaks; not yet read off this round's `differences.tsv`.
  - Row 8: the old pin failed against M-19's bytes (run above).
  - A caught probe still sets the run's `failure` in `run.json`, so these local runs exit 1 on the probe's message. That says nothing about the real checks.
- **Left:**
  1. `cairn milestone M-19 verify` at 6335267 (or this hand-off's commit) was started and still queued behind other projects' runner jobs when this agent stopped. Rerun it, background, and paste its output. Every check but `review` must pass, including `scenario-sky` and `OrcwardLoad` under the luma rule; read the twists' patches and the good frames' from its `differences.tsv` for D-6's table.
  2. Compare every suite's frames by hash against `main` at 720a6fa; the review (`mcp__waystone__review`, under `round-3\review`, at most 32 files, main beside M-19: cube, crate, shed, gardener at rest and in a clip, soft gardener, cuts and gatehouse, an Orcward triptych; context: the sky's blue cast and flatter contrast are expected under the ruling).
  3. Replace this hand-off with a short paragraph in *The rebase onto main*, commit, check `origin/main` is still 720a6fa, `git push --force-with-lease origin milestone/M-19`, `cairn lint`.
- **Not checked:** the luma rule on T-49's acceptance rounds (no round folder run); the lit-palette rule only on the `CheckRest` points; the row 7 twists under the luma rule only in the round-3 model (`twist-measures.tsv`), not yet in a run.

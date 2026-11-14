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

### Hand-off

The rebase onto `origin/main` at fb96f44 (0.8.2: M-5, B-2 and T-51) is done locally and not pushed; it stopped for the person.

- **Done.** Conflicts resolved in T-45, T-50, T-47, T-48, T-49 (twice) and T-72, keeping both sides:
  - export reads the game's limits in every mode, piece mode included;
  - a piece warns on the game's `max_triangles` and `max_submeshes` but not on height (`GameLimits.Warnings(..., wholeBody: false)`);
  - clip warnings and the not-planar fold come after the limit warnings.
- **One fix on top**, 356b578: T-72's leaky-shed test sends each face's `Loop` (main's T-63 made faces objects). Checked by running `NotPlanarFoldToolTests` (4/4).
- **Verify at 147bb3c** failed `review` (expected), `scenario-cuts` (12 checks), `scenario-gatehouse` (43 checks) and `server-regression` (the test fixed above).
- **For the person.** The cuts and gatehouse failures are main's M-5 shade predictions: `ColourRule` from a grey face at `Checks.Exact`, under M-5's rulings of 2026-11-12. With T-82's PBR render they read 1 to 5 levels brighter than predicted, for example the door at (72, 46, 25) against (71, 43, 20). Making them pass would mean loosening the tolerance or re-modelling the prediction for PBR. Which rule wins is the person's call.
- **The person's ruling (2026-11-14):** "Re-work the prediction": "ColourRule predicts the PBR result from T-82's light model (which reproduces frames within 1 level), and the tolerance stays at 2. Each changed check shown failing on its break. The crate's colour check, which passes today, follows the same rule."
- **Second agent (Opus 5.5): T-82's model, as written, cannot hold the checks; stopped for the person again.** T-82's model (linear = albedo x (0.25 + 0.9 n.l / pi), light turned with the camera) is pure multiplication by albedo, so it predicts what straight multiplication does, up to its own error on grey; it was only ever checked on grey faces. The coloured faces carry an extra term that does not scale with albedo: PBR's specular lobe. Worked in the model (light and n.l from `tolight`, as T-82 did; script `%TEMP%\m19\model.py`), against the failing pixels from the last queued run:

  | Face, view | Measured | T-82's model | Model + specular | Specular (linear) |
  |---|---|---|---|---|
  | door `#6b4423`, +Z, front (0.6, 0.35, 1) | (72, 46, 25) | (71, 44, 20), B off by 5 | (72, 46, 25) | 0.0024 |
  | door, +Z ortho | (77, 49, 27) | (77, 48, 23), B off by 4 | (78, 50, 27) | 0.0027 |
  | bottom wall `#4a7a9b`, +Y, front | (52, 85, 108) | (49, 84, 107), R off by 3 | (52, 85, 108) | 0.0037 |
  | grey (0.72, 0.72, 0.70), +Z, front / ortho | | 151 / 162 | 151 / 163 | |

  The specular term is the standard Cook-Torrance one with the shipped constants, added on top of T-82's diffuse with no fitted number: F0 0.04 (dielectric, `GlbExport.Metallic` 0), GGX at `GlbExport.Roughness` 1 (D = 1/pi), Smith G with k = (r+1)^2/8 = 0.5, Schlick Fresnel on v.h, the same light strength 0.9 as the diffuse, v the camera direction. One choice was made by the match: the diffuse is not scaled by (1 - F). With (1 - F) the door is (71, 45, 24) and the wall (51, 84, 106), still within 2. On grey the term is under 1 level, which is why T-82's model matched grey frames.
- **Question for the person.** Implement `ColourRule` as T-82's diffuse plus this specular term (every check above within 2 at `Checks.Exact`, tolerance unchanged), or some other rule? Nothing is changed in code yet; the branch is still unpushed at the commit holding this Hand-off.
- **The person's ruling (2026-11-14) on that question**, word for word: "Yes, diffuse + specular": "ColourRule predicts T-82's diffuse plus the standard PBR specular from the material's own settings; tolerance stays 2. Every check (cuts, gatehouse, crate) must land within it, each changed check shown failing on its break; any that doesn't comes back to you." The diffuse is not reduced by (1 - F): set by matching the frames, not by the settings (recorded at `ColourRule` too).
- **Done by the second agent:** `ColourRule.Predict` and `ColourRule.Specular` (the light turned with the camera as `ViewFraming.LightOrientation` does, ported as `ToLight`); a coloured pixel is (the grey pixel of the same normal in the same frame - specular) x colour / grey + specular, in linear light. `CutsChecks.Predict` (knife, gatehouse) and the crate's `CheckSurface` use it; `Camera.Direction` added. Built with warnings as errors.
- **Verify at b4f28b7:** every colour check now passes (`scenario-crate`, `scenario-gatehouse` and the cuts' 12), and so does `server-regression`. `review` fails (expected), and `scenario-cuts` fails 3 checks of another kind, so the run has stopped for the person again. `knife-slab-recess`'s wall check needs the pocket's lower left wall to differ from the pocket floor's predicted shade by more than 2 (`Checks.Exact`) in at least one channel. The wall reads (51, 84, 107) at <1.17, 1.1, 0.175>, <1.2, 1.05, 0.175> and <1.23, 1, 0.175>, against the floor's predicted (49, 82, 105): 2 apart in every channel. In the model the wall (n.l 0.655) is (51, 84, 107) and the floor (n.l 0.621) is (50, 83, 105), so under PBR the two really are within 2. The old prediction put the floor lower, at about (48, 81, 104), and that is the only reason the check passed before. The upper left wall (n.l 0, (35, 62, 81)) and the bottom wall ((52, 85, 108)) are not affected. **Question for the person:** drop the lower left wall from that check, check it some other way, or something else? The margin is not loosened without a ruling.
- **Shown failing, worked in the model** (script `%TEMP%/m19/breaks.py`). Each figure is the largest channel's distance from the prediction in levels; the check fails past 2. The crate judges in linear light at its own 8 % + 0.004, unchanged:

  | Changed check | Predicted (new rule) | Drawn as the model says | Break 1: drawn without the specular | Break 2: the face left grey |
  |---|---|---|---|---|
  | cuts knife: door, +Z, front | (72, 46, 25) | 0, passes | (71, 44, 20), 5, fails | (151, 151, 149), 124, fails |
  | cuts knife: pocket floor, +Z, front | (50, 83, 105) | 0 | (48, 81, 105), 2, passes | 101, fails |
  | cuts knife: bottom wall, +Y, front | (52, 85, 108) | 0 | (49, 84, 107), 3, fails | 103, fails |
  | gatehouse: door, +Z, front | (72, 46, 25) | 0 | 5, fails | 124, fails |
  | gatehouse: door, +Z ortho | (78, 50, 27) | 0 | (77, 48, 23), 4, fails | 134, fails |
  | crate: lid panel, +Y, front | (44, 86, 144) | 0 | (40, 84, 143), 4 | 115, fails |
  | crate: +Z band, front | (132, 52, 39) | 0 | (132, 50, 36), 3 | 105, fails |
  | crate: +X band, front | (125, 48, 36) | 0 | (124, 47, 34), 2 | 100, fails |
  | crate: lid panel, +Y, tilted | (46, 91, 152) | 0 | (43, 89, 151), 3 | 121, fails |
  | crate: lid rim top, +Y, tilted | (152, 61, 46) | 0 | (151, 58, 43), 3 | 119, fails |

  Every check fails on break 2 (the face not coloured). Break 1, a frame where the old multiplication would be right, is caught wherever the specular adds more than 2 levels, but not on the pocket floor or the crate's +X band. The breaks were worked in the model, not by breaking the product.
- **For the review:** main's frames are queued at fb96f44 as jobs J-20261114-045642-7b4cde (scenario-cuts) and J-20261114-045643-35a163 (scenario-gatehouse). Fetch them with `cairn test artifacts <job> --out <a folder outside the worktree>`, and M-19's with `cairn test artifacts scenario-cuts` / `scenario-gatehouse`. Compare by hash first, then put the changed pairs to `mcp__waystone__review` under `C:\Users\andre\artifacts\carve\M-19\rebase\round-1`.
- **Left (was).** After that ruling: the fix (the prediction needs each probe's face normal and the frame's camera direction, both of which the scenarios already have from the ray test), the shown-failing table, verify again, the review of main's frames beside M-19's, `git push --force-with-lease origin milestone/M-19`, then `cairn lint`.

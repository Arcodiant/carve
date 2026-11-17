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

Third rebase, onto `origin/main` 720a6fa (M-7, 0.10.0), by three Opus 5.5 agents, 2026-11-16 and 2026-11-17. **Rebased locally, not pushed** (`origin/milestone/M-19` is still 7ac5790; `origin/main` was still 720a6fa on 2026-11-17).

- **Rulings (the person):**
  - 2026-11-16, "Colour faces PBR under the sky": every colour face is PBR and lit by M-7's sky. Every shaded colour frame changes and goes to review. `ColourRule` and T-82's thresholds are re-derived with the sky, with one question to the person.
  - 2026-11-17, "Carry on as ruled": keep the default sky on every colour face; measure it, re-derive every moved check (cube margins near 2), and put a new twisted-limb rule to the person.
- **Done by the first agent:** the rebase, every conflict kept both sides (export's description and warnings, the tool list, `save`'s warnings, `glb.Primitives` with `wholeBody: !asPiece`, `RenderSystem`'s materials beside the glb fields, CLAUDE.md), and the build fix (`SurfaceMaterial` names `Empyrean.Material`; `FaceColourTests` takes `out materials`).
- **Done by the second agent (30ad636):**
  - **The sky measured.** New scenario `scenario-sky` (`scenarios/Carve.Scenario.Sky`, suite in `tests.json`): a unit quad facing each of 26 world normals, drawn grey, black and white, from `front`, `back` and straight on, the centre pixel read. `sky.tsv` in the run folder; a copy and the fitting scripts (`fit.py`, `fitspec.py`, `model.py`) in `C:\Users\andre\artifacts\carve\M-19\rebase\round-2\sky\`.
    - A black face is specular alone, so (grey - black) / albedo - (0.25 + 0.9 n.l / pi) is the sky's light. The grey and white faces give the same value within 0.006 in linear light, and the same normal from two views agrees within 0.013.
    - The sky's light fits a first-order SH, constant plus linear in the world normal, within 0.009 linear: R 0.332 - 0.050 n.y, G 0.416 + 0.043 n.y, B 0.575 + 0.220 n.y (x and z terms 0.013 or less). It is about 1.3 to 2.3 times the 0.25 ambient: this is the +55 to +60 on a grey face and the blue cast.
    - The sky's specular is 0.003 to 0.02 linear. It fits the sky's light from the reflected direction times 0.0128 + 0.0267 (1 - n.v)^2.
  - **The model extended.** `ColourRule.SkyLight`, `SkySpecular` and `Reflected` (light + sky specular, per channel). `Predict` and `Lit` use them, and `CrateColours` reports the per-channel specular.
    - Against the 47 measured views: grey within 1 level in every channel, white within 2 (blue clips at 255), black within 3. T-82's model without the sky misses grey by up to 64 (G) and 87 (B).
    - `scenario-sky` checks every pixel against the model (2 levels; black 3) and passes.
    - It predicts the failing pixels: cube-front +Z (192, 201, 216) and +X (189, 199, 213) are the quads' measured values exactly.
- **Every moved check, past the first-failure stops.** `ScenarioDriver.Fail` was made non-fatal for one local run and reverted (not committed). Logs: `round-2\sweep\`, one per suite.
  - With the extended `ColourRule`, **scenario-cuts and scenario-gatehouse pass whole** (were 15 and 43), the gatehouse's load checks included. So does every ColourRule check in the crate.
  - The load checks after the scenarios, run by hand on the sweep's run folders: `ExportViewer` 5/5, `CharacterLoad` 7/7 and `SoftLoad` 12/12 pass; `OrcwardLoad` fails `Every_twisted_limb_fails_as_a_patch` only.
  - What still fails, grouped (value now / needed):
    1. **Cube +Z over +X by MinimumStep 6:** 201 against 199 (crate 202/199) in cube-front, -256, -1024, crate-front, crate-sheet-four, export's five `front*` frames, and `RenderTests.A_cube_shows_three_faces...(Front)` in server-regression. The model predicts a step of 2. Top over +Z (216/201) and the back view's -Z, -X, underside (213, 204, 181) still step by 6 or more.
    2. **cube-zoom3-corner, three shades:** 2 shades (199, 216); +Z and +X merge, the same 2-level step.
    3. **Cube's cylinder sides (dir 2,2,1 ortho):** flat 191/193 needs a step of 6; smooth 191/193 needs 6 and 193/194 needs 3; subdivided 191/193 needs 6; soft rims 188/192 needs 6.
    4. **Soft gardener's torso:** flat facet edges step by 7 at (224, 242) and (316, 242), needing 9; smooth torso has a run of 7 equal pixels, needing 5 or fewer.
    5. **"Not highlighted, |R - B| at most 6":** a grey face now has R - B of 24 to 48. Crate-highlight-front, 8 points at (197, 216, 245); shed-open-edges, 18 points on faces 3 and 4 at (189, 199, 213) and (192, 201, 216). This is shed's whole 18.
    6. **Boots read as skin:** gardener-rest-front, soft-flat-rest-front and soft-smooth-rest-front, the boots point (295, 448) at (71, 49, 39), match 0.996 to skin.
    7. **Orcward twist:** `twist-Hand_L-Y45` differs at 41 px, largest patch 23. **Under the sky one good frame also has a 23 px largest patch** (the other good frames have 0). The twists are 23, 91, 264, 451 and 623. So no patch-size limit at 8/255 separates Y45 from the good frames. A rule needs another measure: the pixel threshold, or a patch on the hand's own region. `round-2\sweep\orcward-differences.tsv` holds the table.
    8. **`ExportHashTests.The_soft_gardeners_skinned_export_is_the_bytes_written_before_materials`:** pins main's 259F5B54..., and M-19's bytes are F67B9897... (T-84's shoulder fix). This is the M-6 pin case again. Not yet checked: that 7ac5790 (before M-7) writes the same F67B9897 bytes.
- **Done by the third agent (2026-11-17): the table, for the person.** No threshold changed. Scripts and outputs in `C:\Users\andre\artifacts\carve\M-19\rebase\round-3\`: `colour.py` (`ColourRule` ported; it gives the cube-front faces within 1 of the drawn ones), `cube.py`, `gardener.py`, `twist.py` / `twist.tsv`, `twist-measures.py` / `twist-measures.tsv`, `breaks.py`.
  - **The 23 px patch in a good frame** is `walk-0.250-side`, x 271-276, y 372-379: one trouser facet of the walking leg, blue up to 10 apart, green 3 or less, red 1 or less (`render` (57, 80, 160) against `render_glb` (57, 78, 151)). The two draws give that facet slightly different normals between keys (posed face against skinned corners). Before the sky this was under 8. The sky's light is mostly blue and grows with n.y (B 0.22 n.y), so a small normal difference is now a blue step. `walk-0.125-side` and `walk-0.875-side` show the same at 5 or less.
  - **No patch limit on the largest channel separates at any threshold below 8.** At each threshold, the worst good frame's largest patch against `twist-Y45`'s: 1066/458, 585/354, 222/220, 142/101, 106/101, 76/95, 49/23, 23/23 (thresholds 1 to 8). Only 6 puts Y45 above, by 19 px.
  - **A brightness difference does separate.** Rec. 709 luma of the sRGB values, a pixel differing past 4: every good frame's largest patch is 2 or less, and the twists' are 101, 185, 396, 285 and 597. Every wrong-file break still fails: patches 268 to 26231; `swapped-bone-order` and `wrong-inverse-binds` rest-front stay 0, as now. Ignoring blue (largest of R and G past 4) also separates (good 0, Y45 101), but it is less principled.
  - **The gardener pin:** 7ac5790 (M-19 before M-7) writes `F67B98974F443A7BA59A8694E547FCD2416C3303232025E2A4...`, the same as M-19 now (the first 50 hex digits, as xunit prints them). A probe test was run in a temporary detached worktree, since removed.
  - **The table** (G is green; "measured" is the sweep's frames, run folders `artifacts/scenarios/<name>/1`):

| # | Suite: check | What it checks | Now | Needed | Model (sky; no sky) | Proposal | Kind | New number |
|---|---|---|---|---|---|---|---|---|
| 1 | scenario-cube (`cube-front`, `-256`, `-1024`), scenario-crate (`crate-front`, `crate-sheet-four`), scenario-export (five `front*`), server-regression (`RenderTests.A_cube_shows_three_faces...(Front)`) | Front view: +Z over +X by `MinimumStep` | G 201/199 (crate 202/199): 2 | 6 | 201/198: 3, error 1 (146/138: 8) | Drop the +Z/+X pair from front-view frames. Keep top over +Z (216/201, 15) and every face over the background. The three-face order stays in the back view: measured 213/204/181, model 213/204/180, steps 9 and 23. No channel or other front-side view keeps 6 with headroom: R and B step by 3; (1, 1, 1) gives 2, (2.2, 1.2, 1.6) gives 5. | dropped | none (top over +Z: 15) |
| 2 | scenario-cube: `cube-zoom3-corner` | Three face shades, 6 apart | 2 shades (199, 216) | 3 | 216 / 201 / 198: 2 shades | Expect 2 shades: +Z and +X merge, as in group 1. The alternative, the corner from below-back (-1.6, -1.2, -2.2) (9 and 24), means new corner and background points. | re-derived | 2 |
| 3a | scenario-cube: flat, smooth and subdivided cylinder, "two ends across the image" | Facet 13 against facet 3, G | 191/193 | 6 | 189/192 (119/131) | Re-pick facet 3 to facet 1. Measured bands on the flat side are 190, 193, 196, 197, 196, 194, 191 (facets 4 to 13): the side's whole range is 7. Facet 13 against 1 is 191/197, which holds 6 with nothing to spare, so lower this pair too. | re-picked + loosened | 4 (measured 6, model 8) |
| 3b | scenario-cube: smooth cylinder, grade across facet 14 | t 0.2 against t 0.8, G | 193/194 | 3 | 192/194 (5); the best of any lit facet is 3, facet 4 by the silhouette | Drop it. The normals look's red-across-facet-2 check already shows the smooth side grading within a facet, and flat stays a plateau (`CheckSame`). | dropped | none |
| 3c | scenario-cube: soft-rimmed cylinder, side ends | Point at 315 deg, y -0.3, against facet 3, G | 188/192 | 6 | 185-187 / 192 | Re-pick facet 3 to facet 1 at mid height. Measured 197-198 mid-side on row 340, so about 9-10; model 10-12. | re-picked, tolerance kept | 6 (about 9 measured) |
| 4a | scenario-soft: `soft-flat-rest-front` torso facet edges at (224, 242) and (316, 242) | Luma step across the octagon's edge (facets at 22.5 and 67.5 deg) | 7 and 7 (G 9) | 9 or more | 9 (15-16; T-82's worked 13/14). The smooth frame's step is 2 (model under 5). | `FacetStep` 9 to 5 and `SmoothStep` 5 to 4: flat 7 and smooth 2 each clear by 2. Or `FacetStep` 6 alone (flat clears by 1). | loosened | 5 (and 4) |
| 4b | scenario-soft: `soft-smooth-rest-front` torso span x 217-239, row 242 | Smooth is a ramp: longest run of equal luma at most 5; flat at least 10 | smooth 7 (flat 16) | 5 or less | ramp 128 to 134 over 23 px (measured 130 to 136); runs 4 if even, and 7 measured | Count distinct shades in the span instead of the longest run. Smooth has at least 5 (measured 7, model 7); flat at most 3 (measured 2: 131, 138). Or loosen the run to 8 or less (clears by 1). | rule changed | 5 or more / 3 or less |
| 5 | scenario-crate: `crate-highlight-front` (8 points); scenario-shed: `shed-open-edges` (18 points, faces 3 and 4) | Not highlighted: abs(R - B) at most 6 | R - B -48 (197, 216, 245); -24 ((189, 199, 213), (192, 201, 216)) | 6 | top -47, walls -24 / -25 (+2) | Compare R - B with the grey's own R - B from `ColourRule.Lit` at the face's normal, within 6. The highlighted lid panel's R - B at least 60 is unchanged; it passes. A simpler one-sided "R - B at most 6" would also hold. | re-derived, tolerance kept | 6 around the predicted -47 / -24 |
| 6 | scenario-character `gardener-rest-front`; scenario-soft `soft-flat-` and `soft-smooth-rest-front` | Boots face (295, 448) classifies as boots | (71, 49, 39): skin 0.9964, boots 0.9852 | boots | `Lit` boots (71, 49, 40), the pixel within 1: match 1.0000; `Lit` skin (214, 178, 157): 0.9860. `Lit` boots against `Lit` skin cos 0.9869 (bare 0.9692) | Classify against the palette lit by the sky (each colour's `ColourRule.Lit` at the face's normal, +Z here), not the bare colours. The sky's blue moves dark brown toward skin's direction. Under the same light they still differ, and their brightness differs by far more (luma 54 against 186). | rule changed | boots 1.000, skin 0.986 |
| 7 | scenario-orcward / `OrcwardLoad.Every_twisted_limb_fails_as_a_patch` (D-6's rule) | Every twist fails, every good frame passes | Y45 41 px, patch 23 (passes); good worst patch 23 (`walk-0.250-side`) | Y45 fails | n/a (the patch is a blue shading step, above) | D-6: a pixel differs when its luma (0.2126 R + 0.7152 G + 0.0722 B) differs past 4 of 255. The 0.5 % and patch-50 limits are unchanged. Good frames: patch 2 or less, 2 px or less differing. Twists: 101 to 597. Breaks: still fail. Changing `ComparisonPng.PixelThreshold`'s measure also moves T-49's acceptance step, which links it. | rule changed | luma > 4; good 2 / limit 50 / weakest twist 101 |
| 8 | unit: `ExportHashTests.The_soft_gardeners_skinned_export_is_the_bytes_written_before_materials` | Gardener-soft skinned export bytes pinned | F67B9897... | 259F5B54... (main) | n/a | Re-pin to M-19's own pre-materials bytes (F67B9897..., what 7ac5790 writes), as the M-6 pin was. | re-pinned, exact check kept | F67B9897... |

- **Left:**
  1. The person's answer on the table, then the changes it rules, and the sweep again.
  2. Then the hash comparison of frames against main, the review under `rebase\round-2`, the paragraph in *The rebase onto main*, the push and `cairn lint`.
- **Not checked:**
  - The sky's light was measured at quad centres only. Perspective frames use per-pixel view directions, and the model uses the camera's.
  - The table's proposals are worked, not run: no check was changed.
  - Group 1: `RenderTests`' Front case was not read. Group 3c: facet 1's value was read off a row, not at the re-picked point.
  - Group 6: the lit palette was not tried on the scenarios' other `ExpectGroup` points.
  - Group 7: the luma rule was not tried on T-49's acceptance rounds.
  - Group 8: the hash was compared on the 50 hex digits xunit prints, not in full.

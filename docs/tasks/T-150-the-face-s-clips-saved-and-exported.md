---
description: >-
  The face gets the clips Happy, Surprised, Angry, Talk and Blink, saved as samples/face.carve and exported to a .glb whose weights channels equal the clips' keys
---
# T-150 — The face's clips, saved and exported

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** T-145, T-146, T-147, T-149

**Verify:** server-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

The face's five clips are authored in Carve alone, through `define_clip`'s `weights`, then saved and exported. A game's loader reads the `.glb` with every clip and every weight key as authored.

## Scope

- **Clips** in `FaceSpec`, defined through `define_clip` in `scenario-face` (as agreed at planning):
  - `Happy`: 2 s, looping; Smile up to 0.8, BrowRaise 0.3; weights only.
  - `Surprised`: 1 s; BrowRaise 1, JawOpen 0.7; weights only.
  - `Angry`: 1 s; Frown 1, Head pitched 8° down; mixed.
  - `Talk`: 1.2 s, looping; JawOpen STEP 0 / 0.6 / 0.2 / 0.8, Head nodding 3° LINEAR; mixed, with mixed interpolation.
  - `Blink`: 0.3 s; Blink_L leads Blink_R by 0.04 s; weights only, asymmetric.
  - `get_clips` reads them back as given, and the loop closure warnings are silent.
- **Save:** `samples/face.carve` is rewritten as version 10, kept equal byte for byte to what the scenario saves, then reopened, with `get_clips` and T-149's frames byte-identical.
- **Export** (skinned `model` mode) into `game/Content/models/face.glb` in the run folder. Also `export mode: clips` once, to show its warning naming the weights it omits.
- **`tests/Carve.FaceLoad.Tests`** (`regression`, reads `CARVE_FACE_RUN`, skipped when unset, as `Carve.MorphLoad.Tests` does), the last step of `scenario-face`:
  - `face.glb` read through `IModelGeometryLoader.Load`: TargetNames in order `[Smile, Frown, BrowRaise, Blink_L, Blink_R, JawOpen]`, default weights 0, no warnings, and the five clips by name, each `Duration` equal to the clip's (the weights-only ones via T-147's held rest channel);
  - the weights channels read with SharpGLTF: one per clip on the mesh node; every key time and value equal to `FaceSpec`'s clip sampled at that time, target by target; unkeyed targets 0; and Talk's STEP held keys as T-147 wrote them;
  - `samples/face.carve` compared by content;
  - breaks shown failing: a swapped target pair in the export, a clip's keys shifted, Blink_L and Blink_R swapped.
- **Content for agents:** the face is the worked example the descriptions point at. Check that `define_clip`'s example (T-146) and the face agree on the target names.

## Notes

- **`scenario-face` in `**Verify:**`:** the suite doesn't exist until T-149 registers it in `tests.json`, so lint refuses it at planning. Add `scenario-face` to this task's `**Verify:**` line as its first commit (T-149: once the suite is registered).

- **Content and checks on settled formats:** Sonnet.
- **Runs in wave 3 beside T-148.** T-148 owns `scenario-morph` and the render_glb files. This task owns `scenarios/Carve.Scenario.Face`, `tests/Carve.FaceLoad.Tests`, `samples/face.carve` and the `scenario-face` suite inputs. A `tests.json` conflict only adds, so keep both sides.
- **The engine doesn't play the weights** (engine need 9, T-148), so the load checks read the channels with SharpGLTF. That's the interim, not a loosened check.
- **Stop and ask:** clip values that break T-149's worked frame list, and any pin you'd re-take.
- **Write down for dependents (As built):** the clips as given, and `face.glb`'s path in the run folder (T-151 and the person's live pass use it).
- **Carried:**
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-150` line given.

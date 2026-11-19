---
description: >-
  The face's clips are seen at their times by render and render_glb, played live in the viewer by the person, and compared frame by frame
---
# T-151 — The face's clips seen and played

**Status:** Open

**Pri:** P1

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** T-148, T-150

**Verify:** core-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

The motion is judged by the person, live, before any clip frame is approved. Then every agreed clip frame is drawn and checked, and the exported file's frames match the preview's.

## Scope

- **First, the person's real-time pass** (motion is a feel check, which a golden can't prove):
  - Open T-150's `face.glb` in the model viewer (T-148's clip keys).
  - Play each of the five clips, looping, from the front and the 3/4 view.
  - Ask the person whether each reads as its name, whether Blink's lead is visible, and whether Talk's steps pop as intended.
  - Write their answers into the As built.
  - A change to clip values goes back to T-150's `FaceSpec` through this task, with the sample rewritten. Stop for the person's ruling before changing them.
- **Then the clip frames in `scenario-face`**, by T-149's worked list, which is binding:
  - render frames 4–14 as agreed at planning: Happy, Surprised front and side, Angry, Talk at 0.15 / 0.3 / 0.75, Blink at 0.08 / 0.15 / 0.3, and Blink close on the eyes;
  - every pixel check is worked by `FacePlan`'s ray test at the clip's sampled weights and pose.
- **render_glb against render** at frames 4, 5, 7, 9 and 11, plus close crops on the mouth (9) and the eyes (11):
  - held to D-6's frame rule, including the connected-patch rule (50 px), since a mouth or lid change is small (M-19's lesson);
  - `differences.tsv` and `compare/` triptychs are written;
  - a control pair must fail.
  - Talk at 0.15 s lies between STEP keys, and Blink at 0.08 s is between LINEAR keys; neither time is itself a key (check this, per RV-52).
- **Breaks:** `CARVE_FACE_BREAK` `clipweights` (render ignores the clip's weights) and `glbclipweights` (render_glb ignores them) must each fail. So must T-149's `mirror` on a clip frame.
- **Byte-identity:** T-149's frames are unchanged.

## Notes

- **`scenario-face` in `**Verify:**`:** the suite doesn't exist until T-149 registers it in `tests.json`, so lint refuses it at planning. Add `scenario-face` to this task's `**Verify:**` line as its first commit (T-149: once the suite is registered).

- **Frames and a person's pass; little new code:** Sonnet, with three agents planned:
  1. the live pass with the person, then the frames;
  2. the frame-review agent, which opens every frame itself;
  3. a stop for the person's approval of the frames.
- **Goldens are approved only after the live pass**, by the planning rule for motion.
- **File ownership:** `scenarios/Carve.Scenario.Face` and `tests/Carve.FaceLoad.Tests`. Nothing in `src/` or the viewer. A viewer gap found in the live pass goes back as a question, not a fix here.
- **Stop and ask:** a frame or probe the worked list didn't foresee (put every miss in one question), and any tolerance you'd loosen.
- **Carried:**
  - each new check shown failing on a broken product, as a table;
  - evidence at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-151` line given.

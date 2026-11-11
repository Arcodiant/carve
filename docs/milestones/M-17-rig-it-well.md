---
description: >-
  Automatic weights, group overrides and read-back, with bones and weights shown in renders so skinning faults are seen before export
---
# M-17 — Rig it well

**Status:** Active

**One-liner:** *Automatic weights, group overrides and read-back, with bones and weights shown in renders so skinning faults are seen before export*

## What this milestone delivers

Rigging becomes practical. One call weights a mesh, a group override corrects it, and bones and weights show in renders, so a skinning fault is seen before export.

Content: the bones overlay, the weights look, and the gardener with planted faults.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06.

**Depends on:** M-18, in Orcward's order: one shared rig means few bodies to weight at first.

**Order.** T-37 then T-38, beside T-39 then T-40; T-41 last. Stronger model on T-37, Sonnet on the rest.

## Required tasks

- [ ] [T-37](../tasks/T-37-automatic-weights.md) — weigh gives every vertex up to four weights from bone proximity, deterministically
- [ ] [T-38](../tasks/T-38-weight-overrides-and-read-back.md) — An agent overrides weights on a face group, reads weights back, and is warned about unweighted vertices
- [ ] [T-39](../tasks/T-39-bones-in-renders.md) — render draws the skeleton over the model, with bone names available as labels
- [ ] [T-40](../tasks/T-40-weights-look.md) — render shows one bone's weights as a heat look on the model, so a stray or missing weight can be seen
- [ ] [T-41](../tasks/T-41-rig-scenario.md) — A scenario finds planted skinning faults through get_weights and the bones and weights looks, in its own suite

## Retrospective

**As planned.**

- **The slice works end to end.**
  - One call weighs a mesh (`weigh`, T-37).
  - A group override corrects it, and `get_weights` reads it back with warnings for unweighted vertices and for bones no vertex follows (T-38).
  - `render` draws the skeleton with `bones` and `bone_labels` (T-39), and one bone's weights as a heat look with `weights` (T-40).
  - `scenario-rig` (T-41) plants four faults on the gardener and finds each through those tools, then fixes it and re-checks the frame:
    - an unweighted patch, while `export` is refused, and succeeds once the patch is fixed;
    - a hat vertex on Shoulder_L;
    - Elbow_L 0.3 m too high;
    - the torso sides that `weigh` gives to the arm.
- **The order held:** T-37 beside T-39, then T-38 beside T-40, then T-41. The only rebase conflict was T-39 onto T-37, in `ChainPose.cs`. T-39 kept both sides.
- **Every existing frame stayed byte-identical** in each member that added frames, compared by hash before and after:
  - T-37: crate 44, character 17;
  - T-39: crate 44, character 17;
  - T-40: crate 50, cube 32, character 21.
  - T-38 added no frames. T-41's scenario is new and changes no tool.
- **Each new check was shown failing**, by mutation, with the tables in each member's As built.
- **The person approved every new frame set put to review:**
  - T-37's chain and gardener bends (round 1, with two notes, below);
  - T-39's five bone frames;
  - T-40's three weights frames;
  - T-41's eight before and after frames.
- **`cairn milestone M-17 verify` (2026-11-11, at 9a88ac8)** passes every check but `review`, which comes after the person's go. All 5 members are done and merged, and all eleven suites pass (1 ran, 10 carried).
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-37 | Opus 5.5 | 2 (a hand-off at the checkpoint, with one question) |
  | T-38 | Sonnet 5.5 | 1 |
  | T-39 | Sonnet 5.5 | 1 |
  | T-40 | Sonnet 5.5 | 2 (the second only reran `finish` after a network error) |
  | T-41 | Sonnet 5.5 | 1 |

  No rework was traced to the cheaper tier.

**Not as planned.**

- **M-17 could not start on the first try.** It depends on M-18, which was still Planned. The orchestrator stopped before `start`, and M-18 ran first, as the person chose.
- **Rulings asked before dispatch** (the M-18 retrospective's advice), both for T-37:
  - "explicit" means any existing weight, with no provenance and no file-format change;
  - a bone with several children uses the nearest of its child segments.
- **One ruling stopped a member:** T-37's `bones` scope. The orchestrator's brief had added a parenthetical the person had not ruled on. The person chose the reading the agent had built: a vertex is in scope when its nearest bone is one of the named ones, and it is weighted among the named bones only.
- **The person's notes on T-37's review:**
  - the chain's inner elbow pinches;
  - the gardener's torso sides follow the hanging arm, not the spine (Body agrees with the hand weights on 65% of vertices).

  Asked what to do with them, the person pointed at explicit weights as the answer. Nothing was filed. T-41 shows the flow on the torso: found in the weights look, fixed with a `define_group` plus a `set_weights` group override, read back, and kept through a plain `weigh`.
- **T-39 did not use `PositionVertex` line lists**, as its spec said. The engine docs say they depth-test, so a bone inside the body would be hidden. It draws with the engine's screen primitives (`DrawLine`, `DrawCircle`), magenta, 3 px, with no depth test. `bone_labels` rode T-29's label path, so M-4's work was not needed.
- **Bone lines and unweighted vertices are both magenta** (T-40). They are told apart only by shape when combined, so T-41 takes the two looks in separate calls.
- **Readings members landed that the spec did not settle:**
  - **T-37:**
    - the falloff `(d_nearest/d)^4`, recorded in D-6;
    - a vertex lying on several bones is shared equally among them;
    - a lone root bone is its head point.
  - **T-38:**
    - `get_weights` with neither argument reads every vertex;
    - its worked example (`{"vertices":[0,7]}`) is the member's own, since none was written anywhere.
  - **T-39:** a lone bone is a dot.
  - **T-40:** `weights` follows `pose` and `clip`, and replaces `mode`.
  - **T-41:** the torso group also moves the torso's shoulder-ring vertices to the spine.
- **Network errors on the test queue:**
  - T-40's `finish` failed on an Azure queue `URLError`, and a landing agent reran it cleanly.
  - T-37's first `verify` hit the same error.
  - T-38 saw one `LoaderTests` timeout under load, which passed on a rerun.
- **`finish` again could not remove a member's worktree folder**, because it was the agent's working directory. `branches/task/T-37` to `T-41` are unregistered from git and left on disk, as are older ones back to T-22.

**Never verified.**

- **A real game.** The rig is proved in Carve's renders and through `export`'s refusal, not loaded and played in Orcward's game.
- **Coverage gaps:**
  - `bones` and `weights` with a `views` sheet or with several meshes (T-39, T-40). The code path is shared, but no frame covers it.
  - `weights` on the gardener has no frame of its own in T-40; T-41's frames cover it.
  - T-41 has no posed frames, since its scenario defines no clips.
- **Weak checks:**
  - T-37's agreement with the hand weights is logged, not asserted.
  - T-37's weighed pose frame's mirror half was not shown failing on its own.
  - T-41's fixed `bones` frame cannot tell a mirrored camera, because Elbow_R sits at Elbow_L's mirror place. The planted frame does catch it.
- **Hash comparisons were local runs.** T-39 did not compare the cube; nothing in it uses a skeleton.
- **The orchestrator opened no frame.** Every look above is a member's or the person's.

**Known and left.**

- `render`'s description does not list `get_weights` among the tools that work in a pose (T-38 left it to `render`'s owner). This is for the refactor pass.
- Bone labels crowd around the gardener's clavicles and shoulders (T-39).
- Distance-only automatic weights pinch at joints and give torso sides to a hanging arm. Explicit overrides are the person's answer for now.

**After the person's go (2026-11-11).**

- **Rebase:** `origin/main` had not moved, so there was nothing to rebase.
- **Refactor pass (Sonnet 5.5):** it added `get_weights` to `render`'s pose list. It left `AutoWeights` and `BoneLines` apart, since sharing one segment function would change either the weights or the frames.
- **Review round 1, RV-29:** three Opus 5.5 reviewers (the weights side, the render side, the whole diff) gave 11 findings, 8 distinct. All were fixed by two Opus 5.5 agents, one handing off to the other, with a Sonnet 5.5 agent landing the fixes. Two notes:
  - **The stuck render pump** was the serious one. After a `weights` render, any model without that bone failed every later render. Two reviewers found it independently. The fix is covered by a `RenderPumpTests` case.
  - **The `Math.Pow` falloff** was replaced by exact multiplication for cross-platform bit-exactness, on the person's ruling. Every frame and `.glb` stayed byte-identical. The weighed gardener `.carve` changed in the last bit of 507 numbers.
- **Review round 2, RV-30:** one Opus 5.5 reviewer found nothing, and every RV-29 outcome held.
- **Next time:** the members' mirror and flip checks were labelled for camera transforms they did not test (RV-29). A brief should ask for each guard point to be worked from the framing's bounds.

**Test report** (`cairn test report --since 2d`):
- `server-regression` is again most of the slot time, about 80%. It caught 5 failures, 4 of them unique to it.
- There were no flakes, and nothing to file from it.

**Next time.**

- Keep asking the open rules before dispatch. It held T-38 to T-41 to one implementing agent each.
- Don't add a gloss of your own to a person's ruling in a brief. T-37's one stop came from such a gloss.
- Pick look colours together across members. Two magentas met in T-40.
- Clean up the worktree folders `finish` leaves behind.

---
description: >-
  Smooth normals with marked hard edges and a subdivide edit, so a character reads soft and painterly in render and game
---
# M-18 — Soft characters

**Status:** Active

**One-liner:** *Smooth normals with marked hard edges and a subdivide edit, so a character reads soft and painterly in render and game*

## What this milestone delivers

A character stops looking faceted. A mesh can be smooth, with hard edges marked, and `subdivide` turns extruded blocks into soft shapes. Render and export draw the same normals.

Content: the gardener soft, with a creased hat brim, flat beside smooth.

Plans against [D-6](../design/D-6-characters-rigging-animation-and-character-export.md), from Orcward's request of 2026-11-06. This takes hard edges and subdivide out of [M-14](M-14-bevel-and-finish.md).

**Depends on:** M-16. It runs before M-17, because Orcward's first character goes to a look review.

**Order.** T-42, T-43, T-44 in sequence. Stronger model on T-43, Sonnet on the rest.

## Required tasks

- [ ] [T-42](../tasks/T-42-smooth-normals-and-hard-edges.md) — An agent marks a mesh smooth and marks hard edges, and render and export draw the same per-corner normals
- [ ] [T-43](../tasks/T-43-subdivide.md) — subdivide smooths a mesh with Catmull-Clark, keeping ids, colours, groups, hard edges and weights
- [ ] [T-44](../tasks/T-44-soft-character-scenario.md) — The test character is subdivided and smoothed through the tools and shown soft and with hard creases, in its own suite

## Retrospective

**As planned.**

- **The slice works end to end.** An agent marks a mesh smooth (`set_shading`) and marks hard edges (`mark_edges`) (T-42). It subdivides with Catmull-Clark in one undo step, keeping D-1's id rule, colours, groups, hard edges and weights (T-43). It sees the gardener soft, with a creased brim, beside the flat one, at rest and mid-Walk, and exports it skinned (T-44).
- **Render and export agree.** Both take their corner normals from `Shading.cs`. A `unit` test compares the render's normals with the loaded `.glb`'s, corner by corner.
- **The order held:** T-42, then T-43, then T-44, with no rebase conflicts.
- **Every existing frame stayed byte-identical** in each member, compared by hash before and after. The last counts were cube 32, crate 44, shed 10 and character 17.
- **The person approved every new frame set put to review:**
  - T-42's cylinder, in round 2;
  - T-43's before and after cylinder;
  - T-44's seven gardener frames.
- **`cairn milestone M-18 verify` (2026-11-10)**, run when this retrospective was first written (954faca), passed every check but `review`, which comes after the person's go. All 3 members were done and merged, and all ten suites passed (1 ran, 9 carried). That was before the refactor pass and the RV-27 fixes. The run that counts is the one `finish` makes.
- **Models:**

  | Member | Model | Agents |
  |---|---|---|
  | T-42 | Sonnet 5.5 | 4 |
  | T-43 | Opus 5.5 | 2 |
  | T-44 | Sonnet 5.5 | 1 |

  No rework was traced to the cheaper tier. T-42's extra agents were stops for questions and a review, not mistakes.

**Not as planned.**

- **T-42 took four agents for a small scope.**
  - The first stopped after 7 calls on two questions the spec left open.
  - The second hit the 60-call checkpoint.
  - The third stopped when the person found the cube frames hard to judge.
  - The fourth added a hard-rimmed cylinder and finished.

  Every stop was for something only the person could decide. Asking before dispatch would have saved at least one agent. The orchestrator did that for T-44 (the sample and the levels), and T-44 ran in one agent.
- **Subagents have no question tool.** `AskUserQuestion` is not available to them, so each open rule costs a stop, a hand-off and a continuation. Only `mcp__waystone__review` reaches the person from inside a member.
- **The `G:` drive does not exist on this machine.** Review frames went to `C:\Users\andre\artifacts\carve\M-18\T-<n>\round-N` instead. T-42's first round was reviewed from the Temp folder.
- **`finish` could not remove its worktree in any member.** It must run from inside the worktree, and the folder was then the agent's working directory.
  - `branches/task/T-42`, `T-43` and `T-44` are unregistered from git and left on disk for the person to delete. T-43's agent was refused permission to delete its own.
  - Scratch also remains in `C:\tmp\t43break`, `C:\tmp\t43cube` and `%TEMP%\t42*`.
- **After `finish`, a member's own `verify` fails on `worktree`**, because the branch is gone. The `VERIFY PASS` that counts is the one `finish` prints.
- **The not-planar warning flood.**
  - A subdivided face is almost never planar, so the soft gardener's export carries 504 warnings.
  - As ruled, the fold went into `subdivide`'s result only. T-44 showed the warnings getting in the way elsewhere.
  - `scenario-soft` therefore asserts that every export warning is a not-planar face, rather than that there are none.
  - **T-72** is filed to fold them in `check`, `get_mesh` and `export`.
  - In review (RV-27), the person ruled that until T-72 lands, the check also pins the count of those warnings to `select faces where non_planar`.
- **T-44 wrote a `tests.json` edit into the milestone worktree by mistake** and reverted it. The orchestrator checked that the milestone worktree was clean afterwards.
- **Rulings the person gave during the milestone** are all recorded in the members' As built:
  - **T-42:**
    - Corner normals are angle-weighted.
    - An open edge is a boundary. An edge of three or more faces is hard. Two faces wound the same way make a hard edge.
    - `save` writes version 3 only when a mesh is shaded, so every flat file stays version 2, byte for byte.
    - The `normals` look shows corner normals.
    - A cylinder with hard cap rims, flat beside smooth, for review.
  - **T-43:** not-planar warnings are folded in `subdivide`'s result only.
  - **T-44, asked before dispatch:**
    - `samples/gardener.carve` stays flat, and `samples/gardener-soft.carve` is added.
    - The gardener is subdivided at level 1: 2,576 triangles against the 5,000 budget (the flat one is 680).
- **Readings members landed that the spec did not settle:**
  - **T-43**, the textbook Catmull-Clark choices (Hoppe/DeRose):
    - A sharp edge is any edge `Shading` does not smooth across.
    - A vertex takes the smooth rule on 0 or 1 sharp edges, the crease rule on 2, and is a fixed corner on 3 or more.
    - Hard marks crease even on a flat mesh.
    - An unweighted source adds nothing to a weight blend.
    - `subdivide` returns the created ids as ranges.
  - **T-44:** the brim's 16 rim edges are the creased ones. `GardenerSpec.cs` is linked from the Character scenario, not copied.

**Never verified.**

- **A real game.** The soft gardener is proved through the engine's loader in `tests/Carve.SoftLoad.Tests`. It has not been loaded and drawn in Orcward's game.
- **Weak checks:**
  - T-42's shaded-cylinder smoothness thresholds (2 and 3) were set from measured values. The normals frames carry the strong checks.
  - T-43's `cylinder-subdivided-smooth` has no check of its own that subdivision happened; its sibling frames prove it. Its flip check fails by only one unit of blue.
  - T-44's mirror and flip mutations fail at the first frame's first check, so the later frames' own mirror checks are not each shown failing.
  - T-44 did not open its reopened and recreased frames. Their byte-equality is asserted in the scenario.
- **Hash comparisons that were partial or local:**
  - T-42 guarded crate, shed, character and export on local runs, and not again after its last commit, which changed only the cube scenario.
  - T-43's crate guard was two local runs, since the queue held no crate baseline job.
  - T-44 did not hash the export suite's files.
- **The orchestrator opened no frame.** Every look above is a member's or the person's.

**Test report** (`cairn test report --since 2d`): `server-regression` is about 80% of the slot time and caught 3 failures, and `scenario-shed` caught 1. There were no flakes, and nothing to file from it.

**Next time.**

- Read every member's spec against its design doc before the first dispatch, and put the open rules to the person then, as was done for T-44. It would have saved T-42 two agents.
- For a shading or shape look, choose a shape that shows it plainly at planning (a cylinder, not a cube), not after a review.
- Clean up the worktrees `finish` leaves behind, either by the orchestrator at the end or by cairn on its next command.

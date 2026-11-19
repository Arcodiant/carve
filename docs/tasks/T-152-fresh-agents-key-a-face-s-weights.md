---
description: >-
  A fresh agent on plain carve authors and exports a clip that keys weights from the tool descriptions alone
---
# T-152 — Fresh agents key a face's weights

**Status:** Open

**Pri:** P2

**Milestone:** M-32

**Batch:** — (standalone)

**Depends on:** T-151

**Verify:** server-unit

**Perf:** skip: Carve declares no heavy suites

## Goal

The descriptions alone are enough. A fresh agent with only carve's tools gives a face a clip that keys weights, checks it by eye, and exports a `.glb` whose weights channel plays it, as M-21's T-107 and M-9's T-98 showed for their features.

## Scope

- **One round of two `claude -p` sessions** on plain `carve`, with `--allowedTools "mcp__carve__*" --tools ""` and the model named in the stop list (Opus 5.5; the default on this machine is Opus too, so say so).
  - **Session 1** gets a copy of `samples/face.carve` with no clips and a plant (below). It's asked for a looping `Wink` clip (one eye) and a skeleton-less use: a second, unrigged copy of the head gets a weights-only clip, then both are exported.
  - **Session 2** opens session 1's file, adds a `Blink` beside `Wink`, renders both clips at two times, and exports.
- **The plant** must be one the descriptions and tool results can't route around (M-21's lesson). Propose it to the person before the run. The planning example ("a clip keys a missing target") can't occur under the lost-target refusal.
  - A candidate: the copy's `Blink_L` target is authored on the wrong lid, so `Wink` with `Blink_L` closes image-left. Only a render shows it.
- **Checks on the round folder** (`tests/Carve.FaceAcceptance.Tests`, reads `CARVE_FACE_ACCEPTANCE_RUN`, skipped when unset; no suite of its own, but a `dotnet-closure` input of `server-unit` so its build compiles, as T-49's is):
  - the `.carve` has the clips with weight tracks;
  - each `.glb`'s weights channels read with SharpGLTF match the saved clips;
  - `render_glb` of the agent's clip equals `render` under D-6's rule.
- **Report** the agents' transcripts' stumbles: any description that misled, and any refusal that didn't say what to do. Each fix to a description is a finding, put to the person before landing.

## Notes

- **Running and reading, not building:** Sonnet, with Opus 5.5 inner sessions.
- **Stop and ask:** the plant before the run; what counts as recovery if the agent sidesteps it; and any description change.
- **Carried:**
  - evidence (transcripts, round folder) at the absolute path the dispatch names;
  - `finish` run inside the worktree, with its `Merge task/T-152` line given.

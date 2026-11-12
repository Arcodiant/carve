---
description: >-
  render_glb draws a .glb through Empyrean's own loader and GPU skinning at a clip and time, framed as Carve's preview is
---
# T-47 — Render a loaded glb

**Status:** In Progress

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-41

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent, and T-48, sees a `.glb` as the game gets it, through Empyrean's own loader and skinning.

## Scope

- **Tool:** `render_glb(path, clip?, time?, view...)`, drawing in the same scene, light and camera as `render`, framed from the file's rest bounds as T-32 settled.
- **Load:** through the engine's model loader, with the file's own materials. Reference `Arcodiant.Empyrean.Animation` in `Carve.Server` through `$(EmpyreanVersion)`, and pose from the file's own clip into `SkinnedPose`.
- **First hour:** prove a `SkinnedPose` draws on Carve's headless host, and record how. If it does not, record an engine need and stop.
- **Errors:** a missing file, a file with no skin given a `clip`, an unknown clip listing the names. Loader `Warnings` are repeated in the result.
- **Tests:** `regression`, a frame of the gardener at a clip and time, with pixel checks.

## Notes

- It shares no skinning code with Carve's preview, on purpose (D-6, *Two samplers*). Stronger model: it meets the engine where the docs stop.
- T-18's viewer remains the game stand-in for static models. This tool is the product's.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

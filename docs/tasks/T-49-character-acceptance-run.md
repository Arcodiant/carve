---
description: >-
  Fresh agents build, rig, animate and export an original character from the tool descriptions alone, across two sessions
---
# T-49 — Character acceptance run

**Status:** In Progress

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-41, T-44, T-48

**Verify:** full

**Perf:** skip: Carve declares no heavy suites

## Goal

Agents who have never seen Carve author an animated character from the tool descriptions alone, and it loads in the engine.

## Scope

- **The run**, as T-20's: scratch folder, plain `carve`, fresh `claude -p` sessions with `--strict-mcp-config`, no Carve docs.
- **Session 1:** build an original character of two meshes, coloured, soft, rigged, with two looping clips, and save it. Check it with renders.
- **Session 2, a later process:** open it, change a clip, and export the model, the clips and the hat as a piece into a game's content folder. A planted mistake: the prompt names a bone that does not exist.
- **Pass means:** recovery from the error text alone; all three files load through `Load` or `LoadAnimationSet`; `render_glb` shows the same character; no unweighted vertices at the end.
- **Rounds:** sharpen descriptions wherever an agent stumbles, at most three rounds. If it still fails, stop and report.
- Keep transcripts, files and frames under `artifacts/m19-acceptance/`.

## Notes

- This is the first fresh-agent test of M-16 to M-18's tools, so expect more description changes than T-20 needed.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

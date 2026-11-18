---
description: >-
  Fresh agents give a mesh dials from the tool descriptions alone, across two sessions, and export it for a game
---
# T-107 — Morph targets acceptance run

**Status:** Done

**Pri:** P2

**Milestone:** M-21

**Batch:** — (standalone)

**Depends on:** T-106

**Verify:** server-unit

## Goal

Agents who have never seen Carve's docs give a model dials and ship it, from the tool descriptions alone. This
proves the descriptions teach the job.

## Scope

- Two rounds at most, each two `claude -p` sessions on plain `carve`, `--allowedTools "mcp__carve__*"` and
  `--tools ""` from round 1 (M-3's lessons). Name these run conditions in the stop list below.
  - **Session 1:** build an original limb or a simple creature part as a quad cage, then make a second cage file
    of the same topology with other sizes. Give the first mesh two dials, one from positions and one from the
    file. Save.
  - **Session 2:** open, subdivide, check, see it at weights, export.
- **Plant:** the brief's second file has one face wound the other way, a topology mismatch the error names. The
  agent can fix it without guessing the person's intent (M-4 and M-5 lessons). Recovery is judged by the error
  being understood and the file fixed.
- Evidence: transcripts, the `.carve` and `.glb`, and frames, in `~\artifacts\carve\M-21\T-107\`.
  The frames go to the person.
- A description changed after a round is listed with its reason.

## Notes

- **Goes back to the person, stop and ask:** any change of run condition (flags, tools, prompt) from what is
  written here; accepting a round in which the plant did not fire.
- Sonnet.

### As built

Built by Claude Sonnet 5.5 (the first agent, which ran round 1, and this continuation). The inner `claude -p` sessions ran on Opus 5.5 (the init event shows it; `--model claude-opus-5-5` was given explicitly, as T-49 did).

- **Round 1 (the only round).** Scratch folder `%TEMP%\carve-m21-acceptance\round-1\`, a Release publish of this worktree, `.mcp.json` of plain `carve`, `claude -p --model claude-opus-5-5 --mcp-config .mcp.json --strict-mcp-config --allowedTools "mcp__carve__*" --tools ""`, the prompt on stdin (M-3/M-4 conditions). Evidence in `G:\My Drive\artifacts\carve\M-21\T-107\round-1\`: transcripts, summaries, prompts, `.mcp.json`, `limb.carve`, `limb-b.carve`, `limb.glb`, 8 frames (the person approved them). The brief fixed the layout (a tube of 5 rings x 6, index 6r+k) so a second file could match, and said "our exporter lists face 7 as [13,14,8,7]" (the plant: really [7,8,14,13]).
- **Session 1** (13 calls): `define_mesh` of the planted second file warned "two faces cross this edge in the same direction"; `define_target` from the file failed with `faces[7][0]: vertex 7 here, 13 in the other mesh`. The agent understood it (face 7 wound the other way), did not guess the intent, and defined Slim from that file's vertex positions with `positions`. Saved `limb.carve` with Girth (18 vertices) and Slim (30).
- **Session 2** (13 calls): open, subdivide, set_shading, check at weights, 3 renders, export, 2 `render_glb`. `limb.glb` has `targetNames ["Girth","Slim"]`, and the `render_glb` frames are byte-identical to the `render` frames.
- **The plant fired**, and recovery was judged by the person's ruling (2026-11-18): round 1 is accepted as recovery, run no second round. The error was understood and the work went on correctly without guessing, as M-4's ruling allowed, though the second file was not fixed. **The Scope's "the file fixed" was amended for this run, by the person's ruling.**
- **No description changed after the round** for what the agents did. One was changed after it, by ruling, for the export result below.
- **`export` names the targets it wrote (the person's ruling, 2026-11-18; a code change in T-104's area, made here).** In `src/Carve.Server/ExportTool.cs`, `TargetsPart(model)` adds `, N morph target(s) 'A', 'B' in glTF target order` to the result line of the model, skinned and piece modes, from `Model.TargetNames` (what `GlbTargets` writes; a piece's model holds only its own meshes, so its own targets). A model with no targets adds the empty string: its result text is unchanged byte for byte. Clips mode has no mesh and no targets. The description gained one sentence saying so, with the example `2 morph target(s) 'Girth', 'Slim' in glTF target order`; the existing worked examples show no targets, so none was edited.
- **Test** (`tests/Carve.Server.Tests/ExportTargetsResultTests.cs`, unit): a cube exported with no targets has no "morph" in its result; after two `define_target` calls the result holds the example text above, and the exported file's `MorphTargets.TargetNames` through the loader is `[Girth, Slim]`. Shown failing: with the part always added, the no-target export fails ("0 morph target(s)  in glTF"); with the names reversed, the targets export fails on the result text. Both restored, passing.
- **Not checked.** No second round, no new frames (none needed for this change), no `server-regression` run (the verify did not require it). The `**Verify:**` line was added afterwards (RV-64, Opus 5.5) to record the scope the verify ran:
  `server-unit`, which holds `ExportTargetsResultTests`. `server-regression` first ran over the export change in the
  milestone's verify after the rebase onto T-71 (see M-21's retrospective). RV-64 also added the skinned and piece
  result lines' test.

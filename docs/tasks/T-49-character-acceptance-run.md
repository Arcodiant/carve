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

### As built

Run on **Claude Sonnet 5.5** (`claude-sonnet-5-5`). Every `claude -p` session ran on **Claude Opus 5.5** (`claude-opus-5-5`, from each transcript's init event: all four), with `--strict-mcp-config`, an `.mcp.json` of plain `carve` only (`dotnet run --project` on this worktree, `--no-build`), `--allowedTools "mcp__carve__*"` and the built-in tools removed (`--tools ""`), from `%TEMP%\carve-m19-acceptance\round-N\` (outside every Carve `CLAUDE.md`, an empty `game/Content/models/`). Prompts word for word as ruled on 2026-11-13. Transcripts, per-session summaries and traces, the extracted images, `character.carve`, the three `.glb` files, `acceptance/` (`differences.tsv`, `compare/` triptychs, `negatives.tsv`) and the scripts (`run-round.ps1`, `summarize.py`, `trace.py`) are in `artifacts/m19-acceptance/round-N/` and `C:\Users\andre\artifacts\carve\M-19\T-49\round-N`.

**What was added.** `scenarios/Carve.Scenario.Acceptance` (draws the saved model with `render` and the exported `character.glb` with `render_glb` at rest and at three times of every clip it has, from the front and the side, and measures each pair under D-6's frame rule with the cluster rule, `ComparisonPng.cs` linked from Orcward; a positive check that the character covers 2 % of each frame; and a control, the front render against the file seen from the back, that must fail), `tests/Carve.AcceptanceLoad.Tests` (the saved model has two meshes, Idle and Walk looping and no unweighted vertex; `Load` of `character.glb` with no warnings, the saved bones and both clips; `LoadAnimationSet` of the clips file with the same bones and durations; the hat piece with the body's bones and no clips; the table of frames), `scripts/run-acceptance-check.ps1` (scenario, then checks, on a round folder) and `scripts/run-acceptance-negatives.ps1`. It is not a suite in `tests.json`: its input is a folder only an acceptance run makes. Both projects are in `Carve.slnx`, so the checks build and skip when `CARVE_ACCEPTANCE_RUN` is unset.

**How the frames were compared.** `render_glb` against `render` of the opened `character.carve`, same view, size, clip and time, by the Orcward rule: a pixel differs past 8 of 255, a frame fails past 0.5 % of its pixels or with a connected (8-neighbour) patch past 50 px.

| | Round 1 | Round 2 |
|---|---|---|
| Character | green creature, body and hat, 7 bones (`hips`, `spine`, `head`, `arm_L`, `arm_R`, `leg_L`, `leg_R`), 1396 vertices | body and hat, 11 bones (`Hips`, `Spine`, `Head`, `Leg_L`, `Foot_L`, `Leg_R`, `Foot_R`, `Arm_L`, `Hand_L`, `Arm_R`, `Hand_R`), 1720 vertices |
| Session 1 calls | 59 (58 carve incl. 10 render, 1 `weigh`, 3 `get_weights`; 1 `Bash`, refused) | 52 (7 render, 11 `set_weights`) |
| Session 2 calls | 10 (9 carve, 1 `Bash` refused) | 12 (11 carve, 1 `Bash` refused) |
| `<PLANT>` and why | `arm_left`: the bones are `arm_L`/`arm_R`; `arm_left` spells the side out, so it is not a bone of this skeleton | `Left_Arm`: the bones are `Arm_L`/`Arm_R`; the side word first, so it is not a bone of this skeleton |
| Plant | no error text was met: the agent read `get_clips` (round 2 also `get_skeleton`) first, saw its keys were on `arm_L` and `arm_R`, said there was no `arm_left`/`Left_Arm`, and used `arm_L` and `arm_R` | same |
| Edit | arm keys 20 deg to 40 deg (0.17365 to 0.34202), both arms, saved `(replaced)` | same (20 to 40), saved |
| Three exports | model (cut by the client, see below), clips, hat piece | same |
| Loads (5 checks) | pass | pass |
| Frames, 14 pairs | all pass: 0 differing px in 13, 1 px in Walk 0.667 side; largest patch 1 | all pass: 1 px in Walk 0 side, else 0 |
| Control, front vs back | fails (5,267 px, patch 2,614) | fails (21,431 px, patch 16,861) |
| Unweighted vertices at the end | none | none |

**What stumbled.** One thing, in both rounds: the result of `move`, `check`, `open` and `export` on the subdivided character was 74 to 120 thousand characters (one "face is not planar" warning per curved quad, which `subdivide` says to expect but folds into one line only in its own result), over what the client will show, so the agent got "result exceeds maximum allowed tokens" and no confirmation. The calls had worked. Each time the agent coped: it checked with `get_weights`, `get_clips`, `render_glb` or `select` instead (and tried `Bash`, which was refused; the file tools are removed). Round 1 session 2 never saw the model export's result and confirmed the file with `render_glb`.

**What changed.** Round 1 to 2: description notes on `subdivide`, `open` and `export` about the long lists. They did not change what the agents did (round 2 flooded the same, coped the same), so by the person's ruling of 2026-11-13 they are reverted. **T-72 owns the fix** (fold the not-planar warnings in `check`, `get_mesh`, `export`, `open` and `move`), pulled into M-19. No third round: both rounds pass.

**Shown failing** (`run-acceptance-negatives.ps1` on round 2, with round 1's files as "another character"):

| Broken | Fails |
|---|---|
| a body vertex with no weight | saved-model check |
| hat piece file missing | hat piece load |
| clips file missing | animation-set load |
| model file of another character | scenario (render_glb differs, 17 % of pixels, patch 45,824), animation-set load |
| saved file of another character | scenario, frames check |

**Recorded, not judged.** Session 1 of round 1 saved `character.carve` before the script copied it (`character-after-session1.carve` exists from round 2 on); the round 1 session-1 content is in its transcript. Neither session used `undo`.

**Review.** The person approved round 2's session-1 renders and four `render | render_glb | difference` triptychs (rest front and side, Walk 0.333 side, Idle 0.667 front), through `mcp__waystone__review`.

**Rulings (2026-11-13).** (1) The planted wrong bone never produced an error: the agent read `get_clips` and `get_skeleton` first and corrected the name from their output. Recorded as a pass, it recovered, with that reading noted. (2) The warning flood goes to T-72, and the three description notes are reverted. (3) No `tests.json` suite for the two acceptance projects; their `CLAUDE.md` rows say they run only on an acceptance run's folder, by its script.

**Models.** The acceptance agents of both rounds ran on Sonnet 5.5 (the first agent of this member too). The finishing agent ran on Sonnet 5.5.

**Hash guard.** HASHPLACEHOLDER

**Not checked:** a real game loading the files (the engine's loaders only); round 1's session-1 `.carve` file itself.

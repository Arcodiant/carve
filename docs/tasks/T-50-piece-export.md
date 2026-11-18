---
description: >-
  export writes chosen meshes to their own .glb with every bone in the same order and no clips, so a costume can be swapped at runtime on the body's palette
---
# T-50 — Piece export

**Status:** Done

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-41

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-character

**Perf:** skip: Carve declares no heavy suites

## Goal

Orcward swaps costumes at runtime: orcs wear monster costumes, and crew change on the way between rooms. An agent exports a chosen piece to its own `.glb`, and it follows the body's animation in the game.

## Scope

- **`export` with `mode: piece` and `meshes`:** the named meshes in one file, as D-6's *Pieces share the body's palette* says: every bone in the same order, the same inverse binds, no animations, one glTF mesh. It refuses an unknown mesh (listing the names), no skeleton, or an empty list. Unweighted vertices in the chosen meshes are refused as in T-34.
- **Description:** says a game loads the piece and shares the body's `SkinnedPose.Palette`, with the body as driver and only the body holding an `Animator`, with a short example.
- **Sample:** the gardener's `Hat` as a piece file.
- **Tests, `unit`:** through `Parse`, no `Warnings`; bone names, order, count and inverse binds equal across the model file and each piece, on a piece using few bones and on a breadth-first skeleton; no clips; byte identity. **Show it failing** with a skeleton trimmed to the bones used.

## Notes

- A fixer may not trim the skeleton: the palette is positional, and nothing throws if it misaligns.
- The engine's guide says pieces should bind the same `Skeleton` instance. Separate files load separate instances, so the game relies on the order guaranteed here. T-48 checks the file side; proving the shared palette in a running scene is T-47's tool if it is cheap, otherwise Orcward's.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Model: Sonnet 5.5.

- `export` takes `mode: piece` and `meshes` (new argument; the arguments are now `path, allow_unweighted, limits, mode, meshes, overwrite, write_limits`, `limits` and `write_limits` being T-51's). `PieceExport` (`src/Carve.Server/PieceExport.cs`) selects the named meshes (in the model's order, duplicates ignored), keeps the whole skeleton and no clips, and writes through `SkinnedExport.Write`, so armature, skin and inverse binds are the model file's. Refused, writing nothing: empty or missing `meshes`, an unknown name (lists the model's meshes), no skeleton, `meshes` outside piece mode, non-string entries; unweighted vertices of the chosen meshes are refused as in T-34 (`allow_unweighted` applies). The description carries the Palette/Animator text and an example.
- Readings the spec left open: chosen meshes keep the model's order; a piece gets no height warning, since a hat is not a humanoid's height; triangle and submesh warnings stay. *Brought up to date in M-19's refactor pass:* T-50 built this as `SkinnedExport.Warnings(..., wholeBody: false)`; since T-51 (per-game export limits, landed on `main` and met in M-19's first rebase) every mode reads the game's limits file, and the rule lives in `GameLimits.Warnings(..., wholeBody: !asPiece)` in `src/Carve.Server/GameLimits.cs`. The result line reads `exported <file> as a piece: ...`.
- Tests: `PieceExportTests` (unit: through `Parse`, no warnings, bones/names/order/count/inverse binds equal to the model file for pieces on the root bone, the deepest bone and several bones of a 9-bone breadth-first skeleton, no animations, one glTF mesh, byte identity, refusals, height) and `PieceExportToolTests` (regression, real `carve`). `PieceExport.cs` is linked into `Carve.Server.Tests.csproj`. `ExportToolTests` and `SkinnedExportToolTests` were updated for the new argument list (the old "mode piece is refused" assertion now uses `mode: bogus`).

| Check | Break | Result |
|---|---|---|
| rig equality (names, order, count, inverse binds) | piece written with the skeleton trimmed to the hat bone | `Shown_failing_...` asserts the check throws; the trimmed file still loads with no error |
| no animations | `PieceExport.Select` kept the model's clips | 5 tests failed (4 theory cases and the tool test); reverted |

Existing frames and exported files: no scenario or frame code changed; model-mode bytes are asserted equal before and after a piece export, and `scenario-cube`/`scenario-crate` pass. Hash comparison of existing frames was not run separately.

**Hat sample (second agent, Sonnet 5.5; rebased onto `origin/milestone/M-19` first, T-45's clip warnings sat beside the mode handling without conflict).** The person's answer of 2026-11-12 to the first agent's question was "Scenario content only": the Character scenario exports `models/gardener.hat.glb` into its content folder (`export` with `mode: piece`, `meshes: [Hat]`; the result must say "as a piece", 22 bones, no warnings; a second export is refused as existing and an overwrite is byte-identical), and `CharacterLoadTests.The_hat_piece_loads_with_the_bodys_bones_in_order_and_no_clips` loads it through `IModelGeometryLoader.Load`: no warnings, bones equal to the body's, no animations. Nothing is added to `samples/`. T-48 reads the piece the same way, from the scenario's content folder. `scenario-character` is added to **Verify**.

Character suite guard, by SHA-256 of every file in the run folder before (baseline run on the rebased tree) and after: 29 files before; after, the same 29 hash-identical (every frame, `gardener.glb`, `gardener.clips.glb`, the `.carve` files) except `run.json` (the step log, which now records the new step), plus the new `game/Content/models/gardener.hat.glb`. The five existing checks pass and the sixth is new (6 passed).

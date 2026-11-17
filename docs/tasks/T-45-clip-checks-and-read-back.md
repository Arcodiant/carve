---
description: >-
  get_clips reads clips back, and looping clips that do not close or clips that travel are warned about
---
# T-45 — Clip checks and read-back

**Status:** Done

**Pri:** P1

**Milestone:** M-19

**Batch:** — (standalone)

**Depends on:** T-41

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent finds a popping loop or a drifting walk from a result, not from the game.

## Scope

- **Warnings** on `define_clip`, `get_clips`, `save` and `export`, as D-6's *Clips* says: a looping clip whose channel ends differently from how it starts, naming the bone, channel and difference; a clip whose root bones end elsewhere on the ground plane.
- **`get_clips(name?)`:** names, durations, `loop`, channels and keys, as given.
- **Tests:** `unit` for each warning, for no warning on a clean clip, and for a rotation equal up to sign counting as closed; the stated tolerance is a constant with a comment.

## Notes

- The closing tolerance is a new rule: propose it and ask.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Run on Sonnet 5.5 (claude-sonnet-5-5), one agent.

- **Closing tolerance** (the person's ruling, 2026-11-11, "1° / 1 mm"): rotation closes when first and last key differ by at most 1° (q and -q equal); translation closes when within 1 mm on X, Y and Z, and a root counts as in place when within 1 mm on X/Z. Constants `RotationToleranceDegrees` (1) and `TranslationToleranceMetres` (0.001) in `src/Carve.Core/ClipChecks.cs`, commented; recorded in D-6's *Clips*. *Brought up to date in M-19's refactor pass:* T-45 read the ruling literally and compared loop closure on X/Z only, so a looping bob in Y that did not close was not warned; T-83 (the person's ruling of 2026-11-12) made loop closure compare all three axes, and the warning now says "on X/Y/Z".
- **Warnings** (`ClipChecks.Check`, compared on each channel's first and last key): a looping clip's rotation or translation that does not close, naming clip, bone, channel and difference; a root bone whose translation ends more than 1 mm away on X/Z (the ground plane), looping or not. A looping root that drifts gets both. Carried in `define_clip`'s result (`warnings`), `get_clips`, `save`'s line and `export`'s line (skinned and clips modes; piece mode is T-50's and has no clips).
- **`get_clips(name?)`**: `{clips:[{name, duration, loop, channels:{bone:{rotation, translation}}, warnings}]}`, keys as given; an unknown name is an error listing the clips.
- **Tests**: `ClipChecksTests` (unit: each warning, clean, sign-flip closed, 1° boundary, non-loop, child bone not travel) and `Clip_warnings_reach_define_clip_get_clips_save_and_export` (regression).
- **Guard checks**: no render, mesh or frame code changed, so existing frames are unchanged by construction; the scenario suites in verify (`scenario-cube`, `scenario-crate`) are the hash guard. No new frame, so no pixel check. Each new check shown failing by mutation of `ClipChecks`:

| Mutation | Tests failing |
|---|---|
| both tolerances set to 100 | 5 (rotation, 1° boundary, root travel x2, loop+travel) |
| loop condition dropped from the rotation check | `A_non_looping_rotation_may_end_elsewhere` |

- **Not checked**: no frame or sound review (none made). Engine needs: none.

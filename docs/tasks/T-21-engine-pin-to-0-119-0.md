---
description: >-
  The engine pin moves to 0.119.0, the version Orcward's game uses, with every existing frame byte-identical
---
# T-21 — Engine pin to 0.119.0

**Status:** Open

**Pri:** P1

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-regression, scenario-cube, scenario-crate

**Perf:** skip: Carve declares no heavy suites

## Goal

`main` restores and builds as pinned, with no `-p:EmpyreanVersion` override, on engine 0.119.0, and every frame the scenarios draw is the same as before the move.

## Scope

**The fact found at planning (2026-11-05):** `Directory.Build.props` pins `EmpyreanVersion` 0.117.0, but the feed (`http://localhost:5555/v3/index.json`) now lists only 0.119.0, 0.115.x and 0.114.x. `dotnet restore src/Carve.Server` on `main` fails with `NU1603` as an error (warnings as errors): 0.117.0 was not found and 0.119.0 was resolved instead. The last restore on this machine (`obj/project.assets.json`) resolved 0.119.0, so builds have been running on an override.

**Change:**

- `Directory.Build.props`: `EmpyreanVersion` to `0.119.0`, and its comment to say it is the version Orcward's game uses, not the newest on the feed (0.120.1 reached the feed by 2026-11-06). No other pin moves.
- Read the engine changelog from 0.117.0 to 0.119.0 (`<cache>/arcodiant.empyrean.graphics/0.119.0/docs/CHANGELOG.md`, and the core package's) and list in the closing report every entry that could touch Carve. Known from planning: `RenderStageScope` (Carve composes engine stages only, so nothing to declare); "on an sRGB output the built-in tonemap no longer encodes" (Carve has no tonemap and encodes in `ToDecodedImage`, so nothing to change); `TextureFormat.Bgra8Srgb` added (a `switch` over `TextureFormat` with no default gains a case: check Carve has none). Anything the build or the tests show is reported, not quietly fixed, unless it is the one-line kind.
- `dotnet restore` and `dotnet build Carve.slnx` with no override pass, warnings as errors.

**Frames.** Before changing the pin, run `scenario-cube` and `scenario-crate` on the current `main` with the override (`-p:EmpyreanVersion=0.119.0`, the state builds have been in) and keep their frames and `run.json` under `artifacts/t21/before/`. After the change run both again: every frame must be byte-identical (md5 table in the report). Then compare against the newest frames cairn holds from before 0.119.0 reached this machine (`cairn test artifacts scenario-cube` and `scenario-crate`): if any exist and differ, that is the engine's 0.117 to 0.119 change showing; **stop and show the person** the pairs (`mcp__waystone__review`, kind `approve`). If none exist, say so plainly: the move from 0.117.0 cannot then be checked by frame on this machine, and the person looks at the after frames instead.

**Tests.** No new test: the pin is enforced by restore itself (`NU1603` is an error). `server-regression`, `scenario-cube` and `scenario-crate` pass through `cairn task T-21 verify`.

## Notes

- **Decided by the person, 2026-11-06:** the bump lands alone on `main` first, ahead of M-3, because M-3 does not restore as pinned and Orcward's work needs it. This replaces the M-4 planning decision. T-16 and M-4's tasks depend on it.
- **Stay on 0.119.0.** Do not move to 0.120.1: Orcward confirmed it tests against 0.119.0 and needs nothing from 0.120.
- **Open rules: stop and ask.** Any pin but `EmpyreanVersion`. Any frame that differs from the before frames. Any code change beyond what the compiler or a test demands from the version move, and any such change that is more than a line or two.
- **A fixer may not change** the light, the framing, the background, the image size or `DrawnSpace` to make a frame match.
- Not in scope: the published `carve.exe` install under `C:\tools\carve` (the person re-publishes it); the M-3 branch, which rebases onto this when it lands.
- Sonnet is enough: the task decides nothing.
- Run `finish` from inside the worktree and leave the folder.

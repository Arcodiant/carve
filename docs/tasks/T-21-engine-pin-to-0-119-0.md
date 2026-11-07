---
description: >-
  The engine pin moves to 0.119.0, the version Orcward's game uses, with every existing frame byte-identical
---
# T-21 — Engine pin to 0.119.0

**Status:** In Progress

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

### As built

**What changed:** `Directory.Build.props` only: `EmpyreanVersion` 0.117.0 to 0.119.0, and its comment now says it is the version Orcward's game uses, not the newest on the feed. No other pin moved and no code changed. `dotnet restore` and `dotnet build Carve.slnx` pass with no override, 0 warnings. Nothing new to call: T-16 and M-4's tasks just restore as pinned.

**Frames: no frame differs.** All evidence is in `artifacts/t21/` in this worktree.

| Comparison | Engine | Machine | Result |
|---|---|---|---|
| `before/` to `after/`, cube and crate | 0.119.0 by override, then 0.119.0 as pinned | Mantorok, RTX 5070 Laptop | 22 of 22 byte-identical |
| `cairn-held/` to `after/`, cube and crate | 0.117.0, then 0.119.0 | Xellotath, RTX 5070 Ti, then Mantorok | 22 of 22 byte-identical |
| `cairn-held/` to `verify-cube/`, `verify-crate/` | 0.117.0, then 0.119.0 | Xellotath, both | 22 of 22 byte-identical |
| `old-export/` to `verify-export/`: 5 scenario frames, 2 viewer frames, `crate.glb` | 0.117.0, then 0.119.0 | Xellotath, both | 8 of 8 byte-identical |

The held frames are not literally from before 0.119.0 reached this machine: they are the newest jobs cairn returns, `J-20261107-010158-36fdf8` (cube) and `J-20261107-005943-8bbd21` (crate), at `3137c72`. They do show 0.117.0: that commit pins it, the job's restore passed with 0 warnings (a floated version is `NU1603`, an error), and an ad-hoc job (`J-20261107-161620-c47ad1`) listed 0.117.0 in Xellotath's package cache (`arcodiant.empyrean.graphics` only; the clean restore covers the other packages). That is inference from those three facts: the job kept no `deps.json`. `run.json` is byte-identical for cube and crate across `before/`, `after/` and `cairn-held/`. No pair differed, so nothing went to the person. The export comparison is beyond the Scope, which names only cube and crate; the Goal says every frame, so it was run (`J-20261107-161733-59da19`, the export script at `3137c72`, against verify's `J-20261107-161032-8b869b`).

| Frame | md5 (before = after = held) |
|---|---|
| cube/after-error-front, cube/cube-flipped-front | `c5a8ee0236604c9479f6b6f86b255dba` |
| cube/cube-back | `38ea53b55779de73adaa97ad9d1428e7` |
| cube/cube-dir-0--1-0, cube/cube-dir-1-0-0 | `f7e6d57111cea6e921abec2a55b4439b` |
| cube/cube-dir-0.4-2-0.8 | `c9087eee5f9712957e0e98639c2d336a` |
| cube/cube-front, cube/cube-front-again | `20ada7cd09fce42964ea446da7edf23f` |
| cube/cube-zoom3-corner | `daa3867f11fd09ae3beb58929bae4c76` |
| cube/lblock-back | `ec371e100b97ccf1ce19dfeda6e3f959` |
| cube/lblock-dir-0--1-0-corner | `1401b6ffbb9830320fd2e830be0c1758` |
| cube/lblock-front | `0ff55d97fc2e45bb8b7a59c887f9a46b` |
| cube/longbox-front | `f8999224bcede2e93c9e71cb00c1402b` |
| crate/crate-back | `262138e0bc9023f45be7801f229cdef4` |
| crate/crate-dir-0.4-2-0.8 | `a926d569da5ae3fd2e0078dea1b337e1` |
| crate/crate-dir-2-1-1-zoom3 | `9cd00c55a6ff639b48391ce34b647f24` |
| crate/crate-dir-m2.2-1.2-1.6 | `d7de54b630b8569118b642356ea56d39` |
| crate/crate-front, -again, -redone, -after-errors | `a3a0ce4968b751ac43ce9f9780edb576` |
| crate/crate-front-undone | `df089e2b3bac74d2543dbe386754cd1b` |

**Engine changelog, 0.117.0 to 0.119.0, entries that could touch Carve** (graphics and core, plus hosting, hosting.glfw and input). None needed a change.

- `RenderStageScope`, `IRenderStage.Scope`, the `PerFrame` throw (0.117.1, logged under 0.119.0): Carve declares no stage of its own; `RenderComposition` composes engine stages.
- `RenderView`, `RenderPipeline.Execute`'s `view` argument, `LastFrame*` summed over views: Carve does not call `Execute`; one view, as before.
- On an sRGB output the tonemap no longer encodes: `RenderComposition` has no tonemap and renders to `Rgba16Float`, encoded in `ToDecodedImage`. The viewer does use `RenderPipelinePresets.StandardHdr`; its frames did not change.
- `TextureFormat.Bgra8Srgb`: Carve has no `switch` over `TextureFormat`; `RenderComposition` uses two members, `Rgba16Float` and `Depth32Float`.
- `ISwapchainManager.TryAcquireNextImage`, `IFrameStateManager.BeginViewFrame`, `IViewTargetProvider`: Carve implements none.
- `Arcodiant.Vulkan` 0.1.2-alpha.0.40 to .41, required by 0.119.0: transitive, Carve pins nothing of it; `project.assets.json` resolves .41 and the frames did not change.
- Virtual-surface images gain `TransferDestination` usage: the viewer sets `VirtualSurface = true`; its frames did not change (`old-export/` to `verify-export/`).
- `IFrameTiming`, `IFrameClock` and the XR packages: no custom clock, no XR.
- A blank `Empyrean:Game:UserPathsRoot` now means unset: Carve does not set it.
- `UseGlfwHost` throws with `UseXrHost`: the viewer does not call `UseXrHost`.
- Engine assemblies now carry `{major}.{minor}.{patch}.0`: Carve's own loader and guard tests pass on it.

**Decisions the Scope left open:** the export comparison above, added because the Goal is wider than the Scope's list. The before and after runs were by hand, as the Scope asks, with `-CarveArguments --Carve:Debug:RunFolder=...`, since an override build is outside any suite.

**Not verified:** no frame was opened: all matched by hash, so this says only that nothing changed, not that the frames look right. `viewer-cube`'s frames were not compared across versions; it passed its own checks on 0.119.0. The published `carve.exe` under `C:\tools\carve` is out of scope and untouched.

---
description: >-
  Review of T-21
---
# RV-18 — Review of T-21

**Status:** Answered

**Reviews:** T-21

**Round:** 1

**Previous:** —

**Diff:** 8151b5834f4706f87ea1f6ba7c9d1d9287f5e6ad against origin/main

**Reviewer:** claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### The changelog list leaves out two entries that do reach Carve (Minor)

`docs/tasks/T-21-engine-pin-to-0-119-0.md:83-93`. Rubric 7, and the Scope's "list in the closing report every entry that could touch Carve". Neither entry needs a change, so "None needed a change" stands, but the list is not complete:

- **`Arcodiant.Vulkan` 0.1.2-alpha.0.41** (graphics changelog, 0.119.0, first entry, in bold as a requirement). This dependency really moved under Carve: 0.117.0 took 0.1.2-alpha.0.40, and `src/Carve.Server/obj/project.assets.json:801` now resolves .41 through the 0.119.0 nuspec. It is transitive and Carve names no `Arcodiant.Vulkan` type, so the "no other pin" stop rule is not touched, but a reader of the list would not know a second package moved. The list names `ISwapchainManager.TryAcquireNextImage`, which is the other half of this change.
- **"Virtual-surface images gain `TransferDestination` usage"** (graphics changelog, 0.119.0, the desktop mirror entry). The viewer runs on exactly this: `spikes/Carve.ModelViewer/Program.cs:37` sets `graphics.VirtualSurface = true`. The list takes the mirror entry's two interfaces and says "Carve implements none", which skips the one line of that entry that applies. The viewer's `front.png` and `minus-x.png` match across versions, so it did no harm.

**Outcome:** fixed

### "It names three members" is wrong: `RenderComposition` names two (Minor)

`docs/tasks/T-21-engine-pin-to-0-119-0.md:88`. Rubric 7, `### As built` matches. `src/Carve.Server/RenderComposition.cs:22,27,29` has three uses of two members, `TextureFormat.Rgba16Float` twice and `TextureFormat.Depth32Float` once, and no other `.cs` file in the repo names `TextureFormat`. The conclusion, no `switch` over `TextureFormat`, is true.

**Outcome:** fixed

## Summary

Reviewed T-21 at `7f3ddc9` against `origin/main` (`00821e8`), core rubric only: `.cairn.yml` names no project rubric and the item has no `**Review:**` line. Two minor findings, both in the As built's changelog list. Neither touches the pin or the frames. Nothing was built or run, and no image was opened.

**Scope and stop rules.** Outside the task file the diff is two lines of `Directory.Build.props`: `EmpyreanVersion` and its comment, which says what the Scope asks. No other pin moved and no code changed. No `.csproj` holds a literal version, and no `NoWarn` or `WarningsNotAsErrors` exists at `HEAD` or at `3137c72`. T-16 depends on T-21. `cairn lint` is clean.

**Hashes, recomputed from `artifacts/t21/`.** All claims hold.

- `before/` to `after/`, `cairn-held/` to `after/`, and `cairn-held/` to `verify-cube/` plus `verify-crate/`: 22 of 22 identical each.
- Every md5 in the As built table matches.
- `cairn-held/cube/` matches `cairn-held-cube-job/` (13 frames), so the job id given for the cube is the right one.
- `old-export/` to `verify-export/`: the 5 scenario frames, 2 viewer frames and `crate.glb` are identical, 8 of 8. `view.json` and `crate.carve` also match.
- `export-result.txt` and `run.json` differ there, but only by the job's temp folder: identical once that is normalised.
- `run.json`, which the As built says was not compared, is identical in every cube and crate set.
- Job ids, commits and runners in the As built match line 1 of each `output.log`. The Xellotath logs show the RTX 5070 Ti.
- `before/` and `after/` have no `output.log`, so their engine (override, then pinned) and GPU rest on the As built's word. File times do fit (before 11:09:08, after 11:09:34, pin commit 11:10:09), the two sets are separate runs, and this machine is Mantorok. Nothing turns on it: they match the verify jobs too.

**`cairn-held` as 0.117.0.** The reasoning is sound and stated as inference.

- `git show 3137c72:Directory.Build.props` pins 0.117.0 with warnings as errors, and no suppression exists at that commit.
- The held logs show a plain `dotnet build Carve.slnx -c Debug --nologo`, with no override, restoring and ending at 0 warnings.
- M-3 records the opposite case on the same pin: `docs/milestones/M-3-first-model-in-a-game.md:166` has jobs failing with `NU1603` on a runner without 0.117.0.
- `xellotath-cache/output.log` lists 0.117.0, created 29/10. That listing covers `arcodiant.empyrean.graphics` only, not core or `hosting.glfw`, which the As built does not say. The clean restore covers them, so the conclusion stands.
- `old-export` at `3137c72` was a warm build in the same work folder, so its 0.117.0 rests on the same pin and the same reasoning.
- The As built says plainly that the held frames are not literally from before 0.119.0 reached Mantorok.
- I checked no newer pre-0.119.0 job, as the queue status only reaches back part of the day. The claim that these are the newest cairn returns is taken from the As built.

**Other claims.** `cairn test wait` reports PASS for `scenario-cube`, `scenario-crate`, `scenario-export`, `server-regression` and `viewer-cube` at `d24c26b`, so the loader, guard test and viewer claims hold. `7f3ddc9` adds only the As built. Restores in the verify logs end at 0 warnings with no override.

**Changelog audit.** Read graphics and core from 0.117.0 upward, plus hosting 0.118.0, hosting.glfw 0.119.0 and input 0.118.0. Each "nothing to change" was checked against `src`, `extensions`, `spikes`, `scenarios`, `tests` and `scripts`, and all are true.

- Carve has no `IRenderStage`, `ComputeStage` or `ClearBufferStage`, and `RenderComposition` uses only `AddClearColor`, `AddClearDepth` and `SceneRenderStage`.
- There is no `.Execute(` or `LastFrame`, no custom swapchain, frame state, view target or clock, and no `UserPathsRoot` or any `Empyrean:Game:` key.
- `ToDecodedImage` is at `RenderTool.cs:191`, and `UseXrHost` appears nowhere. The viewer's host call is `UseStandardHost`, which the same entry covers.
- The extension loader compares no versions, so the `AssemblyVersion` change is harmless to it.
- The entries not listed do not apply: the 0.118.1 `StandardHdrOptions` overloads are on the ray-traced presets, the viewer uses plain `StandardHdr`, and the code mods are opt-in.
- `StbImageSharp` and `StbImageWriteSharp` in the 0.119.0 nuspec still match Carve's pins.

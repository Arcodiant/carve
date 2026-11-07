---
description: >-
  Second review of T-21
---
# RV-19 — Second review of T-21

**Status:** Answered

**Reviews:** T-21

**Round:** 2

**Previous:** RV-18

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

## Summary

No findings. Reviewed T-21 at `53823c7` against `origin/main` (`00821e8`). Both RV-18 answers hold, and so do the two other edits in that commit. The patch-id is RV-18's, so the code diff is unchanged: two lines of `Directory.Build.props`. Nothing was built or run, and no image was opened. The rest of round 1 was not redone.

**`Arcodiant.Vulkan` .40 to .41.** Accurate.

- The graphics changelog logs .40 under 0.117.0 and "Requires 0.1.2-alpha.0.41" under 0.119.0, with no other bump between them.
- The 0.119.0 nuspec depends on .41, and `src/Carve.Server/obj/project.assets.json:801` resolves it. The viewer's assets file resolves .41 too.
- "Transitive, Carve pins nothing of it" is true: no `.csproj` or `.props` file names it, and no `.cs` file uses it. Line 48 of the assets file shows it arriving through `Arcodiant.Empyrean.Graphics`.
- 0.117.0's own nuspec is not in this machine's cache, so the .40 rests on the changelog.

**`TransferDestination`.** Accurate.

- The sentence closes the desktop mirror entry under 0.119.0 in the graphics changelog.
- `spikes/Carve.ModelViewer/Program.cs:37` sets `graphics.VirtualSurface = true`, and only a hand-passed `--Empyrean:Graphics:VirtualSurface=false` turns it off, as `scripts/run-viewer.ps1:8` documents. Nothing in the export job's log passes it.
- `viewer/front.png`, `viewer/minus-x.png` and `viewer/view.json` are byte-identical from `old-export/` to `verify-export/`, so "its frames did not change" holds.
- That does not contradict the Not verified line, which is about the `viewer-cube` suite's frames, not the export job's.

**`TextureFormat`.** Right. `src/Carve.Server/RenderComposition.cs:22,27,29` has `Rgba16Float` twice and `Depth32Float` once, and `git grep` finds `TextureFormat` in no other `.cs` file, so there is no `switch` over it.

**Cache listing.** Right. `artifacts/t21/xellotath-cache/output.log` runs one `Get-ChildItem` on `arcodiant.empyrean.graphics` and nothing else. It lists 0.117.0, created 29/10/2026 21:44:27. "The clean restore covers the other packages" matches RV-18's reading.

**`run.json`.** Right. One md5 per scenario across `before/`, `after/` and `cairn-held/`, confirmed with `cmp`: cube `3f8db59ed446e277978c107d86483f78`, crate `6984e56a1878943eb1d09e0bef85e96a`. The sentence stays under **Not verified** although it is now a verified fact, which is a matter of placement and misleads no one.

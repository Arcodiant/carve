---
description: >-
  Review of T-119, round 2
---
# RV-58 — Review of T-119, round 2

**Status:** Answered

**Reviews:** T-119

**Round:** 2

**Previous:** RV-57

**Diff:** d7ab34dae46045e3b6013e390f0f5d058f30ed69 against 7a531be

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

None. Each of RV-57's eight answers holds in the README and the task file, and nothing new is wrong.

## Summary

The diff against 7a531be is README.md alone, so there is no scope creep, and every Scope bullet has its
section in the README. Because this is docs-only, that meets the tests point. I checked the fix commit
fdb6c74 against the tool sources in src/Carve.Server:

- define_group's `[0,1,4,5]` is the -Z, +Z, -X and +X faces of define_mesh's CubeExample. Materials keeps
  `[1,3]` as the front and top. Both departures are in T-119's Notes, and B-6 states the two
  description errors accurately (FaceStyleTools.cs:40, MaterialTools.cs:201-204).
- The Characters examples run in order against the define_skeleton example's bones `hips`, `spine` and
  `head`. `weigh` and `set_weights` accept `mesh` (UnknownArguments and WithMesh). `set_weights` gets
  `vertices` rather than `group`, so it needs no undefined group, and its ids 0 to 7 fit an 8-vertex box.
  `head` is a real bone. The clip `nod` keys only `head`. It carries `interpolation` in the channel part,
  as ClipTools' schema has it. It closes, so a looping clip raises no warning. Its quaternion
  [0.17364818, 0, 0, 0.98480775] is sin and cos of 10 degrees, a unit quaternion for 20 degrees about X.
  render's `clip` and `time`, the piece export's `path`, `mode` and `meshes:["hat"]`, and render_glb's
  `path`, `clip` and `time` are all accepted names. Export names each glTF animation after its clip
  (GlbRig.cs:101), so `nod` is found in the exported file.
- Export's text matches ExportTool.Text: overwrite (line 17/30), the first carve-limits.json in the
  .glb's folder or above it, `limits` (line 40), and `write_limits` (line 42). On `write_limits`, the
  README says the file is created. The tool also fills missing keys in an existing file. Leaving that
  out is not wrong.
- The barrel sample's material names match. T-119's Notes and As built match the diff, Status In
  Progress is right before finish, and `cairn lint` is clean.

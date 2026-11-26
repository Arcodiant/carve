---
description: >-
  Review of T-223
---
# RV-85 — Review of T-223

**Status:** Open

**Reviews:** T-223

**Round:** 1

**Previous:** —

**Diff:** —

**Reviewer:** —

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

## Findings: Core

### An unattached vertex displaced by noise does not move but is reported as moved

`src/Carve.Core/Displace.cs:137-148`. A vertex on no live face gets `normals[v] == Vector3.Zero`, so with no `axis`
`along` is zero and it stays where it is (line 139). Its offset still goes into `largest` (line 141), it is still
listed in `moved` and in the result's moved vertices (line 146), and nothing warns. The field source refuses such a
vertex as "on no face with UVs" (line 185); the noise source lets it through. Failing input: a mesh with an unattached
vertex (MeshQuery's `unattached`), `Displace(mesh, [thatVertex], new NoiseSource(7, 0.5, 4), 1, 0, null, ...)`. It
succeeds with no position changed, `report.Largest` about 0.5 m, and the vertex among the moved ones, when the same
call with `amount: 0` is refused as "nothing would change". Refuse it (or skip it with a warning) when there is no
normal and no axis, and leave it out of `largest`.

**Outcome:** —

## Findings: Server

### A group is read from the model outside the write lock, so the edit can use stale faces

`src/Carve.Server/DisplaceTool.cs:80-85`. When `group` is given, the tool reads the group's faces from
`store.Current` (`Target(store, ...)`, then `FacesOf`) and only afterwards calls `store.Edit`, which takes the write
lock and reads the model again (`ModelStore.cs:123-133`). Every other tool that takes a group resolves it inside the
lock, on the mesh the edit is given (`SetWeightsTool.cs:87`, inside `store.SetWeights`). MCP requests can run at
the same time, so another call landing between the two reads makes displace use the old group. Failing input:
`displace {"source":{"noise":{}},"amount":0.1,"group":"top"}` running while `define_group {"name":"top","faces":[4]}`
(previously `[3]`) lands: the old face 3's corners are displaced. If a `delete` of a group face lands in between, the
call is refused with a `faces[N]` deleted error naming an id the caller never gave. Fix: resolve the group inside the
`store.Edit` lambda on `m.Style`, and return the `NoGroup` problem from there.

**Outcome:** —

### Noise arguments that are valid get misleading refusals

`src/Carve.Server/DisplaceTool.cs:146-149, 154-158, 128`. `Whole` accepts only `TryGetInt32`, so a seed outside the
int range is refused as `source.noise.seed: expected a whole number, got 4294967296`, which is a whole number. The
description says only "seed a whole number". Agents often pass large seeds. Either say "a whole number from
-2147483648 to 2147483647" or fold the long into an int. In the same way, `scale: 1e400` reads as infinity through
`GetDouble` (checked on .NET 10: no exception) and Core refuses it as `∞ is not above 0`. Last, a source with neither key
gets "give graph (with output) or noise", but `output` is optional and defaults to `height`.

**Outcome:** —

## Summary

Three findings, all fixed. Unattached vertices are no longer counted as moved, the group is read under the store's lock, and the noise argument refusals are clearer.

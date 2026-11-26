---
description: >-
  Review of M-29 after the rebase onto main
---
# RV-90 — Review of M-29 after the rebase onto main

**Status:** Answered

**Reviews:** M-29

**Round:** 4

**Previous:** RV-84

**Diff:** 158cd873b1ff7fbceb9171b17224f354a5966225 against origin/main

**Reviewer:** one reviewer, claude-opus-5-5 (Claude Opus 5.5), on the rebase onto main at bba6897e: the rebased tree against a clean merge, and where M-29 meets T-223

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: the rebase onto main at bba6897e, and where M-29 meets T-223 (claude-opus-5-5)

**The rebased tree against a clean merge:** The installed git (2.36) has no `merge-tree --write-tree`, so I cloned the repo into a scratch folder under `%TEMP%`, checked out `92eed856`, merged `bba6897e` (fork point `b02b7b2a`), and diffed the working tree, conflict markers included, against the rebased head `a3d1c249` for every path outside `docs/`. Nine files differ, and nothing else does. Seven are the seven conflicts git reported, and each resolution keeps both sides with nothing dropped. `CarveServer.cs` keeps main's list with `DisplaceTool` after `SubdivideTool` and M-29's `HelpTool` at the end. `MaterialTools.cs` takes M-29's side of every hunk; that loses nothing, because M-29 already had `Missing(ToolPath, string extension = ".png")` with main's exact signature, delegating to `GraphSlotProblem.Missing`, whose body is main's line for line (same limit of 20). `tests.json` keeps M-29's globs, and they cover main's one explicit addition, `samples/boulder.carve`. `PartToolsListTests.NewTools` has both `displace` and `help`, with main's `partSpace` addition merged cleanly. `ToolHarnessTests` counts 44 and names both tools. In `CLAUDE.md` and `README.md`, every phrase main added since the fork is present once; main's "`NestedSamplesTests`, and `marble.texgraph`" is joined to M-29's text with a comma, the only rewording. Every M-29 phrase is also present; a script checked all 53 word-level additions, and the one miss is that same join. Each conflicted row appears once. The other two files are the fix commit. `Carve.Server.csproj`: the clean merge auto-merged two `Patina.Graphs` references, and the head keeps one with a combined comment. `DisplaceToolTests.cs` gains the new test. No slips. No other line of either side's reviewed code changed.

### displace tells an agent to fix a valid mesh-reading graph in patina, where there is nothing to fix
- **Where:** `src/Carve.Server/DisplaceTool.cs:201`, `:213-217` (reads with `NodeRegistry.BuiltIn`, then says "fix them in patina"); `src/Carve.Server/DisplaceTool.cs:30` and `README.md:128` (say nothing of mesh nodes); pinned by `tests/Carve.Server.Tests/DisplaceToolTests.cs:131-148`
- **Severity:** Minor
- **Rubric:** correctness
- **What is wrong:** I ran a probe on `ToolHarness` in a scratch copy. For `samples/graphs/weathered-iron.texgraph`, displace answers "has 1 fault(s), so it does not evaluate: node 'curv': unknown type 'mesh_curvature'; the types are blend, blur, ... worley; fix them in patina". A graph whose only mesh node is inside a nested file gets the same answer ("nodes['g/c']: unknown type 'mesh_curvature' ... (inner.texgraph); fix them in patina"). So does a graph with a bad mesh parameter (`width` 0): the unknown type is reported and the parameter is never reached. That much is safe: nothing changes, the mesh is left as it was, and nothing worse happens. displace reads the file again on each call (I edited it between two calls and got a different result), so it caches nothing. But the advice is false. patina registers the four mesh types, opens the graph with no fault, and evaluates it on stand-ins (`MeshNodes`: "so a graph that reads the mesh still evaluates in patina"; `MeshNodeToolTests` holds both weathered samples clean). An agent that follows the message finds nothing to fix in patina. The one "fix" it can make is to delete the mesh node, which spoils the graph for the material that uses it. Neither displace's description nor the README says a mesh-reading graph is refused, and the new test pins this wording as correct. Its own comment says "the wording is a finding for the person". Telling the agent the truth would need only the message: displace does not read mesh nodes.

**Outcome:** skipped: filed off-milestone as T-236 by the person's choice (2026-11-26); displace refuses and leaves the mesh unchanged

### displace's whole tools/list entry is pinned by nothing, the gap RV-82 closed for help and bake
- **Where:** `tests/Carve.Server.Tests/PartToolsListTests.cs:39`, `:90-103`
- **Severity:** Nit
- **Rubric:** tests
- **What is wrong:** `NewTools` is the right place for `displace`, a tool added after the M-9 baseline. But being there keeps it out of the digest. RV-82 (its finding on help and bake, answered with `The_help_and_bake_entries_are_as_pinned`) closed that gap for M-29's new tools, and the rebase did not carry the rule over to main's new tool. Any later change to displace's description or schema would pass `PartToolsListTests`. Other tests hold displace's worked example and its PartSpace sentence, and nothing else of the entry. displace does meet M-29's other description rules in fact, not by being left out. `HelpToolTests` enumerates every served tool, and displace's served description is 3,180 characters, under the 3,686 limit (unwrap is the longest at 3,685). It has no `More: help` pointer, so the pointer rule does not apply to it.

**Outcome:** skipped: filed off-milestone as T-236 by the person's choice (2026-11-26); displace refuses and leaves the mesh unchanged

### The new test's doc comment is placed after its attribute, so it documents nothing
- **Where:** `tests/Carve.Server.Tests/DisplaceToolTests.cs:131-135`
- **Severity:** Nit
- **Rubric:** the rebase changed reviewed code
- **What is wrong:** `[Fact]` comes before `/// <summary>`. It is the only test in `tests/Carve.Server.Tests` written that way; every other test puts the summary above the attribute. The comment is not attached to the method, and the build would warn CS1587 if XML documentation were generated (`TreatWarningsAsErrors` is on in `Directory.Build.props`). The build passes today only because no documentation file is generated.

**Outcome:** skipped: filed off-milestone as T-236 by the person's choice (2026-11-26); displace refuses and leaves the mesh unchanged

**Covered:** I read whole `DisplaceTool.cs`, `GraphSlotProblem.cs`, `PartToolsListTests.cs`, `HelpToolTests.cs`, `MeshNodes.cs` and the graph and refusal tests of `DisplaceToolTests.cs`. I read every hunk of the clean merge against the head, both sides' diffs since the fork of `MaterialTools.cs`, `tests.json`, `NodeCatalogue.cs`, `BlendNode.cs`, `SubdivideTool.cs` and the list tests, M-29's changes to `Patina.Graphs`, and the sample-selection rule in `CheckSamplesTests` and `MeshNodeToolTests`. Main changed no description inside the digest; subdivide's change is to its warnings only. In a `git archive` copy of HEAD I built the code and ran a probe test. It showed the `Missing()` messages are unchanged for every caller: `define_material` texture lists .png files, `define_material` graph and `displace` list .texgraph files or say the folder does not exist, and `open` lists .carve files. I also ran these unit test classes, all passing: carve's Help, PartToolsList, ToolHarness, Displace, GraphMaterialErrors and Open tests (42), and patina's CheckSamples, PatternSamples, MeshNodeTool, NodeTool and FirstNodes tests (102). `marble` uses built-in nodes only, so it falls under `CheckSamplesTests` and not the mesh-sample list. `WorkedMesh`'s `blend` in `multiply` mode is still valid. Under M-29's file writer `boulder.carve` is still written at version 3, and both sides' readable-versions code is untouched by `main`. The two changelogs do not collide: main's 0.18.1 and 0.18.2 files are new, and M-29 adds only to `UNRELEASED.md`. I did not run any regression or scenario suite, including main's `DisplaceStage` in scenario-cuts. I did not check docs/ files outside the changelog, and I did not review T-223 itself.

## Summary

A review of the rebase, not of M-29 again: `main` gained 26 commits (T-223's `displace`, releases to 0.18.2) while
rounds 1 to 3 ran, and the milestone was rebased onto it at `bba6897e`. One reviewer, since the question was narrow:
is the rebased tree the two sides put together and nothing else, and what happens where they meet.

The rebased tree equals a clean merge of the milestone before the rebase and `main`, apart from the seven conflicts'
resolutions, each keeping both sides, and the fix commit's two files. 3 findings: 1 Minor and 2 Nits, all about
`displace` meeting a mesh-reading graph and M-29's rules for tools. All three are filed off-milestone as T-236 by the
person's choice, so no code changed after this review.

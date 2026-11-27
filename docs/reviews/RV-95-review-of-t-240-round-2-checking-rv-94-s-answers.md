---
description: >-
  Review of T-240, round 2: checking RV-94's answers
---
# RV-95 — Review of T-240, round 2: checking RV-94's answers

**Status:** Answered

**Reviews:** T-240

**Round:** 2

**Previous:** RV-94

**Diff:** 168d70827d1282fd793b4b494607534be2b90c23 against origin/main

**Reviewer:** claude-opus-5-5 (fresh agent)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings

### GraphFaultTests.AnUnknownTypeListsTheTypes fails: the type list now has `image`

`tests/Patina.Graphs.Tests/GraphFaultTests.cs:52-54`. Rubric 1 and 6. The branch registers `image`, so the unknown-type fault now lists `... height_to_normal, image, input, ...`, but this test still expects the list without it. `dotnet test tests/Patina.Graphs.Tests` on HEAD: 1 failed, 584 passed. The test is unchanged on the branch and the node was added before 918aa275, so round 1 left it, not the fixes. Failure scenario: `finish` or CI runs the Patina.Graphs suite and it is red. Adding `image, ` to the expected string fixes it.

`image, ` added to the expected type list; the whole Patina.Graphs suite passes (586).

**Outcome:** fixed

### Files_lists_a_png_without_decoding_it passes without ListOnly

`tests/Patina.Graphs.Tests/ImageNodeTests.cs:236-246`. Rubric 2. RV-94's fourth finding (carve's key path decodes every PNG) is answered by `NodeBudget.ListOnly`, and this test is named as its cover. But the old `ReadImage` (918aa275) already ran `a.Files.Add(full)` before `PngReader.Decode`, so a damaged PNG was listed then too. Both assertions (`Files` is `[path]`; `Validate` gives one fault) hold with `ListOnly` deleted. Failure scenario: someone drops `budget.ListOnly ||` from `GraphValidator.cs:293`, carve's `Read` decodes on every render again, and no test fails. A test needs something that only a decode does. One way: a file that `File.Exists` sees but that is not a PNG at all, timed or counted. Another: assert that `Files` does not leave a decoded image behind, for example through an internal decode counter.

`Files_does_not_decode_a_large_png` measures allocation: listing a 4096 grey PNG allocates under 16 MiB, validating it over 64 MiB. Run with `budget.ListOnly ||` removed, it fails.

**Outcome:** fixed

### ListOnly lists an existing PNG it cannot read, which validation does not

`src/Patina.Graphs/GraphValidator.cs:292-298`. Rubric 1 (what a caller relies on). In ListOnly mode a PNG goes into `Files` once `File.Exists` is true. A real analysis lists it only after `File.ReadAllBytes` succeeds (`:299-309`). Before the fix, `Files` agreed with validation. For a PNG that exists but cannot be read (locked by the editor writing it, no read access), `Files` now names it and validation does not. Failure scenario: carve's `GraphSlotBake.Read` (`src/Carve.Graphs/GraphSlotBake.cs:56-64`) throws `the graph file '<...>.png' cannot be read`, which calls a PNG a graph file, in place of patina's `parameter 'path': could not read ...` fault. Patina's `open` (`src/Patina.Server/FileTools.cs:243-244`) says it "read" the PNG beside a fault saying it could not. The render fails either way, so the impact is small. Either state this in the `Files` summary or reword the message in `Read` to "a file it reads".

Kept: listing without reading is the point of the fix. `Files`' summary now says a PNG that is there but cannot be read is listed though validation faults it, and `GraphSlotBake.Read` says "the image file '...' cannot be read" for a PNG rather than calling it a graph file.

**Outcome:** fixed

### As built's Not verified still lists Files among the analyses that decode

`docs/tasks/T-240-image-read-a-png-into-a-patina-graph.md:46`. Rubric 7. The sentence says "Validation decodes the PNG on every analysis (define_graph, edit_graph, Uses, Files, the evaluator each run one)". The same sentence then says `Files` lists only, and decision (7) says so too. `Files` should come out of the parenthesis.

`Files` taken out of the parenthesis; it names `Images` and says `Files` does not decode.

**Outcome:** fixed

## Summary

I read `git diff 918aa275 HEAD` and the parts of `GraphValidator.cs` it touches (`ReadImage`, `Expand`, `NestedParameters`, `Files`), plus `GraphSlotBake.Read` and patina's `open`. Each RV-94 outcome was checked against its code and test.

- **Bilinear skip:** honest. The Scope does say bilinear. The limit is now stated in As built (8) and in README.md.
- **One decode per validation:** holds. The cache is per `NodeBudget`, so per validation, never shared between validations. It is keyed on the full path, and `kind` is applied later in `Texel`, so one decode serves every node. Only a successful decode is cached, so a damaged PNG still gives a fault on each node that reads it. The `Assert.Same` test fails without the cache.
- **Adler-32 and critical-chunk rows:** hold, and they pin the reader's own words.
- **save description:** holds.
- **Nested-PNG save test:** holds. It saves an identical copy, which a graph-only compare would refuse.
- **Costliest filter:** holds.
- **ListOnly:** `Files`' list is the same as before for every file that can be read. Nested files are added before any failure return in `Expand`, and the null images ListOnly leaves only affect faults, which `Files` drops. So `GraphSlotBake.Read` and `open` get the same list, except for an unreadable PNG (third finding). But its test does not pin it (second finding).

Ran: `dotnet test tests/Patina.Graphs.Tests` (584 passed, 1 failed: the first finding), `dotnet test tests/Patina.Server.Tests --filter Tier=unit` (195 passed, 6 skipped), `dotnet test tests/Carve.Graphs.Tests` (45 passed). No probes were written.

---
description: >-
  Review of M-28, round 3
---
# RV-78 — Review of M-28, round 3

**Status:** Open

**Reviews:** M-28

**Round:** 3

**Previous:** RV-77

**Diff:** 64fb8fb8358ccaae2a67f0a3730c4daffccf485b against origin/main

**Reviewer:** one reviewer, claude-opus-5-5 (Claude Opus 5.5), checking RV-77's answers and the work since it (T-178 to T-181)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: round 3 (claude-opus-5-5)

Reviewed on Claude Opus 5.5 (`claude-opus-5-5`), read only, in `D:\Repos\carve\branches\milestone\M-28` at `f53d573`, from `git diff 829f3c7..HEAD`. To probe it, I built HEAD's `src/Patina.Graphs`, extracted with `git archive`, in `%TEMP%\m28-review3\src`, and ran `%TEMP%\m28-review3\probe\p3.fsx` (logs `p3.log`, `p3b.log`) and `p4.fsx` (`p4.log`) against it. I ran no suites.

### An outer node of an unknown type with an id expansion also makes still throws, not a fault
- **Where:** `src/Patina.Graphs/GraphValidator.cs:362` (the RV-77 fix, which tests only `is NestedGraphNode`), `:94` (an outer node of any other type is put in `nodes`), `:110-114` (an unknown type is `continue`d before `a.Nodes[id]` is set) and `:555` (`Kinds` reads `a.Nodes[from.Node]` with no guard).
- **Severity:** Minor
- **Follows:** RV-77 "An outer graph node whose id expansion also makes still throws, not a fault", which follows RV-76 "An outer node whose id collides with an expanded id replaces the nested node, with no fault".
- **What is wrong:**
  - The fix leaves the use unexpanded only when the outer colliding node is a `graph` node. The throw needs only that the outer node is absent from `a.Nodes`. A node of an unknown type is absent too: it is kept in `nodes`, but faulted and skipped before it is resolved.
  - Probe `p3b.log`: `g` uses `plain.texgraph`, whose output reads `c`, beside an outer `"g/c": { "type": "nosuch" }`, with `o` reading `g`. `TexGraphFile.ReadFile` throws `KeyNotFoundException: The given key 'g/c' was not present in the dictionary` at `GraphValidator.Kinds` `Visit`, line 555. `o`'s source resolves through `use.Outputs` to `g/c` (`:166`), and nothing checks it is a resolved node.
  - The controls in the same probe hold: an outer `colour` `g/c`, and an outer `graph` `g/c` whose nested `c` is itself a graph node, each give the single `node 'g/c': an id is ...` fault.
  - So `open`, `define_graph` and `edit_graph` still give the generic error for this shape. The person's ruling in T-179's Notes is "a fault, never a throw". It is the third fix to this collision, and each has closed one type of outer node.
  - Fixing the cause would close the class: guard `:555` with `a.Nodes.ContainsKey`, as `:485` and `:544` are, or treat a use output that resolves to a node not in `a.Nodes` as at fault. A test with an unknown-type outer node would hold it.

**Outcome:** fixed

**Fix:** the cause is guarded: `GraphValidator.Kinds` reads a source's kind only when the source is a resolved node (`a.Nodes.TryGetValue`), as `Cycles` and `Kinds`' own walk already did, so a use output resolving to an outer node left out of `a.Nodes` (an unknown type) is an input of unknown kind, and the node reading it is kept out of the order. The other lookups the collision reaches were checked: `Cycles` (guarded), `Kinds`' walk (guarded), the source resolution in `Analyse` (`nodes.TryGetValue`, an unknown-type target marks the input at fault), `Expand`'s reads (the inner analysis's own nodes), and the evaluator's (never reached, the id fault refuses evaluation). `NestedGraphTests.An_outer_node_of_any_type_with_an_expanded_id_is_faulted_not_thrown` reads the probe's shape through `ReadFile` with an outer `g/c` of every built-in type, `graph` and `nosuch`: each is faulted on `g/c` and none throws; without the fix it threw `KeyNotFoundException` for `nosuch`. RV-78's probe `p3.fsx`, rerun against this build: `unknown` gives the id fault and the unknown-type fault, the `colour` and `graphgraph` controls the single id fault as before. (T-183, claude-opus-5-5)

### A nested file that a parameter drives is charged to the node cap twice, and the fault blames its expansion
- **Where:** `src/Patina.Graphs/GraphValidator.cs:441-445` (`NestedParameters`' `Read` charges the file's nodes) and `:330-335` (`Expand` charges the same file again). The cap's summary is at `:28-34`.
- **Severity:** Nit
- **Follows:** RV-77 "Fan-out through parameter drives is still exponential" and "The node cap counts the graph's own nodes only once it uses a graph".
- **What is wrong:**
  - A file read for a drive's range is charged in full. When the same graph node is then expanded, the file is charged again. So a driven file costs the cap twice what it takes in.
  - Probe `p4.log`: `big.texgraph` has 2,103 nodes, and `top` has one `graph` node `g` on it. With no drive, `top` loads with 0 faults. Add one parameter `q` driving `g.p` and it is refused: `node 'g' parameter 'path': uses big.texgraph, which takes the nodes validated past 4096 (the graph's own and each nested file's each time it is used ...)`.
  - The fault is on `path` and says "each time it is used", but the file is used once. The read for `q`'s range is what took the budget. The summary reworded for RV-77 calls it "a cap on what nesting takes in", and a driven file is taken in once.
  - Real graphs are far from the cap (9 to 33 nodes), so this costs nothing today. Either charge a drive's read only for files that are not also expanded, or say in the summary and the fault that a driven file counts twice.

**Outcome:** skipped: filed off-milestone as T-182 by the person's ruling (2026-11-22), since real graphs use 9 to 33 nodes and it costs nothing today

## Summary

Round 3, a light confirming round over RV-77's 14 answers and everything since RV-77 was written (`829f3c7..HEAD`: T-178, T-179, and the README work T-180 and T-181). One reviewer. 2 findings: 0 Major, 1 Minor, 1 Nit, both proved by probes. Every other RV-77 answer was checked and held; the README was checked claim by claim against the code, with no finding. No suites were run in this round.

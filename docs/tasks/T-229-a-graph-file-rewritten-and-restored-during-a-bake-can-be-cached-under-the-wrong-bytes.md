---
description: >-
  The bake cache compares the bytes a bake read with the bytes on disk after it, nested files included, so a rewrite from A to B and back to A during a bake cannot cache B's image under A's key
---
# T-229 — A graph file rewritten and restored during a bake can be cached under the wrong bytes

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

The bake cache writes an entry only when the bytes on disk after the bake are the bytes the bake read, for the graph
file and every nested file, so a file rewritten from A to B and back to A during one bake cannot leave B's image under
A's key.

## Scope

## Notes

- **From M-29's review.** RV-82 found that the bake hashed the files once and read them again; T-217 added a guard
  that re-reads the files after the bake. RV-83 found the guard compares two later instants and not the bytes the bake
  read ("`Unchanged` compares the files at two instants"), a Nit.
- **Filed off-milestone on 2026-11-26,** after two rounds, by the person's standing rule for a Nit that recurs.
- **What T-226 found:** the nested graph files are read again inside `Patina.Graphs` (`GraphValidator.Expand`,
  `File.ReadAllText`), so comparing against what the bake read needs a small additive hook there, engine-free and
  deterministic. Fixing the top-level file alone leaves the nested files' window.
- Nothing is cached wrongly unless a graph file is rewritten and restored within one bake.

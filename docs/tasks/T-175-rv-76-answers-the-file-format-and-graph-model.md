---
description: >-
  Every finding of RV-76's `## Findings: the file format and graph model` is answered, fixed where it is a defect
---
# T-175 — RV-76 answers: the file format and graph model

**Status:** Done

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

Every finding of RV-76's `## Findings: the file format and graph model` is answered, fixed where it is a defect, so that M-28's review round can pass.

## Scope

- All six findings under `## Findings: the file format and graph model (claude-opus-5-5)` in RV-76.
- Each fix with a test that fails without it, where a test can hold it.
- Fill each finding's `**Outcome:**` line in RV-76, in this task's worktree.

## Notes

- A `graph` node's path is relative to the using graph's file, as T-140's spec says: a rooted, drive-relative or UNC path is a fault, `..` stays allowed.
- Owns `TexGraphFile.cs`, `TextureGraph.cs`, `GraphValidator.cs`, `GraphParameters.cs`, `NestedGraphNode.cs`, `InputNode.cs` and the path check in `NodeType.cs`'s parameter kinds (T-173 owns the rest of `NodeType.cs`; keep both sides on a rebase).
- **Answering RV-76:** for each finding listed, fix it, or answer it `skipped: <reason>` or `no change needed: <reason>`, by filling its `**Outcome:**` line in `docs/reviews/RV-76-review-of-m-28.md`, in this task's worktree. Leave the other findings' lines as they are. A fix that would change behaviour beyond the finding, loosen a check, or change a committed output or an approved frame goes to the person first.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.

### As built

Run on Claude Opus 5.5 (`claude-opus-5-5`). Every finding fixed, each with a test; no committed `.texgraph`, output or frame changes.

- **Expanded-id collision:** an outer node keeps its id, is checked as a plain id and faulted; the nested node is not put over it (`GraphValidator`).
- **Rooted paths:** `ParamSpec.IsRelativePath` faults absolute, drive-relative, from-the-root and UNC paths on every system; `ResolvePath` gives null for them, so nothing is opened. `..` stays allowed.
- **Fan-out:** `GraphValidator.MaxNodes` = 4096 nodes taken in per validation, all levels down; past it a graph node's `path` is faulted. The number was approved by the person (review round 1).
- **String-list parameter value:** `ParametersField` reads it as `Texts`, so it loads and is faulted by kind; a mixed array is still a load error.
- **`Write` with `Version1`:** throws for a graph with exposed parameters instead of dropping them.
- **Nested size:** a test with inner 32 under outer 64 against the hand-written copy.

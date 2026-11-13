---
description: >-
  Export limits per game, not Orcward's constants
---
# T-51 — Export limits per game, not Orcward's constants

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** T-34

## Goal

`export`'s limit warnings (triangles, colours or submeshes, humanoid height) come from the game being exported for, not from Orcward's starting numbers fixed as constants.

## Scope

- To be planned: where a game's limits live (an `export` argument, a project or content-folder setting, or a profile), what the defaults are when none is given, and whether static exports warn too.
- D-6's *Warnings for a game's limits* is revised to match.

## Notes

- Raised by the person on 2026-11-09 during M-16: the limits T-34 ships are Orcward's numbers (D-6, Leaning, Guideline) and are not general to every user. T-34 lands them as named constants on skinned exports only, as D-6 says; this task replaces that.

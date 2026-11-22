---
description: >-
  The README's carve sections and its Build and test list describe what carve's server offers now, so the README is current on both servers when M-28 lands
---
# T-181 — Carve's README sections caught up

**Status:** Open

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-180

**Verify:** server-unit

## Goal

A person reading `README.md` sees every tool carve's server offers today, and every suite the repo runs, as the new
CLAUDE.md rule (T-180) asks of every change.

## Scope

- **Carve's tools the README never names**, found by T-180: `bake` (M-24), `keep_level`, `define_part` and `get_parts`,
  `define_target` and `delete_target`. Check `Program.Tools` (or carve's tool registration) for any others missing, and
  describe each in the section where it belongs, briefly, pointing at the tool's description for detail. A new section
  where none fits (for example baked maps).
- **`## Build and test`**: the suites in `tests.json` by tier, patina's and the scenario suites included (the three
  patina scenario suites from T-172 among them), or a sentence pointing at `cairn test list` if a full list would go
  stale.
- Anything else in carve's sections that the code makes untrue. Patina's section is T-180's and stays as it is.
- Any worked call the README gains runs as written through carve's tools once; say how in As built.

## Notes

- **Decided (2026-11-22, by the person):** T-180 found carve's README sections stale from before M-28. Asked whether to
  file it or fix it now, the person chose to fix it in M-28.
- Write from the code and the tool descriptions, not from old plans.
- Opus 5.5: Sonnet is under its weekly usage limit until 2026-11-23.

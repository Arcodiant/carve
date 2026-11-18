---
description: >-
  Description, worked-example and report-shape checks run on the harness as unit tests
---
# T-88 — Tool definition checks as unit tests

**Status:** In Progress

**Pri:** P2

**Milestone:** M-20

**Batch:** — (standalone)

**Depends on:** T-87

**Verify:** server-unit, server-regression

## Goal

The checks on what the tool list says run as `unit` tests on T-87's harness. These are descriptions, schemas,
worked examples that run clean, and every "Returns JSON" shape naming its fields. Most of them start a fresh
`carve` per `[InlineData]` row today, only to read `tools/list`.

## Scope

- Find them across the regression classes: tests built on `ListToolsAsync` and `Examples(...)`, such as
  `EditToolTests.Every_worked_example_in_a_description_runs_on_the_cube_closed_and_warning_free` and
  `Every_edit_report_shape_in_a_description_names_deleted_and_split`, and their peers in `SelectToolTests`,
  `DefineMeshTests`, `NamedMeshTests`, `SubdivideToolTests` and the cut tools.
- Move each to a `unit` class on the harness with the same assertions on the same inputs. Do not loosen any.
- If a worked example renders, it stays in `regression`; say which ones did in the notes.
- Every test keeps its `Tier` trait. A moved class has no `regression` cases left behind by accident.

## Notes

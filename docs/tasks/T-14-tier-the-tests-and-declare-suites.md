---
description: >-
  The tests carry Tier traits and tests.json declares suites, so a member re-runs what moved instead of the whole hook
---
# T-14 — Tier the tests and declare suites

**Status:** Open

**Pri:** P1

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Perf:** skip: Carve declares no heavy suites

## Goal

Carve reaches L2 of D-8's testing onboarding, as Waystone's `request/waystone/test-tiers` asks: a `tests.json` splits the tests from the cube scenario, and M-2's plan is written for it, before M-2 opens.

## Scope

- **`Tier` traits** on every test class, by what the class does rather than `cairn test discover`'s hint:
  - `unit`: all of `Carve.Core.Tests`; `MeshBridgeTests`, `ModelStoreTests`, `ProductReferencesNoExtensionTests`, `RenderPumpTests` and `ViewFramingTests` in `Carve.Server.Tests`.
  - `regression`: `DebugExtensionTests`, `DefineMeshTests`, `LoaderTests`, `RenderTests` and `StdioTests`, which start the real `carve`.
- **`tests.json`** beside `.cairn.yml`: `core-unit`, `server-unit`, `server-regression` and `scenario-cube`, each with its inputs, slot and estimate. No test or scenario leaves the set the old hook ran.
- **Checked once against the old hook**: `verify --fresh` runs the same 238 tests and the cube scenario. Record the counts and times in the closing report.
- **`CLAUDE.md`**: the verify rule and *Layout* name the suites; `legacy` is gone from them.
- **M-2's plan**: T-12 adds a `scenario-crate` suite instead of editing `verify.ps1`, T-13 checks through cairn, and T-8 to T-13 carry `**Verify:**` and `**Perf:**` lines.

## Notes

- The worked examples Waystone mentions do not exist yet, and the request says not to wait for them.
- `scripts/verify.ps1` stays until L3 retires it. With a `tests.json` the gate no longer runs it.
- No tolerance, assertion or test target changes here. A test that does not fit its tier goes back to the person.

---
description: >-
  Move CI to actions/checkout v7 and actions/setup-dotnet v6, which run on Node 24.
---
# T-118 — CI actions on Node 24

**Status:** Done

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Review:** skip: two version numbers in the CI workflow; the CI run on main is the check

## Goal

CI runs without the Node 20 deprecation warning that its first run (T-109) raised.

## Scope

- `.github/workflows/ci.yml`: `actions/checkout@v4` to `@v7`, `actions/setup-dotnet@v4` to `@v6`, the latest majors, both `node24`. Their release notes change nothing CI uses (`dotnet-version` is unchanged; checkout v7's fork-PR block applies only to `pull_request_target` and `workflow_run`).

## Notes

### As built

As scoped. The check is the next CI run on `main`, after `finish`.

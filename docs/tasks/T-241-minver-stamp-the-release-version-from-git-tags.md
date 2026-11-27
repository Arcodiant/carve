---
description: >-
  MinVer sets the assembly version from the vX.Y.Z tags, so ServerInfo and the update check see the real version
---
# T-241 — MinVer: stamp the release version from git tags

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** BA-1

**Depends on:** — (none)

## Goal

MinVer takes the release version from the `vX.Y.Z` git tags and stamps it into the carve and patina builds. Then `ServerInfo.Version` and T-242's update check have a real version to compare.

## Scope

- Add the `MinVer` package to `Directory.Build.props` with `PrivateAssets="all"`, its version pinned there like the other packages. Set `MinVerTagPrefix` to `v`, because the tags are `v0.20.0` and the like. Every project gets it this way.
- `EngineHost` (`src/Carve.Hosting/EngineHost.cs`) reports the full semantic version in `ServerInfo.Version`. Read it from `AssemblyInformationalVersionAttribute` with any `+commit` metadata dropped, not from `GetName().Version`. Expose that version from one place in `Carve.Hosting` so T-242 can read it.
- CI (`.github/workflows/ci.yml`): give `actions/checkout` `fetch-depth: 0`. Without the tags, MinVer falls back to `0.0.0-alpha.0`.
- A tagged commit gets the version `X.Y.Z`. A later commit gets MinVer's `X.Y.(Z+1)-alpha.0.N`, which T-242 must count as newer than `X.Y.Z`.
- Check that `%LOCALAPPDATA%/carve-mcp/update.ps1` still produces a stamped version. It publishes from the `branches/main` worktree, so MinVer should see the tags there.
- Add a test in `tests/Carve.Server.Tests` (no GPU). It checks that the reported version is not `1.0.0.0` or `0.0.0`, and that it equals the informational version without the metadata.

## Notes

- 2026-11-27 discussion: the person chose MinVer over a hand-edited `<Version>` property, so a release needs only its tag. `.cairn.yml`'s `release:` stays empty.

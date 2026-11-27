---
description: >-
  A background check of GitHub's tags at startup in Carve.Hosting, one notice per session in a tool result and in help {}, with an opt-out
---
# T-242 — Tell the agent when a newer carve is tagged on GitHub

**Status:** Open

**Pri:** P3

**Milestone:** — (off-milestone)

**Batch:** BA-1

**Depends on:** T-241

## Goal

When a newer carve is tagged on GitHub, the agent using carve or patina is told so once per session, without calling anything. The notice names the version it runs and the version that is out.

## Scope

- Put the check in `src/Carve.Hosting`, so carve and patina both get it. It starts in the background when the server starts and does not delay startup or `initialize`.
  - It sends `GET https://api.github.com/repos/Arcodiant/carve/tags`. The request is public and unauthenticated, and its `User-Agent` is the product name and version only. The API does not sort the tags, so it takes the highest `vX.Y.Z` tag in semantic-version order and ignores tags of any other form.
  - It compares that tag with the running version from T-241 by semantic-version precedence. A prerelease such as `0.20.1-alpha.0.3` counts as newer than `0.20.0`.
  - It has a 3-second timeout. If the request fails, the answer is not 200 (the rate limit is 60 an hour per address), the JSON cannot be read, or no tag matches, the check ends silently. It writes a line to the server's log file and tells the agent nothing.
  - It runs once per process, with no cache file.
- When a newer tag is found, add one line as an extra text block to the first tool result returned after the check finishes, and to no other: `carve 0.21.0 is available (this is 0.20.0). Update the checkout or the published copy; what changed is in docs/changelog.` Patina's line names patina.
  - Do this in one place for every tool, not tool by tool. Use the MCP SDK's call-tool filter; in ModelContextProtocol 2.3 that should be `WithRequestFilters`/`AddCallToolFilter`, which still needs confirming.
  - Leave error results alone and add the line to the next result that is not an error.
- While a newer version is known, `help {}` always ends with the same line, so an agent that missed the first notice can still find it. This applies to carve's `HelpTool` and to patina's equivalent if it has one.
- The configuration key `UpdateCheck` = `false` turns the check off. Read it through the host's configuration, so that both the environment variable `UpdateCheck=false` and the argument `--UpdateCheck false` work. Document it in the README beside the build instructions.
- Add tests in `tests/Carve.Server.Tests` (no GPU, no network). The GitHub fetch sits behind a seam that the tests replace with a stub. The tests cover:
  - picking the tag from an unsorted list with non-semver tags, against prerelease running versions;
  - newer, equal, older and failed checks;
  - the notice appearing on exactly one result and skipping error results;
  - `help {}` carrying the notice;
  - the opt-out stopping the fetch.
- No suite reaches GitHub: under test, the check is off or stubbed.

## Notes

- Decided in the 2026-11-27 discussion with the person:
  - Read tags rather than GitHub Releases. The repo has tags up to `v0.20.0` and no Releases, so `releases/latest` returns 404.
  - Make the check passive rather than a tool, because an agent would not call an update tool unprompted.
  - Put the notice in a tool result, not in the MCP `instructions`. Those are sent at `initialize`, before the check can finish.
- Carve cannot update itself. It runs either through `dotnet run` from a checkout or as the published copy that `update.ps1` refreshes, so the notice can only inform.

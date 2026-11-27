---
description: >-
  carve and patina know their own version and tell the agent when a newer one is tagged on GitHub
---
# BA-1 — Update check

**Status:** Open

**Milestone:** — (standalone)

## Why

An agent using carve or patina cannot tell it is running an old build. Neither server knows its own version today: no project sets one, so ServerInfo reports the default. This batch first stamps the version from the release tags. It then checks GitHub for a newer tag and tells the agent once per session.

## Members

- T-241 — MinVer: stamp the release version from git tags
- T-242 — Tell the agent when a newer carve is tagged on GitHub

## Notes

- Raised in a discussion on 2026-11-27. The person chose MinVer for the version and asked for the two tasks as one batch. T-242 depends on T-241.

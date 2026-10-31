---
description: >-
  An agent connects over MCP, defines a mesh by coordinates and gets a shaded render back
---
# M-1 — A cube you can see

**Status:** Planned

**One-liner:** *An agent connects over MCP, defines a mesh by coordinates and gets a shaded render back*

## What this milestone delivers

An agent connects any ordinary MCP client to `carve`, defines a cube by coordinates and gets back a shaded render it can look at. That one path runs for real, kernel to Empyrean to image.

Underneath it: the first cut of the extension API, the debug extension, the first scenario on the scenario harness, and the guard test that the product references no extension.

Content: the cube, a shaded look that shows its form clearly, and tool descriptions an agent can act on with no other documentation.

## Open at planning

- Which package composes a windowless Empyrean host inside a long-lived MCP server, and does the frame loop or `IOffscreenRenderer` drive a render? Is the renderer in the server process or a child process?
- Which MCP library: the official `ModelContextProtocol` package or something thinner?
- The arguments of define-mesh, and how much a call returns.
- How does a render come back: an MCP image, a file path, or both?
- What a repeatable frame means here, and whether scenarios compare frames or only keep them to be looked at.
- How extensions are found and loaded, given the server is started by an MCP client.

## Required tasks

## Retrospective

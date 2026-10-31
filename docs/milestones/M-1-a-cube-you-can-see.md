---
description: >-
  An agent connects over MCP, defines a mesh by coordinates and gets a shaded render back
---
# M-1 — A cube you can see

**Status:** Active

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

**Decided at planning (2026-10-31).** Each was decided by the person, after probes on the planning machine (RTX 5070 Ti, engine 0.117.0; kept in `artifacts/m1-probe/` and `artifacts/m1-probe2/`, not committed).

- **Host and process.** The renderer is in the server process. A generic host (`Host.CreateEmptyApplicationBuilder`) owns one container holding both the engine and MCP. Our own `IHostedService` runs `IGameLoop` on a dedicated `frame` thread, paced at 20 Hz, because `EmpyreanHost` refuses `IHostedService`. `IOffscreenRenderer` draws on request from a system's `Update`. If Vulkan fails, MCP stays up and `render` reports it.
- **MCP library.** The official `ModelContextProtocol` 2.3.0.
- **`define_mesh`.** `vertices` and `faces` of any size, counter-clockwise from outside. One current mesh, replaced by each call. Returns counts, `closed`, bounds and warnings, no geometry. Bad input is a tool error naming the element; an open or inconsistent surface is a warning.
- **How a render comes back.** An MCP image block plus one caption line. No file path. 512x512, views `front` and `back`, no grid or axes.
- **Repeatable frame.** The same raw pixels from the same calls on the same machine. Scenarios render twice and require byte equality, plus pixel checks. Frames are kept in `artifacts/` to be looked at. No committed goldens in M-1, so no tolerance is chosen. `verify.ps1` runs the scenario, so verify needs a Vulkan device.
- **Extensions.** Only those listed in `Carve:Extensions`, in order, from the command line, environment or a client's server entry. Nothing is scanned; a plain launch loads none. The public API is `Carve.Extensibility`.
- **Unattended runs.** The debug extension swaps the transport for an in-memory pipe and drives it with the SDK's `McpClient`, so scenario calls take the real path.

Agreed frame, `front` (`back` is the same from the opposite corner, with the light turning with the view):

```text
+------------------------------+
|  dark neutral (0.03 linear)  |
|                              |
|         _.-''''-._           |
|     _.-'  bright  '-._       |
|    |'-._   top   _.-'|       |
|    |    '-.__.-'     |       |
|    | mid   |  dark   |       |
|    | front | side    |       |
|     '-._   |     _.-'        |
|         '-.|_.-'             |
|  ~8% margin, no overlay      |
+------------------------------+
        512 x 512

caption: 512x512, view front, 8 vertices, 6 faces
```

Facts the plan rests on, measured: Empyrean winds clockwise from outside, so the bridge reverses Carve's counter-clockwise faces; lighting is absent in the frame that creates the scene, so a change renders on a later `Update`; frames were byte-identical across frames and processes; about 1 s from launch to first image and about 0.15 s per call after that; idle 1.8% of one core and 169 MB. No engine need was found.

Task order: T-1 and T-2 side by side; then T-3, T-4, T-5, T-6, T-7 in sequence.

## Required tasks

- [ ] [T-1](../tasks/T-1-polygon-mesh-kernel.md) — Carve.Core holds an indexed polygon mesh: validation, stats, flat face normals and ear-clipped triangulation
- [ ] [T-2](../tasks/T-2-server-host-and-extension-api.md) — carve starts as a generic-host MCP server over stdio, runs the engine on its own thread and loads extensions from an explicit list
- [ ] [T-3](../tasks/T-3-define-mesh-tool.md) — An agent defines a polygon mesh by coordinates through define_mesh and gets counts, bounds and warnings back
- [ ] [T-4](../tasks/T-4-shaded-render-tool.md) — An agent calls render and gets a shaded Empyrean image of the current mesh from the front or the back
- [ ] [T-5](../tasks/T-5-debug-extension.md) — A debug extension drives the product unattended through an in-memory MCP client with a fixed clock, seed and run folder
- [ ] [T-6](../tasks/T-6-cube-scenario-and-verify-wiring.md) — The cube scenario renders a cube and an L-block from both views, proves byte-equal frames and runs in verify
- [ ] [T-7](../tasks/T-7-ordinary-client-acceptance-run.md) — A fresh agent in an ordinary MCP client builds a shape from the tool descriptions alone, and how to install carve is written down

## Retrospective

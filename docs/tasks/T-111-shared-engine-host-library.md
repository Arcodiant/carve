---
description: >-
  Carve.Hosting: the engine thread, extension loading, host set-up, fixed sky and texture upload rules move out of Carve.Server, every frame unchanged
---
# T-111 — Shared engine host library

**Status:** In Progress

**Pri:** P1

**Milestone:** M-27

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** core-unit, server-unit, server-regression, scenario-cube, scenario-sky, scenario-rig, scenario-crate, scenario-uvs, scenario-shed, scenario-export, viewer-cube, scenario-character, scenario-soft, scenario-materials, scenario-cuts, scenario-gatehouse, scenario-orcward

## Goal

A library both MCP servers host through, so patina (T-113) runs the engine, loads extensions (the debug extension and
scenarios among them) and draws with Carve's fixed sky and texture rules exactly as `carve` does. This is a move out of
`Carve.Server`: no behaviour, frame or tool changes.

## Scope

- **Read first:** M-20's As built (T-87 to T-90). M-27 waits for M-20 to land, and T-87 changed `Program.cs`,
  `EngineStatus.cs` and how tests reach the tools (`CarveServer.AddTools`, the in-process harness). Build on what
  M-20 left, and keep its harness working.
- **New project `src/Carve.Hosting`** (class library; references `Carve.Extensibility`, the engine, the MCP SDK and
  `Microsoft.Extensions.Hosting`). Move into it, public where a second server needs them:
  - the host set-up now in `Program.cs`: configuration layering (product defaults, extensions, environment, command
    line on top after each hook), logging to stderr and the temp folder (`FileLoggerProvider`), extension loading
    (`ExtensionLoader`, `ExtensionLoadException`), `RunHooks`, the stdio transport unless an extension supplied one,
    and the start and shutdown sequence. The server's name, its log file prefix and its tool list are parameters;
  - `EngineThread` and `EngineStatus`;
  - `RenderSky` (the fixed sky);
  - the texture upload rules from `PbrMaterials`: `Sampler`, sRGB for colour and linear for data, and mipmaps on
    (T-78's ruling 1). Upload is from RGBA8 pixels, so patina need not encode a PNG to draw one;
  - and anything else generic that the research pass finds a second server needs to queue an offscreen render and
    read it back (`RenderQueue`, the parts of `RenderPump` that are not Carve's looks). Record what moved and what a
    second server must write itself.

  `Carve.Server` keeps its tools, its looks, `ModelStore`, export and everything about meshes. `Program.cs` shrinks to
  the host call with carve's name and tools.
- **Guards keep holding:** the product-reference guard (the product references no extension, now checked for
  `Carve.Hosting` too); `CoreIsEngineFreeTests`; the viewer links no Carve code (T-79's ruling).
- **The frame guard:** every frame of every scenario suite, and every `server-regression` test frame, is
  byte-identical by hash before and after. Take the table on this machine from a run before the first commit and one
  after the last, and keep both tables in the evidence folder. `carve`'s `tools/list` is byte-identical.
- CLAUDE.md's Layout: a row for `src/Carve.Hosting`, and the `Carve.Server` row updated.

## Notes

- Decided at planning (2026-11-17, by the person): a shared host library, not linked files or a second copy. M-27
  waits for M-20, so this task does not race T-87's changes to the same files.
- Decided at planning by the orchestrator: the library is named `Carve.Hosting`, and it keeps the `ICarveExtension`
  API by that name. Patina loads the same extensions, so `Carve.Debug` and the scenario harness work for it unchanged.
- **Design-heavy: it decides the API T-113 and T-114 host through.** Opus, with a research pass that reads M-20's
  As built and lists what moves before any code moves.
- **Stop and ask** if a frame or `tools/list` changes by even one byte, if a guard test would need loosening, or if
  something must move that changes behaviour (a default, an order of configuration sources).
- Write down in this file's As built: the host API (how a server names itself and registers tools, systems and its
  render pipeline), and what a second server must supply for an offscreen render. T-113 and T-114 build on it and
  must not edit `Carve.Hosting`. If they need more, they stop.
- Plan for the GPU queue (M-5): submit the scenario suites early and keep working. The member does not hand off only
  to wait on a queued `verify`.
- Runs beside T-110: both add to `Carve.slnx` and CLAUDE.md, and a conflict there only adds, so keep both sides.

### Research pass: what moves

Read before any code moved (Claude Opus 5.5, `claude-opus-5-5`): `Program.cs`, `CarveServer.cs`, `EngineThread.cs`,
`EngineStatus.cs`, `ExtensionLoader.cs`, `FileLoggerProvider.cs`, `RenderSky.cs`, `RenderLights.cs`, `PbrMaterials.cs`,
`RenderQueue.cs`, `RenderPump.cs`, `RenderComposition.cs`, `RenderSystem.cs` (its scene set-up and `Render`), the test
project's linked files and `ProductReferenceGuard`, and T-87 to T-90's As built.

Moves to `src/Carve.Hosting` (namespace `Carve.Hosting`):

- **`Program.cs`'s host set-up** becomes `EngineHost.RunAsync(args, EngineHostOptions)`, the same statements in the same
  order: the empty application builder; product defaults (`Empyrean:Time:FixedDeltaTime` 0.05, `PaceToFixedStep`
  true, now `EngineHostOptions.Defaults`, the same values by default), the environment and the command line; console
  logging to stderr at every level and `FileLoggerProvider` at `<temp>/<prefix>-<pid>.log`; `ExtensionLoader.Load`;
  `AddMcpServer` with `ServerInfo` (name, version of the server's own assembly); the empty tool collection; the
  `ConfigureHost` hooks with the environment and command line added on top after each; the engine core, headless
  graphics and configuration; the stdio transport unless an extension registered an `ITransport`; then **the server's
  own registrations** (a callback, where `CarveServer.AddTools` and the render pipeline and `RenderSystem` sat); then
  `EngineThread`; build, start, the `OnStarted` hooks, wait for shutdown, `Environment.ExitCode`. Error lines keep the
  `<name>: ` prefix. Parameters: the name, the log file prefix (defaults to the name), the version assembly, the
  defaults, the registration callback.
- **`ExtensionLoader`, `ExtensionLoadException`, `FileLoggerProvider`, `RunHooks`**: internal to the library; the key
  stays `Carve:Extensions`, so patina loads the same extensions the same way.
- **`EngineThread`** (internal; the host registers it) and **`EngineStatus`** (public: tools await it). `CarveServer.AddTools`
  keeps registering `EngineStatus`, so the in-process harness (T-87) works unchanged; the host adds one only if the
  server did not.
- **`RenderSky`** (the fixed sky), public.
- **`TextureUpload`**: `PbrMaterials.Sampler` and the upload rule (RGBA8 pixels; sRGB for colour, linear for data;
  mipmaps on). `PbrMaterials` keeps decoding the PNG and caching, and calls it.
- **Offscreen render plumbing a second server needs**, cut from carve's own types with no change of calls or order:
  `OffscreenTarget` (the resolved, 4x multisampled and depth targets of one size, made in the order
  `RenderComposition` makes them, and the five pipeline steps it declares for them), `OffscreenRequest<TSpec, TFrame>`
  and `OffscreenQueue<TRequest>` (what `RenderRequest` and `RenderQueue` are, less Carve's store version), and
  `OffscreenFrame.ReadAsync` (ask the readback, then `RenderFrame`, as `RenderSystem.Render` does).

Stays in `Carve.Server`: every tool, `CarveServer.AddTools`, `ToolFolder`, `ModelStore`, export, `RenderSpec` and
`RenderedFrame`, `RenderPump` (all of it decides what Carve's looks show: mesh versions, targets, highlights, weights,
poses, glb), `RenderComposition` (Carve's three sizes, background and tags, now built of `OffscreenTarget`s),
`RenderSystem`, `RenderLights` (its light turns with Carve's views), `PbrMaterials` (it builds from Carve's
`Material`), the checker texture (pixel-art sampler, no mipmaps: a look, not the texture rule).

Guards: `ProductReferenceGuard`'s allowed set gains `Carve.Hosting` (carve now references it); new checks that
`Carve.Hosting`'s project and built assembly reach no extension. Frames: `server-regression` frames had no record, so
the first commit adds `FrameLog` (tests only), which writes every frame and `tools/list` the real process sends, by
hash, under `CAIRN_ARTIFACTS`; the before table is that commit's queued run, product code untouched.

---
description: >-
  Carve.Hosting: the engine thread, extension loading, host set-up, fixed sky and texture upload rules move out of Carve.Server, every frame unchanged
---
# T-111 — Shared engine host library

**Status:** Done

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
  mipmaps on). `PbrMaterials` keeps decoding the PNG and caching, and calls it. (Since the milestone's refactor pass
  (f601bdf) `PbrMaterials` keeps no `Sampler` of its own: its summary names `TextureUpload.Sampler`.)
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

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`).

**What moved.** As the research pass lists, with no change of calls or their order. `src/Carve.Hosting` (namespace
`Carve.Hosting`) references `Carve.Extensibility`, `Arcodiant.Empyrean.Graphics`, `ModelContextProtocol` and
`Microsoft.Extensions.Hosting`, and no Carve code besides the extension API. `ExtensionLoader`, `ExtensionLoadException`,
`FileLoggerProvider` and `EngineThread` are internal to it; the rest below is public. `Carve.Server` references it, with
a global `using Carve.Hosting`; `tests/Carve.Server.Tests` uses it the same way and no longer links `RenderSky.cs`.

**The host API** (T-113 and T-114 build on this and do not edit `Carve.Hosting`):

```csharp
return await EngineHost.RunAsync(args, new EngineHostOptions
{
    Name = "patina",                       // serverInfo.name, and the "patina: " prefix of the host's stderr lines
    Product = typeof(Program).Assembly,    // serverInfo.version is this assembly's version
    LogFilePrefix = null,                  // optional; the log is <temp>/<prefix or Name>-<pid>.log
    Defaults = EngineHostOptions.EngineDefaults, // optional; the lowest configuration source (fixed 0.05 s step, paced)
    AddServer = server =>                  // EngineHostContext: Services, Mcp (IMcpServerBuilder), Configuration
    {
        server.Mcp.WithTools([...]);                              // tools
        server.Services.AddSingleton<OffscreenQueue<MyRequest>>(); // services the tools and systems share
        server.Services.AddSingleton<MyComposition>();            // the render pipeline
        server.Services.UseRenderPipeline<MyComposition>();
        server.Services.AddSystem<MySystem>();                    // systems
    },
});
```

The order inside `RunAsync` is carve's `Program.cs` before the move: defaults, environment, command line; console log to
stderr and the file log; `Carve:Extensions` loaded (a load failure is `<name>: <message>` and exit 1); `AddMcpServer`;
`ToolCollection ??= []`; each extension's `ConfigureHost` with the environment and command line added on top after each
(a throwing hook is exit 1); `AddEmpyreanCore`, `AddEmpyreanGraphicsHeadlessCore`, `AddEmpyreanConfiguration`; the stdio
transport unless an extension registered an `ITransport`; **`AddServer`**; `EngineStatus` if the server did not register
one (`TryAddSingleton`); `EngineThread`; build, start, `OnStarted` hooks, wait for shutdown, `Environment.ExitCode`. So
the debug extension, its in-memory transport, `ScenarioDriver` and any scenario load into a second server unchanged.

**Engine state for tools.** `EngineStatus` (singleton): `WhenSettled` (ready or faulted), `FaultMessage`, `WhenStopped`,
and `EngineStatus.Faulted(message)` for an in-process harness with no engine. A tool that renders does what carve's do:
await `WhenSettled`, refuse with `FaultMessage` if set, post, then `Task.WhenAny(frame, status.WhenStopped)`.

**What a second server supplies for an offscreen render.**
- A pipeline: an `IRenderPipelineConfigurator` holding one `OffscreenTarget(resources, size)` per size (resolved
  `Rgba16Float` with `TransferSource`, 4x MSAA colour, 4x MSAA `Depth32Float`) and calling
  `target.Declare(builder, context, prefix, background, cameraTag, immediateTag?)` from `Compose` (declares the three,
  clears colour and depth, the scene stage for cameras tagged `cameraTag` resolved into `Resolved`, and an immediate
  surface tagged `immediateTag` if given).
- A scene: a system that creates a scene, a camera per tag (`AddCamera3D` and a `RenderTag`; the headless host does not
  apply a camera's projection, so the system sets it), its lights, and `entities.AddComponent(sky, RenderSky.Sky)` for
  carve's fixed sky. The ambient (0.25) and directional (0.9) lights are carve's `RenderLights`, which stays in
  `Carve.Server` because its light turns with carve's views; a server that wants carve's light writes the same two
  components.
- Textures: `TextureUpload.Create(resources, rgba8, width, height, srgb)` (sRGB for colour, linear for data, mipmaps,
  `TextureUpload.Sampler`, the one sampler: `PbrMaterials.Sampler` went in the refactor pass, f601bdf), from pixels; no
  PNG needed. Materials are the engine's `Material.Pbr`.
- A queue: requests derive from `OffscreenRequest<TSpec, TFrame>` (`Spec`, `Result`, `Complete`, `Fail`), held in an
  `OffscreenQueue<TRequest>` (`Enqueue` from a tool, `TryPeek`/`TryTake` on the frame thread). What is due when is the
  server's own (carve's is `RenderPump`: store versions, looks, glb).
- A read: in the system's `Update`, `OffscreenFrame.ReadAsync(renderer, readback, target.Resolved)` (asks the readback,
  then `RenderFrame`), and complete the request from its continuation. The pixels are linear `RenderTargetPixels`; the
  server encodes them (carve's `ToDecodedImage` and PNG writing stay in `Carve.Server`).

**Guards.** `ProductReferenceGuard`'s allowed set gains `Carve.Hosting` (needed: carve references it, and any other
`Carve.*` name is still forbidden); three new `unit` tests check that `Carve.Hosting`'s project, project closure and
built assembly reach no extension, that its only Carve reference is `Carve.Extensibility`, and that carve references it.
`CoreIsEngineFreeTests` and the viewer's no-Carve-code test are unchanged; only a comment in `ViewerSystem.cs` names
the sky's new home.

**One visible difference, not behaviour:** the engine thread's log lines carry the category `Carve.Hosting.EngineThread`
where they said `Carve.Server.EngineThread`. No test or script reads it.

**Frame guard: holds.** Evidence in `artifacts/T-111/` (ignored, in this worktree): `frames-before.tsv` and
`frames-after.tsv` (991 rows each, built by `hashtable.py <root> <out>` from `before/` and `after/`), and the two
logged `server-regression` runs `before2/` and `after2/`.
- *Scenario suites:* all 466 PNG rows of the 15 suites are byte-identical, before (the start commit, on Xellotath)
  against after (6be5cf6, or 543ca50 for sky, rig and crate, whose later commits touch only test code and docs; on
  Mantorok).
- *`server-regression`:* `tools/list` is the same two hashes in every run. The set of distinct (request, frame) pairs
  is identical in `before/` (484fab5), `before2/` (41e38fd: 484fab5's tree with only the test-name logger) and
  `after2/` (14afce8). Leaving out two tests, the whole table, counts included, is identical between `before2/` and
  `after2/`. The two are timing races in the tests, seen on the before tree too: `RenderTests.A_render_after_define_mesh_returned_shows_that_mesh_with_renders_overlapping`
  sends `render {}` while meshes are being defined, so which of three frames each render shows depends on timing
  (`before/` and `before2/`, the same product code, differ in those counts: 79/82/78 against 82/81/76; this was the
  "24a160af against 16ed43a9" difference); and `NamedMeshTests.A_render_with_the_lids_ids_drawn_on_a_later_model_is_refused_...`
  renders until a race refuses one, so how many succeed varies (6be5cf6's one extra row; `after2/`, with the same
  product code, does not have it).

Finished by Claude Opus 5.5 (`claude-opus-5-5`), the second agent: it settled the frame guard above; no product code
changed after 6be5cf6.

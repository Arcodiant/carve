---
description: >-
  Review of M-1
---
# RV-1 — Review of M-1

**Status:** Open

**Reviews:** M-1

**Round:** 1

**Previous:** —

**Diff:** 11b1d2ec4caeb8b5227358cdbf3451777ac9ff4a against origin/main

**Reviewer:** claude-opus-5-5 (four reviewers)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: render and threading (claude-opus-5-5)

### Render after define_mesh can draw the previous mesh, or fail on a null report

`src/Carve.Server/RenderSystem.cs:52-69`. Correctness; tests cover the Scope. `store.Current` is read once per `Update`, before the drain, and the `while` keeps taking requests of the same view without rechecking it. T-4's Scope promises the version change is handled first. Stale form: with a render queued or in flight, `define_mesh(pyramid)` returns, and the next `render` is taken by the same `while` and draws the old mesh, a success with the old image and counts. Measured on the unmodified product, delay 0-55 ms: 14 of 60 with 1 render in flight, 29 of 60 with 2, 48 of 60 with 5; 20 of 20 in a burst of 30. It needs overlapping calls. Null form: first mesh, view `front`; line 52 reads version 0, then `define_mesh` and `render` complete before line 59, and `_report!` at line 150 throws, giving `The render failed: Object reference not set...`. Reproduced only with a delay inserted (7 of 10 at 45 ms). `Replacing_the_mesh_changes_the_image_on_the_very_next_call` is sequential and cannot catch either form.

**Outcome:** fixed

### The framing floor breaks the 8% margin for a small mesh far from the origin, and for a tiny one

`src/Carve.Server/ViewFraming.cs:41`. Correctness; tests cover the Scope. `Math.Max(1e-6f, largest * 1e-4f)` raises `radius` above the real half-diagonal; the floor exists only to keep the camera apart from a point. Foreground width of a unit cube, 376 px at the origin: side 1 at x=1e5 is 99 px, at (1e6,1e6,1e6) is 10 px; side 1e-7 at origin is 99 px, side 1e-8 is 10 px. No error is given. `ViewFramingTests.Bounds` stops at a box 37 from the origin.

**Outcome:** fixed

### An exception in ShowMesh ends the engine for good and is reported as a Vulkan problem

`src/Carve.Server/RenderSystem.cs:99`, `src/Carve.Server/EngineThread.cs:72`. Correctness. `Render` catches per request, but `ShowMesh` and `ApplyView` do not. An exception from `resources.CreateMesh` (injected for the test) ends `loop.Run()`; `render` and every later render report `Engine failed (is a Vulkan device available?)` until restart, on a working device. No real input was found that throws; an allocation failure on a very large mesh is the likely trigger, and `define_mesh` has no size limit.

**Outcome:** fixed

### T-2's As built names EngineStatus members that no longer exist

`docs/tasks/T-2-server-host-and-extension-api.md:70` against `src/Carve.Server/EngineStatus.cs`. Tracker hygiene. As built lists `IsReady`, `IsFaulted`, `FaultMessage`, `WhenSettled`; commit d6af7da removed the first two and did not touch T-2. Also reported by the whole-diff pass.

**Outcome:** fixed

## Findings: kernel and define_mesh (claude-opus-5-5)

### Argument-binding failures return an opaque error with no element named

`src/Carve.Server/DefineMeshTool.cs:64-66`, `:72`. Correctness (T-3 Goal: "exactly what is wrong"). The typed `double[][]`/`int[][]` parameters fail in SDK binding before `Run` validates. Each returns only `An error occurred invoking 'define_mesh'.`: `"faces":[[0,1.5,2]]`, `[[0,1.0,2]]`, `[[0,99999999999,2]]`, `[[0,null,2]]`, `"vertices":[[0,0,0],[1,null,0],[0,1,0]]`, a vertex `[1,"x",0]`, `"vertices":null`, `"faces":null`, a missing `faces`, `{}`. That contradicts the description at `:31-32`, and one fractional index hides every other problem. T-3's As built on this is inaccurate. No test covers these.

**Outcome:** fixed

### Single-precision squares overflow and underflow, so large faces get a zero normal and faces at either extreme are refused for the wrong reason

`src/Carve.Core/PolygonMesh.cs:120,139-140,153,159`, `src/Carve.Core/Triangulator.cs:37-38`. Correctness. `Vector3.Length()` squares a Newell vector of order size^2, overflowing at side about 1e10 and underflowing at about 1e-12. Triangle `[0,0,0],[1e10,0,0],[0,1e10,0]`: accepted, `FaceNormal(0)` is `<0,0,0>`, area Infinity. Quad of side 1e10: refused as "cannot be triangulated". Side 1e20 and side 1e-12: refused as "zero area". Side 1e-11: accepted with non-unit normals (z 0.9921 and 1.0097). No test exceeds scale 1000.

**Outcome:** fixed

### A self-intersecting outline can be accepted and draws overlapping triangles

`src/Carve.Core/Triangulator.cs:50-85`, error text at `src/Carve.Core/PolygonMesh.cs:110-111`. Correctness (T-1: "A face it cannot clip is a validation error, not a bad draw"). Nothing checks the outline is simple. The double-wound hexagon `[0,0],[4,0],[2,3],[0.1,0.1],[3.9,0.1],[2,2.5]` (z=0), `faces:[[0,1,2,3,4,5]]`, with crossing edges 2-3 and 5-0, is accepted with no warning and gives total area 10.535 inside an outer triangle of area 6.

**Outcome:** fixed

### The "failed call keeps the previous mesh" test does not look at the mesh

`tests/Carve.Server.Tests/DefineMeshTests.cs:75-78`. Tests cover the Scope. It asserts only `isError` and the text "unchanged". A `Run` that cleared the store on failure would pass. The `render` caption's counts are available to check.

**Outcome:** fixed

### The bowtie test never reaches the triangulator failure path

`tests/Carve.Core.Tests/MeshValidationTests.cs:63-67`, path at `src/Carve.Core/PolygonMesh.cs:110-111`. Tests cover the Scope. `(0,0),(1,1),(1,0),(0,1)` has Newell area exactly 0, so it stops at the zero-area check and the test passes with lines 110-111 deleted. `(0,0),(2,2),(2,0),(0,1)` reaches the path; the test should also assert the message.

**Outcome:** fixed

### One non-finite or malformed vertex suppresses area and clipping checks on every face

`src/Carve.Core/PolygonMesh.cs:65,102`, `src/Carve.Server/DefineMeshTool.cs:80`. Correctness (T-3: "not the first problem only"). `coordsOk` is global. `{"vertices":[[0,0,0],[1,0,0],[2,0,0],[1e39,0,0]],"faces":[[0,1,2]]}` reports only `vertices[3]`; face 0 uses no bad vertex and is collinear, but its error appears only on the next call. The check should skip only faces touching a bad vertex.

**Outcome:** fixed

### Ear clipping silently omits real vertices of a many-sided face

`src/Carve.Core/Triangulator.cs:68-77`, `:46`. Correctness. The collinear fallback's `|cross| <= 1e-9 * size^2` is relative to the whole face, so short edges fall below it: a regular 4096-gon of radius 1 yields triangles using 4094 of 4096 vertices (8182 of 8192 for n=8192). `MeshReport` counts them as used, and a neighbouring face gets a T-junction. The 1e-9 is a bare literal, not `PolygonMesh.ZeroAreaTolerance`.

**Outcome:** fixed

### Newell vector is not centred, so exactly planar faces far from the origin get a false non-planar warning

`src/Carve.Core/PolygonMesh.cs:126-133`, `src/Carve.Core/MeshReport.cs:95-101`. Correctness. Multiplying by `a + b` puts rounding noise into the normal at large offsets. The exactly planar quad `<65536,65536,-131072> <65537.375,65535.5,-131072.875> <65536.125,65536.875,-131073> <65534.75,65537.375,-131072.125>` warns `0.00939 off its plane (limit 0.000338)`. Of 2000 random planar quads, 779 warned at offset 65536, none at 10000 or below. Subtracting a face vertex first removes it.

**Outcome:** fixed

### Edges shared by more than two faces never report inconsistent winding

`src/Carve.Core/MeshReport.cs:80-88`. Correctness. `c.Forward == 2 || c.Backward == 2` is reached only when `total == 2`. Faces `[0,1,2],[0,1,3],[0,1,4]` all run 0 to 1 and report only `edge is shared by 3 faces`.

**Outcome:** fixed

### Smaller define_mesh input-handling defects

`src/Carve.Server/DefineMeshTool.cs:91-95,103`, `src/Carve.Core/PolygonMesh.cs:83`. Correctness. `"faces":[null]` is reported twice for one problem. `"faces":[[0,"1",2]]` is accepted as index 1 although the schema says integer. `{"vertices":[],"faces":[[0,1,2]]}` gives `index 0 is out of range 0..-1`.

**Outcome:** fixed

### No test covers immutability of the mesh against the caller's arrays

`tests/Carve.Core.Tests/MeshValidationTests.cs` (absent). Tests cover the Scope (T-1: "immutable"). The code is correct, but no test pins it, and T-3's `ModelStore` thread-safety claim rests on it.

**Outcome:** fixed

## Findings: extension API and harness (claude-opus-5-5)

### The L-block, long box and flipped-cube checks all pass on a blank frame

`scenarios/Carve.Scenario.Cube/CubeScenario.cs:62-64,70-71,83,92-93`. Correctness; tests cover the Scope. Every pixel check on these frames asserts background. Run in a scratch copy: with the camera turned 1000 units away for the L-block and long box, `run-scenario.ps1 cube` exited 0 and all three frames were the same blank image. Each frame needs a positive check. This is a tightening, so it needs no tolerance decision.

**Outcome:** fixed

### The guard test misses an extension that reaches the product through Carve.Core

`tests/Carve.Server.Tests/ProductReferencesNoExtensionTests.cs:13,21-22`, `tests/Carve.Server.Tests/ProductReferenceGuard.cs:44-51`. Correctness; tests cover the Scope. Run in a scratch copy: a `ProjectReference` to `Carve.Debug` in `Carve.Core.csproj` built, put `Carve.Debug.dll` beside `carve.dll` and in `carve.deps.json`, and all 13 guard tests passed. Walk the `ProjectReference` closure, or check `carve.deps.json` and the output folder.

**Outcome:** fixed

### A host stop during a scenario exits 0 with the run unfinished

`src/Carve.Server/Program.cs:92-93`, `extensions/Carve.Debug/DebugExtension.cs:56,97-101`, `extensions/Carve.Debug/IScenario.cs:15`. Correctness (T-5: "exiting 0, or 1 on failure"). `RunAsync` is fire-and-forget, and `IScenario.RunAsync` takes no token. A scenario that stops the host then awaits something other than the driver gave script exit 0 with `"outcome": "running"`. An exit of 0 should require a concluded pass.

**Outcome:** fixed

### During ConfigureHost an earlier extension's default beats the command line

`src/Carve.Server/Program.cs:51-60`, `src/Carve.Extensibility/ICarveExtension.cs:13-14`. Correctness (*Headless runs*). Environment and command line return to the top only after all hooks. With `--Empyrean:Time:PaceToFixedStep=true`, a scenario's `ConfigureHost` read `false`. Restore the two sources after each hook.

**Outcome:** fixed

### The script's stdout check has no test and throws away what it caught

`scripts/run-scenario.ps1:77-80,86`, `tests/Carve.Scenario.Tiny/TinyScenario.cs:35`, `tests/Carve.Server.Tests/DebugExtensionTests.cs:198-228`. Tests cover the Scope. Deleting lines 77-80 leaves every test green, and the failure gives a byte count while the `finally` deletes the text.

**Outcome:** fixed

### T-5's choices judged on merit: clearing a run folder is the one risk

`extensions/Carve.Debug/RunFolder.cs:34-42`, `RunLog.cs:96-101`, `DebugExtension.cs:59-60`, `scripts/run-scenario.ps1:63-72,77-80`. Design decisions. Hashes in `run.json`, the 300 s timeout with exit 124, and any stdout failing the script are sound. The scenario name is sound, though `run-scenario.ps1 cube` relies on a case-insensitive filesystem. Clearing a reused folder deletes `frames/` and `user-data/` under whatever `Carve:Debug:RunFolder` names, with no sign a run used it: a mistyped `--Carve:Debug:RunFolder=D:\photos` deletes `D:\photos\frames`. Requiring an existing `run.json` first would prevent that.

**Outcome:** fixed

## Findings: the whole diff (claude-opus-5-5)

### README omits the private feed the entry needs to build

`README.md:22`, `NuGet.config:7,10-13`. Correctness. The prerequisites are the .NET SDK and a Vulkan device, but the `dotnet run` entry restores `Arcodiant.Empyrean*` and `Arcodiant.Vulkan*` only from `http://localhost:5555/v3/index.json`. Elsewhere the entry cannot restore, and the README does not say why. Read from the config, not tested with the feed down.

**Outcome:** fixed

### Retrospective says MetadataLoadContext is still floating

`docs/milestones/M-1-a-cube-you-can-see.md:102`. Tracker hygiene. *Known and left* reports the floating `10.0.*`, which d6af7da pinned (10.0.14).

**Outcome:** fixed

### Test packages float, with literal versions in a new .csproj

`tests/Carve.Server.Tests/Carve.Server.Tests.csproj:9-11`, copied from `tests/Carve.Core.Tests/Carve.Core.Tests.csproj:9-11`. `Microsoft.NET.Test.Sdk` `17.*`, `xunit.v3` `1.*`, `xunit.runner.visualstudio` `3.*` float with no lock file, so `verify` can change with no commit. CLAUDE.md's rule names only the engine version; every other package goes through `Directory.Build.props`.

**Outcome:** fixed

### "Triangulated only on export" and the edge-connected structure have no recorded exception

`CLAUDE.md:44`, `docs/design/D-1-carve-concept.md:95-96`, `src/Carve.Server/MeshBridge.cs:16`, `src/Carve.Core/PolygonMesh.cs:26`. Design decisions. D-1 says an edge-connected structure, triangulated on export; as built it is plain indexed storage, triangulated for every render and for validation. T-1's Notes accept both, but D-1 has no `Exceptions:` entry and CLAUDE.md's sentence no longer matches.

**Outcome:** fixed

### verify's description omits the scenario run and the Vulkan requirement

`CLAUDE.md:63`, `scripts/verify.ps1:3-4`. Tracker hygiene. Both say build plus tests; since T-6 the script also runs `run-scenario.ps1 cube`, so verify needs a Vulkan device.

**Outcome:** fixed

### T-4's As built and RenderQueue's summary say it holds no engine type

`docs/tasks/T-4-shaded-render-tool.md:64`, `src/Carve.Server/RenderQueue.cs:2,7,24-25`. Tracker hygiene. `RenderedFrame(RenderTargetPixels Pixels, ...)` is what `Post` returns. What is true is narrower: the queue needs no engine service, so a tool can resolve it with no device.

**Outcome:** fixed

## Summary

Round 1 of M-1, four reviewers on claude-opus-5-5, at d6af7da. The change is 4.5k lines across kernel, server, extension API, debug extension, scenario and tests, with threading between MCP and the frame thread, a public API and an unattended harness as its risks. So it was split by area into three parts (render and threading; kernel and define_mesh; extension API and harness), plus one pass over the whole diff. The two reports of T-2's As built are one problem, kept once under render and threading, and the reviewers' reports are condensed here.

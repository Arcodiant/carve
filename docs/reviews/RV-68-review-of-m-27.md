---
description: >-
  Review of M-27
---
# RV-68 — Review of M-27

**Status:** Open

**Reviews:** M-27

**Round:** 1

**Previous:** —

**Diff:** 381ff0841882e514221903b9d185f9d9068caa24 against 802e486

**Reviewer:** claude-opus-5-5 (Carve.Hosting and Carve.Server), claude-opus-5-5 (Patina.Graphs and the planks), claude-opus-5-5 (Patina.Server), claude-opus-5-5 (the scenarios and checks), claude-opus-5-5 (the whole diff)

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: Carve.Hosting and Carve.Server (claude-opus-5-5)

I read `git diff origin/main...HEAD` for `src/Carve.Hosting`, `src/Carve.Server`, `tests/Carve.Server.Tests` and `src/Patina.Server/Program.cs` / `SwatchRendering.cs`, and compared `EngineHost.RunAsync` line by line with carve's old `Program.cs`: the configuration order, defaults, logging, hooks, transport, `EngineStatus` registration, engine thread and shutdown all match. I built `tests/Carve.Server.Tests` and ran it: 977 passed.

### FrameLog writes the same frame file from two threads at once with no lock

**Where:** `tests/Carve.Server.Tests/FrameLog.cs:38`, `:58-66`

**What:** `Call` takes the `Writing` lock only for `Append`. `Keep` runs outside it: it checks `File.Exists(path)`, then calls `File.WriteAllBytes(path, png)`. `CarveProcess.ReadResponseAsync` polls stdout every 10 ms in each caller's own task, so overlapping `CallToolAsync` calls finish in parallel on pool threads.

**Failure scenario:** This only happens on a queued run, where `CAIRN_ARTIFACTS` is set. `RenderTests.A_render_after_define_mesh_returned_shows_that_mesh_with_renders_overlapping` sends a burst of `render {}` calls, and their answers carry the same frame. Two continuations reach `Keep` with the same hash in the same poll window. Both see that the file does not exist, and both open `<hash>.png` for writing. On Windows the second open fails with a sharing violation, and the `IOException` reaches the test as a failure. The product is fine, but the `server-regression` run fails, and the frame guard of T-111 and T-128 depends on that run. Taking the lock around `Keep` (or catching `IOException`) closes the gap.

**Severity:** Minor

**Outcome:** fixed: `FrameLog.Keep` now runs under the `Writing` lock (and is `internal`); `FrameLogTests.OneFrameKeptFromManyThreadsAtOnceIsWrittenWithoutACollision` keeps one 4 MB frame from eight threads at once, eight rounds, and threw the sharing-violation `IOException` without the lock (c0d9079)

### Three product commits after T-128 finished, outside any member, and T-128's As built now describes code that no longer exists

**Where:** commits `cabbfad`, `ee8608e`, `01cb4b2` (after `4f8b0ae cairn: finish T-128` and the retrospective); `docs/tasks/T-128-swatch-sphere-lit-as-carve-renders.md:63-65`

**What:** The commits change `src/Carve.Server/ViewFraming.cs`, `PbrMaterials.cs` and `src/Carve.Hosting/RenderLights.cs`. They belong to no task. They come after T-128's `VERIFY PASS` at 439b8b8 and after both frame-guard tables, and no As built records them. T-128's As built still says "`ViewFraming.LightOrientation`, `UpFor` and `VerticalCosine` delegate to it" and "Nothing else in `Carve.Hosting` or `Carve.Server` changed". `ViewFraming.VerticalCosine` is now gone, and `ViewFraming.FrontDirection` now reads `RenderLights.FrontDirection`. T-111's As built says `PbrMaterials` keeps a `Sampler`, and it no longer does.

**Failure scenario:** A reader takes T-128's As built as what was verified and finds a different tree. The commits do keep the values the same (the same vector, the same sampler, a constant that is no longer read), and the build and unit run pass. But the frames of the merged tree were never hashed against the guard. If a later frame difference shows up, nothing in the tracker points at these commits.

**Severity:** Minor

**Outcome:** —

### More of Carve.Hosting is public than any second server reads

**Where:** `src/Carve.Hosting/RenderLights.cs:14`, `:17`, `:20`; `src/Carve.Hosting/Offscreen.cs:22`, `:27`, `:29`; `src/Carve.Hosting/EngineHost.cs:20`, `:33`

**What:** No code outside `Carve.Hosting` reads `RenderLights.AmbientIntensity`, `DirectionalIntensity` or `VerticalCosine`. Since `cabbfad`, `VerticalCosine` is read only inside `RenderLights`. Nothing outside reads `OffscreenTarget.Size`, `Multisampled` or `Depth` either. `LogFilePrefix` is set by neither server. `EngineDefaults` is typed `IReadOnlyDictionary` but holds a mutable `Dictionary`.

**Failure scenario:** The two intensities are `public const`, so a second server that uses them compiles the values in. If carve's light later changes, that server keeps the old numbers until it is rebuilt, and its swatch no longer lights as carve renders. Code that casts `EngineDefaults` to `Dictionary<,>` and writes to it changes the defaults of every later `RunAsync` in that process.

**Severity:** Nit

**Outcome:** fixed: `RenderLights.AmbientIntensity`/`DirectionalIntensity` are `internal const`, `VerticalCosine` `private`; `OffscreenTarget.Size`/`Multisampled`/`Depth` `internal`; the unused `LogFilePrefix` is removed (the log name stays `<temp>/<name>-<pid>.log`); `EngineDefaults` is a `ReadOnlyDictionary`, held by `EngineHostOptionsTests.TheEngineDefaultsCannotBeWrittenThroughACast`, which failed against the mutable `Dictionary`. No value changes (f44e507)

### Test-local `EngineHost` hides `Carve.Hosting.EngineHost`

**Where:** `tests/Carve.Server.Tests/PbrEqualityTests.cs:22`, with the project-wide `<Using Include="Carve.Hosting" />` in `tests/Carve.Server.Tests/Carve.Server.Tests.csproj`

**What:** The test project now imports `Carve.Hosting` everywhere, and `Carve.Hosting` has a public `EngineHost`. `PbrEqualityTests.cs` declares its own `internal static class EngineHost` in `Carve.Server.Tests`, and that one silently wins.

**Failure scenario:** A test written later in that namespace calls `EngineHost.RunAsync(...)`, expecting the shared host. It gets a compile error against the in-process equality host, or, if the names happen to line up, the wrong type. Renaming the test's class would remove the ambiguity.

**Severity:** Nit

**Outcome:** fixed: the test-local host in `PbrEqualityTests.cs` (and its uses in `PbrReleaseTests.cs`) is renamed `InProcessEngine`, so `EngineHost` in the test project means `Carve.Hosting.EngineHost`; a rename, so no test of its own, the build is the check (5e6867d)

## Findings: Patina.Graphs and the planks (claude-opus-5-5)

I read all of `src/Patina.Graphs`, the tests, `tools/Patina.SampleGraphs` and `planks.texgraph` against T-110, T-112 and D-7, and ran `patina-unit`'s graph tests (82 pass). I checked suspected faults with probes in `rv68/probe-graphs/p1` against the built `Patina.Graphs.dll`. Those probes confirmed that banded and whole evaluation give the same planks bytes at 32, 1024 and 2048 (band heights 1, 3, 7, 100 and the default), that numbers round-trip (`1E-07`, `-0`, `5E-324`), and that `DeterministicMath.Pow` comes within 3e-8 of `Math.Pow` with no 8-bit difference.

### Validation throws when a node's input is a node of unknown type

**Where:** `src/Patina.Graphs/GraphValidator.cs:61-62` (and `:188`; `Nodes/LevelsNode.cs:28`, `Nodes/BlendNode.cs:31-32`)

**What:** When a source names a node whose type is unknown, the validator `continue`s without adding a fault and without recording the source. The consumer's `InputsAtFault` therefore stays false. `Kinds` then calls `OutputKind` with that port missing from `inputs`. `LevelsNode` (`inputs["in"]`) and `BlendNode` (`inputs["a"]`/`["b"]`) index the dictionary directly, so the call throws `KeyNotFoundException` out of `GraphValidator.Validate` and `TexGraphFile.Read`. Scope promises the opposite: "a graph with reference faults still loads, so that `open` can hold it and report them". The fault fixture has a mistyped `"a": { "type": "nosie" }`, but nothing consumes it, so no test reaches this path. `Patina.Server` calls `Read` or `Validate` in define, edit, open, get, save, export and swatch.

**Failure scenario:** `"x": { "type": "nosie" }, "l": { "type": "levels", "inputs": { "in": "x" } }, "o": { "type": "output", "inputs": { "in": "l" } }`. `TexGraphFile.Read` throws `KeyNotFoundException: The given key 'in' was not present` where it should return the one fault "node 'x': unknown type 'nosie'". A blend fed by `x` throws the same way on `'a'`. A one-letter typo in a type, made through `define_graph` or `edit_graph`, or present in a file being opened, crashes the tool instead of producing a fault.

**Severity:** Major

**Outcome:** fixed: an input whose source is a node of unknown type now marks its node's inputs at fault (no fault of its own: the unknown type's says why), so `Kinds` never calls `OutputKind` without that port; `GraphFaultTests.AnInputFromANodeOfUnknownTypeIsOnlyThatNodesFault` (levels on `in`, blend on `a`, blend on `b`) gets the one fault "unknown type 'nosie'", and threw `KeyNotFoundException` for levels and blend-`a` without the fix (58957fc)

### An unknown output role adds a false "two outputs with one role" fault

**Where:** `src/Patina.Graphs/GraphValidator.cs:202-207` with `Nodes/OutputNode.cs:197-201` and `Nodes/NodeType.cs:153`

**What:** `OutputNode.Role` reads the role through `NodeParameters`, and that falls back to the default `base_colour` when the value is wrong. `Outputs` then counts the faulty output as a `base_colour` output. The validator's own doc comment says a fault does not cascade, and this one does.

**Failure scenario:** Output `a` has role `base_colour` (or none) and output `b` has `"role": "albedo"`. The probe got two faults on `b/role`. One is correct: "is "albedo", but must be one of ...". The other is false: "base_colour is already the role of output 'a'; a graph has one output per role". An agent that follows the second fault may rename or remove output `a`, which was right all along.

**Severity:** Minor

**Outcome:** fixed: `Outputs` leaves out an output whose `role` is already at fault; `GraphFaultTests.AnUnknownRoleIsNotAlsoCountedAsTheDefaultRole` gets the single `b/role` fault, and got the false second one without the fix (db475d4)

### No built-in node is tested banded against whole

**Where:** `tests/Patina.Graphs.Tests/EvaluationTests.cs:64-102`, `tests/Patina.Graphs.Tests/FirstNodesTests.cs:280-295`

**What:** Only `TestNodes`' blur and hash are compared exactly across band heights. `height_to_normal` is the one built-in node with a radius, and it is never run with a `bandRows` override. The planks are only evaluated at 256, which is one band. The 1024 size-invariance check runs in 256-row bands, but it only compares to within one interpolation step. So a band-edge bug confined to a built-in node (for example a row read as `y` where it should be `Band.Wrap(y, size)`) would pass the suite. My probe shows the code is correct today, so this is a coverage gap, not a defect.

**Failure scenario:** Change `HeightToNormalNode.Evaluate` so that it fills only the rows inside the image and leaves wrapped rows at 0. At 1024 and above the default bands then give different bytes from a whole-image run, and no test fails.

**Severity:** Minor

**Outcome:** fixed: `PlanksGraphTests.ThePlanksGiveTheSameBytesBandedAsWhole` evaluates each of the planks' four outputs at 64 px in bands of 1, 3, 7 and 32 rows and holds them byte-equal to one band; it passes on the real nodes and failed (normal) on a `height_to_normal` that leaves the top row of each band after the first unfilled (c7252fd)

### Write's order follows whatever comparer the caller's dictionary has

**Where:** `src/Patina.Graphs/TextureGraph.cs:12`, `src/Patina.Graphs/TexGraphFile.cs:38`

**What:** `TextureGraph` and `GraphNode` are public records whose constructors take any `ImmutableSortedDictionary`. `Write` lists nodes in that dictionary's own order and does not sort ordinally. A dictionary built without `StringComparer.Ordinal` uses the culture comparer, so the first write is not the canonical text. Only `GraphNode.Of`, `TextureGraph.Empty` and `Read` force ordinal order.

**Failure scenario:** Build `new TextureGraph(256, 0, ImmutableSortedDictionary.Create<string, GraphNode>().Add("a0", …).Add("a_", …))`. `Write` puts `a_` before `a0`, and `Write(Read(Write(g)))` puts `a0` first: the probe gave two different byte strings. No caller in the milestone does this today; `EditGraphTool` passes `Ordinal`.

**Severity:** Nit

**Outcome:** fixed: `TexGraphFile.Write` sorts nodes, inputs and params ordinally itself; `TexGraphFileTests.WriteListsNodesInOrdinalOrderWhateverTheDictionarysComparer` builds a graph with the default (culture) comparer and holds `a0` before `a_`, `b0` before `b_`, and `Write(Read(Write(g))) == Write(g)`; it failed without the fix (2c29053)

### Load-error columns count UTF-8 bytes, not characters

**Where:** `src/Patina.Graphs/TexGraphFile.cs:65`, `:90-96`

**What:** Both the `JsonException` path (`BytePositionInLine`) and the shape path (`Position`) count bytes. Each non-ASCII character before the error on the same line moves the reported column one or more places to the right.

**Failure scenario:** In `… "nodes": { "éé": { "type": 5 } } }` the error is reported at column 69, but the `5` is at character column 67.

**Severity:** Nit

**Outcome:** fixed: both the not-JSON and the shape path now count characters, skipping UTF-8 continuation bytes; `TexGraphFileTests.ALoadErrorColumnCountsCharactersNotBytes` (a shape fault after `"éé"`, and a not-JSON fault on line 2) failed without the fix (07a09a2)

## Findings: Patina.Server (claude-opus-5-5)

I read every file in `src/Patina.Server` and the test names and key bodies in `tests/Patina.Server.Tests` against the T-113/114/115/128 specs, the M-27 retrospective and `tests.json`. `Tier!=regression` passes (93/93). I checked three things against the real `patina.dll` over stdio with a probe script (`C:\Users\andre\AppData\Local\Temp\rv68\probe-server\`): a masked fault, export's lock while it runs, and odd arguments.

### export holds the graph lock for its whole run and ignores cancellation

**Where:** `src/Patina.Server/ExportTool.cs:64` (the `lock (store.Gate)` spans evaluation and encode, lines 64-139; `Run` takes no `CancellationToken`)

**What:** export evaluates and encodes every output while holding `GraphStore.Gate`, and nothing in the loop checks for cancellation. Its own description puts that at minutes at 8192. During that time every other tool waits on the lock, including `get_graph` and a 32 px `swatch`. A cancelled call still runs to the end and writes its files. The graph is an immutable value, and `swatch` already copies it under the lock and evaluates outside it (`SwatchTool.cs:64-69`). export does not. The description tells a client with a short timeout to ask for fewer outputs, but says nothing about the session blocking.

**Failure scenario:** Probed: open the planks, call `export {"folder":"big","size":4096,"outputs":["base_colour"]}`, send `notifications/cancelled` for it 0.5 s later, then call `get_graph {}` and `swatch {"view":"flat","size":32}`. Both answered only at 30.3 s, and `big/base_colour.png` was written anyway. At 8192 with four outputs (154.7 s as built), a client that times out at 60 s and retries queues a second full export behind the first. The server answers nothing else for about 5 minutes and overwrites the files twice.

**Severity:** Major

**Outcome:** —

### edit_graph lets a hidden fault through, then blames the fix for it

**Where:** `src/Patina.Server/EditGraphTool.cs:94-98` (with `GraphValidator`'s no-cascade rule, `src/Patina.Graphs/GraphValidator.cs:11` and `Kinds`)

**What:** edit_graph treats a fault as new when its place is not among the faults the graph shows before the edit. But the validator does not kind-check a node whose inputs are at fault, or anything downstream of it. In an opened graph with a reference fault (T-117's acceptance plant), an edit can add a greyscale/colour mismatch downstream and edit_graph reports success. The later edit that fixes the reference fault uncovers the mismatch and is refused as leaving a "new fault" on a node it never touched. The description promises that nothing changes when a list "would leave a fault the graph did not have", and the agent was told the earlier edit left only the old fault.

**Failure scenario:** Probed: `define_graph` with `base` = blend(a: `c` colour, b: `zzz`) feeding a base_colour output gives 1 fault, at `base` input `b`. Then `edit_graph [set_node h height_to_normal {height: base}, set_node nout output {in: h, role: normal}]` returns OK, still "1 fault". Then `edit_graph [connect base b from n]` returns `nothing changed: the operations would leave 1 new fault: node 'h' input 'height': takes greyscale, but 'base' gives colour`.

**Severity:** Minor

**Outcome:** —

### patina-regression's inputs omit the sample graph its 8192 test reads

**Where:** `tests.json:49-61` (patina-regression), test at `tests/Patina.Server.Tests/ExportBoundedMemoryTests.cs:55`

**What:** `ExportBoundedMemoryTests` opens `samples/graphs/planks.texgraph` and holds the 2 GB bound on it. patina-regression's inputs are only the test project's dotnet closure and `ProductReferenceGuard.cs`, and the closure does not reach `samples/graphs/**`. patina-unit and scenario-patina both list it. Separately, scenario-patina lists `samples/graphs/**` twice (`tests.json:551` and `:554`).

**Failure scenario:** Someone changes `planks.texgraph` (a heavier graph, more octaves). `cairn` carries patina-regression's earlier pass, so the memory bound, which T-115 says is a stop and not a raised bound, is never measured on the new graph.

**Severity:** Minor

**Outcome:** —

### An export folder with a character a path may not hold gives a bare SDK error

**Where:** `src/Patina.Server/ExportTool.cs:96`

**What:** `Path.GetFullPath(folderText, ...)` runs outside the `try` that turns path failures into a named error. export has no check like `ToolSupport.Resolve`'s invalid-character check, which `save` and `open` use. The `ArgumentException` escapes the tool.

**Failure scenario:** Probed: `export {"folder":"a\u0000b"}` returns `An error occurred invoking 'export'.` It does not name the folder or the reason, as the Scope and the description promise.

**Severity:** Nit

**Outcome:** —

### swatch takes an angle that overflows to infinity

**Where:** `src/Patina.Server/SwatchTool.cs:91-93`

**What:** the angle is checked as a finite double, then cast to `float`. A value above about 3.4e38 becomes `+Infinity`, and the sphere is turned by a NaN quaternion.

**Failure scenario:** Probed: `swatch {"view":"sphere","angle":1e300}` is answered OK with the caption `... angle Infinity, 512 x 512 px` instead of being refused. I did not open the image.

**Severity:** Nit

**Outcome:** —

### Stale test name and a stale As built line about the tool lists

**Where:** `tests/Patina.Server.Tests/GraphToolTests.cs:16`; `docs/tasks/T-115-png-export-at-a-given-size.md` (As built, *Tests* paragraph)

**What:** the test `Tools_list_gives_the_seven_tools_in_order...` now asserts nine tools. T-115's As built still says the tool-list asserts "compare the first seven names and then `Contains("export")`". T-114's rebase made them exact, as the milestone retrospective records.

**Failure scenario:** A reader of T-115, or of the test list, believes the tool-list check is loose and that patina has seven tools.

**Severity:** Nit

**Outcome:** —

## Findings: the scenarios and checks (claude-opus-5-5)

I read `scenarios/Carve.Scenario.Patina` (all stages, `SwatchSphere.cs`), `scenarios/Carve.Scenario.Planks`, `tests/Carve.PlanksLoad.Tests`, both scripts, the M-27 `tests.json` entries, and T-113..T-116 and T-128. I measured how far the break and tolerance checks can move with numpy, using `samples/graphs/planks/*.png` and `planks.texgraph`. I built nothing and wrote nothing into the worktree. The approved values (dark 66, band 80..128, highlight 40, label band 22, slate 0.1 %) match the code.

### The 1024 export check cannot fail on a shifted, mirrored or flattened picture

**Where:** `scenarios/Carve.Scenario.Patina/ExportStage.cs:49` (and `tests/Patina.Server.Tests/ExportToolTests.cs:83`, at 40)

**What:** T-115's "the same picture at four times the resolution, by sampling at matching uv" is a mean absolute difference of the green channel below 25. On the planks that bound is far wider than any likely uv fault. I measured each against the 256 sample. A half-tile roll of base_colour differs by 8.4, a u-mirror by 5.0, and a one-stave shift by 8.8. For occlusion, every roll and mirror differs by 0.0 (it is a u-periodic bevel), and even a uniform grey at its mean differs by only 21.9. Of the faults I tried, only a flat base colour (27.0) fails. The unit test's bound of 40 is looser still.

**Failure scenario:** export at 1024 samples uv shifted by half a tile, mirrored in u, or writes occlusion as one flat tone. Both 1024 checks pass. Only the 256 export is pinned, byte for byte, to the samples.

**Severity:** Major

**Outcome:** —

### `tiled-shift` on the sheet is caught by the rolled labels, not by the shift; the real fault is invisible in the normal and occlusion columns

**Where:** `scenarios/Carve.Scenario.Patina/SwatchStage.cs:190-194`

**What:** the tamper rolls the whole tiled cell, label included. The label rows (cell rows 0..21, which the check leaves out) land in rows 213..234, which the check reads. So every tiled cell fails whatever its texels. In `planks.texgraph`, `normal` and `occlusion` come only from `bevel` (a u gradient repeating 8 times, constant in v). A true half-tile shift (128 texels, four periods) leaves those images unchanged: both differ by 0.0 under the roll. The As built says the break is "caught by all 8 sheet tiled cells". In fact the four normal and occlusion cells fail only because of the moved label and the 43 px against 42.67 rounding. The base_colour and roughness cells and the 1:1 frames do catch the fault, so the product is still covered, but the break table claims more than it proves.

**Failure scenario:** a product regression shifts the sheet's tiled cells by half a tile. The normal and occlusion tiled cells pass *exact*, and the break run reports them as catching it.

**Severity:** Minor

**Outcome:** —

### The planks `mirror` break mirrors the camera, not u on the side

**Where:** `scenarios/Carve.Scenario.Planks/PlanksScenario.cs:106`

**What:** T-116's Scope names the break "u mirrored on the side". `CARVE_PLANKS_BREAK=mirror` instead negates the X of the prediction's front camera direction. That moves the whole predicted silhouette and the faces in view, so it fails on the first check it reaches ("rune not where predicted"), whatever the checks would make of a real u-mirror. It does not show that the tone, gap or rune checks catch textures mirrored on the staves.

**Failure scenario:** `project_uvs` cylindrical starts writing u as 1 - u. Nothing in the break set shows the scenario fails. The stave texture is mirror-symmetric per stave (the bevel is a triangle), so only the tone order and the rune's emissive placement could catch it, and neither has been shown to.

**Severity:** Minor

**Outcome:** —

### `scenario-patina` is a `gpu` suite with no `requires: ["vulkan"]`

**Where:** `tests.json:540-557`

**What:** every other `gpu` suite (all 16) declares `"requires": ["vulkan"]`. `scenario-patina` renders the sphere and sheet through `OffscreenTarget` but declares no requirement, so it can be dispatched to a runner without Vulkan. There it fails instead of being placed elsewhere.

**Failure scenario:** verify places `scenario-patina` on a GPU-class runner without Vulkan. The swatch sphere and sheet calls are refused with the engine fault, and the suite fails for the runner, not the code.

**Severity:** Minor

**Outcome:** —

### `patina-regression`'s inputs miss `samples/graphs/**`, which its test reads

**Where:** `tests.json:49-60`; `tests/Patina.Server.Tests/ExportBoundedMemoryTests.cs:55`

**What:** `ExportBoundedMemoryTests` (Tier regression, the bulk of this suite's 200 s) opens `samples/graphs/planks.texgraph` and exports it at 8192 within a memory bound. The suite lists only the test project's closure and `ProductReferenceGuard.cs`. `patina-unit` and `scenario-patina` do list `samples/graphs/**`.

**Failure scenario:** a change to `planks.texgraph` (more nodes, a bigger size) raises the 8192 export's peak working set past the bound. `patina-regression` is carried as passed, because its inputs did not change.

**Severity:** Minor

**Outcome:** —

### Scenario Patina references the product library `Patina.Graphs`, outside "Writing a scenario", unrecorded

**Where:** `scenarios/Carve.Scenario.Patina/Carve.Scenario.Patina.csproj:14`; `CLAUDE.md:205`

**What:** CLAUDE.md gives a scenario project references "to `Carve.Debug` and `Carve.Extensibility` only". T-114 added a project reference to `src/Patina.Graphs`. The swatch checks evaluate the graph through the same `GraphEvaluator` and `OutputImage.Rgba8` that `SwatchTool` calls (`src/Patina.Server/SwatchTool.cs:110,121`). T-114's As built does not record this as a deviation, and nobody put it to the person. As a result, flat and tiled *exact* check only the composition: an evaluation fault shared by both sides passes. At 256 the planks are separately pinned by `ExportStage`'s byte equality with the samples. At 64 and 128 (sheet 64, tiled 128) nothing else holds them.

**Failure scenario:** a size-dependent bug in `OutputImage.Rgba8` or the evaluator at 64 or 128 px. `swatch-sheet-64` and `swatch-tiled-base-128` still pass *exact*, since prediction and product compute the same wrong texels.

**Severity:** Minor

**Outcome:** —

### The branch is not on `origin/main` 6ae5a22, so its `tests.json` is set against an older main

**Where:** `tests.json` (whole file); merge base `802e486`

**What:** the review was briefed as "rebased onto `origin/main` at `6ae5a22`". `git merge-base HEAD 6ae5a22` is `802e486`: 91 of main's commits (M-21's merge and its RV-64..67 answers) are not under M-27. Against 6ae5a22, M-27's `tests.json` drops `scenario-morph` and core-unit's `samples/*.carve`, `Fixtures/*.carve`, `ArmPlan.cs`, `NotPlanarFold.cs` and `CameraPixels.cs` inputs. These are main's later additions, not M-27 removals, but they show the suite file needs a real merge. Any reviewer diffing against `origin/main` sees them as M-27 changes.

**Failure scenario:** finish rebases and resolves `tests.json` by taking M-27's side of a hunk. `scenario-morph`, or core-unit's inputs from RV-65 finding 3, are lost, and stale carried passes follow.

**Severity:** Minor

**Outcome:** —

### `samples/graphs/**` is listed twice in `scenario-patina`'s inputs

**Where:** `tests.json:551,554`

**What:** the same glob appears at both lines, one from T-114 and one from T-115's addition. It is harmless but noise.

**Failure scenario:** none at run time; a reader takes one of the two for a different path.

**Severity:** Nit

**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

I read `git diff origin/main...HEAD` (48 commits) for the seams between members: `tests.json` suite by suite (a script over every suite's tier, resources, requires and duplicate inputs), `Carve.slnx`, CLAUDE.md's rules and Layout against the tree, the README's patina section, the slot names in export, `set_material` and `barrel-planks.carve`, the host API and the extension loader, GraphStore and the planning decisions, and the member files' headers and As built. I built nothing and ran no tests.

### scenario-patina draws on the GPU but is the only gpu suite without `requires: ["vulkan"]`; it also lists one input twice

**Where:** `tests.json:539-558` (suite `scenario-patina`)

**What:** T-113 recorded that `scenario-patina` was `cpu` only until something drew, and that "T-114, the first to render, must move `scenario-patina` ... to `gpu`" (`docs/tasks/T-113-the-patina-server-and-its-graph-tools.md:97-101`). T-114 set `"resources": "gpu"` but added no `requires`. All 16 other gpu suites declare `"requires": ["vulkan"]`, `scenario-planks` included. The suite now draws the sphere and the sheet in `SwatchStage.cs`, and they fail with no Vulkan device (`PatinaHarness.NoEngine`, and README: "patina needs a Vulkan device to draw the sphere and the sheet"). Its `inputs` also list `"samples/graphs/**"` twice.

**Failure scenario:** a runner with a GPU slot that does not publish `vulkan` can take `scenario-patina`, because nothing in the suite asks for the capability. `SwatchStage` then fails on its first sphere call, where every other GPU suite would never have been sent to that runner. The gate reports a red suite where it should have routed or skipped it.

**Severity:** Minor

**Outcome:** —

### CLAUDE.md's Layout stops at T-113 for patina's scenario and tests, and omits what T-128 moved into Carve.Hosting

**Where:** `CLAUDE.md:68` (`src/Carve.Hosting`), `CLAUDE.md:85` (`scenarios/Carve.Scenario.Patina`), `CLAUDE.md:97` (`tests/Patina.Server.Tests`), `CLAUDE.md` `samples/` row

**What:** Planning says CLAUDE.md's Layout "take[s] additions from every task". Four rows lag the code:
- The `Carve.Hosting` row lists `RenderSky`, `TextureUpload` and the offscreen types, but not `RenderLights`. T-128 moved that type there (`src/Carve.Hosting/RenderLights.cs`). Both servers now light through it, and `ViewFraming` reads its `FrontDirection` (01cb4b2).
- The `Carve.Scenario.Patina` row names only `GraphStage.cs (T-113)`. It has no `SwatchStage.cs` (T-114/T-128: the sphere checks, the four breaks moved there from the product), `SwatchSphere.cs` or `ExportStage.cs` (T-115).
- The `Patina.Server.Tests` row says "one `regression` class (suite `patina-regression`) starting the real `patina`". `ExportBoundedMemoryTests` (T-115, `Tier=regression`, the 8192 test that is most of the suite's time, T-129) is a second regression class, and it starts no process.
- The `samples/` row does not mention `samples/barrel-planks.carve` (T-116) or `samples/graphs/` (T-112).

**Failure scenario:** a later agent follows the Layout to "add a stage with one line in `PatinaStages.cs`" and reads that the scenario only exercises graph tools. It also takes `patina-regression` to be one process class, for example when doing T-129's move of the 8192 test, and does not find `RenderLights` where the Layout says carve's look code lives.

**Severity:** Minor

**Outcome:** —

### Carve.Scenario.Patina references Patina.Graphs, against CLAUDE.md's scenario rule, with no ruling recorded

**Where:** `scenarios/Carve.Scenario.Patina/Carve.Scenario.Patina.csproj` (ProjectReference `../../src/Patina.Graphs/Patina.Graphs.csproj`); `CLAUDE.md`, *Writing a scenario* ("project references to `Carve.Debug` and `Carve.Extensibility` only")

**What:** Every other scenario keeps to the two references. This one also references the product library `Patina.Graphs` and predicts the swatch texels with it (`SwatchStage.cs:72-73`, `Readback`: `TexGraphFile.Read`, `new GraphEvaluator`, `OutputImage.Rgba8`). `ExtensionLoader` loads extensions into `AssemblyLoadContext.Default` and falls back to their resolvers (`src/Carve.Hosting/ExtensionLoader.cs:53-70`). So the scenario runs the `Patina.Graphs` already loaded in patina's process, the very evaluator whose output it checks. T-113 and T-114 record no ruling on the exception, and CLAUDE.md was not updated.

**Failure scenario:** a defect in a node's evaluation, for example a wrong `levels` clamp, changes patina's flat, tiled and sphere output and the scenario's prediction identically. `scenario-patina` stays green, and only the unit tests' hand-worked numbers can catch it. Separately, the rule as written now disagrees with the tree, so the next scenario author cannot tell whether a product-library reference is allowed.

**Severity:** Minor

**Outcome:** —

## Summary

Five reviewers, all Opus 5.5, side by side, after the rebase onto `main` at 802e486 and the refactor pass. The diff (101 files outside `docs/`, about 7.9k lines) was split by area, since the members build separate pieces but T-111's host library and T-128's lights run under all of them: `Carve.Hosting` and `Carve.Server` (T-111, T-128); `Patina.Graphs` and the planks (T-110, T-112); `Patina.Server` (T-113 to T-115, T-128); the scenarios and checks (T-113 to T-116, T-128); and one pass over the whole diff for what falls between them.

26 findings: 0 Blocker, 3 Major, 15 Minor, 8 Nit. The Majors: `TexGraphFile.Read` and `GraphValidator.Validate` throw on an input from a node of unknown type; `export` holds the graph lock for minutes and ignores cancellation; scenario Patina's 1024 export check passes a half-tile shift, a u-mirror and a flat occlusion map.

`main` moved after this round's rebase (M-21 landed, 91 commits). The milestone is rebased again before the answers, so the next round reviews the answers and that rebase together.

# Carve

A 3D modelling tool for Claude agents, served over **MCP**. An agent defines and
edits meshes by coordinates or with tools (cut, extrude, move a vertex, edge or
face), looks at the result (screenshots, and render modes such as UVs or
normals), runs queries ("select all unattached vertices", "select all faces
above y=10"), and exports models that games on the **Empyrean** engine import as
content. The concept, decisions and open questions are in **D-1**.

## Planning rules

Every plan, for a milestone or a task, keeps these:

- **Content comes with its mechanic, as a priority.** Content (here: the sample
  models a tool is shown on, its render-mode looks, its tool descriptions and
  the worked examples agents learn from) is built in the same milestone and the
  same task as the mechanic or feature it belongs to, so each milestone's
  mechanics arrive showing something. Placeholder content may stand in where the
  real thing is not ready; absent content may not. Content is never a milestone
  of its own.
- **A thin slice first.** One small, complete slice of the whole is built end to
  end first; wider content and secondary systems come after it. No polish-only
  milestone while the core has a gap.
- **Every milestone is reachable and adds something.** Its result is reachable
  from a plain launch, through the product's own interface (MCP tools, called
  from an ordinary client) and never a debug flag, and adds something that can
  be used or looked at that could not be before.
- **Size by feedback.** A task is one to three commits; a milestone roughly four
  to eight tasks. A point mid-milestone where something could be tried and
  learned from is a milestone boundary.

## Architecture rules

- **`Carve.Core` never references the engine**, `System.Random` or the wall
  clock. It owns meshes, edit operations and queries, so all of it is testable
  with no GPU. `CoreIsEngineFreeTests` enforces the engine half.
- **`Carve.Server` is presentation and transport**: the MCP surface, rendering,
  export. If it is deciding what an edit does to a mesh, that belongs in the
  Core.
- **`Patina.Graphs` is engine-free too**: no engine or `Arcodiant.*` package, no
  `System.Random`, no wall clock; only `StbImageWriteSharp` for PNGs. Texture
  graphs evaluate the same on patina and in Carve (D-7), so evaluation is
  deterministic and tiled. `PatinaGraphsIsEngineFreeTests` enforces it. Node
  types are registered in `NodeRegistry`, never switched on.
- **Bakes are computed in `Carve.Core`** (`Carve.Core.Baking`, T-120), deterministically and at rest, and are never
  stored in the Carve file.
- **Stdout belongs to MCP.** On the stdio transport nothing but protocol goes to
  stdout; logs go to stderr or the temp folder.

- **Renders are Empyrean's**, so a screenshot shows the model as the game would:
  the model's own colours are drawn with the engine's PBR material, made with the
  parameters the loader makes from the exported `.glb`, so `render` and
  `render_glb` agree in shading (D-1, D-6, T-82). A posed mesh, flat or smooth, is
  lit by its skinned corner normals, as the game lights the exported file, and
  `render_glb` with no clip skins a skinned file at its bind pose (D-6, RV-52). The
  other looks keep their own materials.
- **The mesh is polygons**, triangles are a derived view for render and export, never stored. Work persists in a native
  Carve file; `.glb` is what reaches a game.
- **The product has no window.** Every look is an offscreen render.
- **The Carve file is versioned JSON geometry**, read back bit-exact, each model written in the lowest version that holds it (2; 3 shading; 4 holes and tombstones; 5 UVs; 6 materials; 8 morph targets, sparse deltas by vertex id, T-101; 7 the part tree and kept levels, `parts` after `meshes` and `levels` in a mesh, T-92; 9 a part tree beside morph targets, version 8 with version 7's `parts` and `levels`, a level keeping its targets, T-132), so a model without the newer things is written byte for byte as before; the `.glb` export takes its normals from `Shading` (a flat mesh's are its faces', a smooth one's are per corner, D-6, T-42) and is written with SharpGLTF (D-1, M-3).

The rest are decided in D-1 as they settle; add them here when they do.

## Layout

| Path | What |
|---|---|
| `src/Carve.Core` | Engine-free modelling kernel |
| `src/Carve.Extensibility` | The public extension API: `ICarveExtension`, `CarveHostBuilderContext` |
| `src/Carve.Hosting` | The engine host both MCP servers run through (T-111): `EngineHost.RunAsync` (configuration layering, logs to stderr and the temp folder, extension loading and hooks, the stdio transport, the engine thread and `EngineStatus`), the fixed sky (`RenderSky`), carve's lights (`RenderLights`, T-128: the ambient light, the sky and the directional light, whose `FrontDirection` carve's `ViewFraming` reads, so patina's `swatch` sphere is lit as carve's `render` is), the texture upload rule (`TextureUpload`) and the offscreen render plumbing (`OffscreenTarget`, `OffscreenFrame`, `OffscreenQueue`, `OffscreenRequest`); references the extension API and the engine only, no extension and no Carve code |
| `src/Carve.Server` | The shipped product, the `carve` MCP server: its tools, its looks (`RenderComposition`, `RenderSystem`, `RenderPump`), `ModelStore` and export; `Program.cs` is one `EngineHost.RunAsync` call with carve's name and registrations |
| `src/Patina.Graphs` | Engine-free texture graphs (D-7, T-110): the `.texgraph` format read back bit-exact (versions 1 and 2, written in the lowest that holds the graph: `TexGraphFormat`'s fields and node types, T-134; list parameters `Floats` and `Colours` stay version 1), validation into `GraphFault`s naming node and port, the node registry (`Nodes/`, one file per type, each naming by `InputRect` the rectangle of each input it reads, or, for an input whose rectangle depends on another's values, by `DependsOn` and `InputRects` once that input is made, T-153: `warp`), tiled deterministic evaluation under a capped tile cache (`Evaluation/`, T-133) and the image pieces (`Tile`, `TileRead`, `TexelRect`), output packing and PNGs (`Imaging/`: the roles `base_colour, normal, roughness, metallic, occlusion, height` in role order, then named outputs a to z; metallic packed into `roughness.png`'s B, T-134); `tiles`, `stripes` and `shape` (T-136: patterns that carry their own direction, worked from the texel alone) |
| `src/Patina.Server` | `patina` (T-113), the texture-graph MCP server, hosted through `Carve.Hosting` with its own name and tools and loading extensions as `carve` does: one open graph a session in `GraphStore` (its path, and the snapshot stack, never saved); tools `define_graph`, `edit_graph`, `get_graph`, `save`, `open`, `push_snapshot`, `pop_snapshot`, `export` (T-115: the open graph's outputs as PNGs at a size the agent names), `swatch` (T-114: the sheet, a column per output and a label atlas of 32 rows, flat, tiled 3 x 3 and the sphere, wearing metallic since T-134), listed in `Program.Tools` in `Program.cs` (a later tool adds one line there); faults are `Patina.Graphs`' as they stand; `NodeCatalogue` writes define_graph's list of node types from the registry |
| `extensions/Carve.Debug` | The debug extension: in-memory transport, `ScenarioDriver`, fixed clock and seed, run folder. Referenced by nothing in `src/` |
| `scenarios/Carve.Scenario.<Name>` | One project per scenario, run by `run-scenario.ps1`; `Cube` is the first: a cube, an L-block, a long box, both views, four cube frames by direction and zoom, and the sizes, orthographic and axis-view frames; `Cube` links the Crate's `CrateBuild.cs` for the front-view check `CheckFrontOrder`, which the crate and export frames share |
| `scenarios/Carve.Scenario.Crate` | The crate: one cube built into a crate with `move` and `extrude` only, five views, undo and error paths, then the wireframe, normals and highlight frames (`CrateLooks.cs`, projecting points with `CameraPixels.cs`) and the labelled frames and contact sheets (`CrateLabels.cs`), the checker look (`CrateChecker.cs`, `CheckerColours.cs`) and the UV layout look laid with `project_uvs box` (`CrateUvLayout.cs`, every pixel worked from the UVs by `UvLayoutPixels.cs`); its colour rule (`ColourRule.cs`: T-82's PBR light, the measured sky and the specular) is linked by the Shed, Cuts, Gatehouse, Uvs, Character, Soft, Orcward and Sky scenarios; suite `scenario-crate` |
| `scenarios/Carve.Scenario.Shed` | The leaky shed: `samples/leaky-shed.carve` opened, `check` and `select` finding every planted fault, a selection moved, and the shed in every look, size and an axes sheet; each pixel is worked from the mesh by a ray test (`ShedGeometry.cs`); suite `scenario-shed` |
| `scenarios/Carve.Scenario.Export` | The crate saved, reopened and exported into a game content folder in the run folder, then shown by the viewer; suite `scenario-export`, run by `scripts/run-export-scenario.ps1` with the checks in `tests/Carve.ExportViewer.Tests` |
| `scenarios/Carve.Scenario.Character` | The gardener, `samples/gardener.carve`, built through the tools from `GardenerSpec.cs` (two meshes, seven colours, 22 Synty-named bones and a can bone, clips `Idle` and `Walk`), seen at rest, in a pose and in each clip at three times with every pixel worked from the geometry (`GardenerPixels.cs`, which the Soft and Orcward scenarios link too), saved, reopened and exported as a model, as clips and the hat as a piece (`models/gardener.hat.glb`), then weighed by `weigh` and compared with its hand weights (`weigh-agreement.txt`); suite `scenario-character`, run by `scripts/run-character-scenario.ps1` with the checks in `tests/Carve.CharacterLoad.Tests` (the content folder read through `IModelGeometryLoader.Load`, `HumanoidRoleMap.Synty`, and the sample compared by content) |
| `scenarios/Carve.Scenario.Soft` | The soft gardener: `GardenerSpec.cs` and `GardenerPixels.cs` linked from the Character scenario (and `CrateBuild.cs` and `CameraPixels.cs` from the Crate), built flat and shown, the hat's brim creased with `mark_edges`, both meshes subdivided one level and smoothed, then shown again at rest, in a clip and close on the hat (with the brim uncreased for one frame), saved, reopened and exported flat and soft; suite `scenario-soft`, run by `scripts/run-soft-scenario.ps1` with the checks in `tests/Carve.SoftLoad.Tests` (triangle count and corner normals read back through `IModelGeometryLoader.Load`, and `samples/gardener-soft.carve` compared by content) |
| `scenarios/Carve.Scenario.Orcward` | Orcward's *Done when* in one run: the gardener (`GardenerSpec.cs` and `GardenerPixels.cs` linked from the Character scenario, `CrateBuild.cs`, `CameraPixels.cs` and `ColourRule.cs` from the Crate) exported as a model, as clips and the hat as a piece, then every `render_glb` frame of the exported file compared with `render` at rest, at three times of Idle and five of Walk (between keys included) under D-6's rule (a pixel differs when its luma differs past 4 of 255, a frame fails past 0.5 % of its pixels or with a connected patch of differing pixels past 50), each frame with a positive pixel check, and colour checks against the lit palette (a thigh face as trousers, a hat-band face as band, the can and the near hand), since luma cannot tell trousers from band; then shown failing: a swapped bone order, swapped inverse binds, reversed winding, swapped colours (trousers and band, caught by the colour checks), a dropped piece and a twisted hand (`GlbEdit.cs` rewrites the glb), the measured table in `differences.tsv` (with a `colour` column), a triptych of each pair in `compare/`; suite `scenario-orcward`, run by `scripts/run-orcward-scenario.ps1` with the checks in `tests/Carve.OrcwardLoad.Tests` |
| `scenarios/Carve.Scenario.Acceptance` | T-49's frame step: a fresh agent's round folder (`CARVE_ACCEPTANCE_RUN`: `character.carve` and `game/Content/models/character.glb`), the saved model drawn with `render` and the exported file with `render_glb` at rest and three times of each clip from the front and the side, each pair measured under Orcward's frame rule (`ComparisonPng.cs` linked from the Orcward scenario), a front-against-back control that must fail, and each rest front and side frame of both draws held to a silhouette predicted from `get_mesh` (`Silhouette.cs`, IoU 0.9 or more, so a camera fault both draws share fails); writes `acceptance/differences.tsv`, `silhouettes.tsv` and `compare/` into the round folder. No suite of its own: it runs only on an acceptance run's folder, by `scripts/run-acceptance-check.ps1`, and is a `dotnet-closure` input of `server-unit`, whose build compiles it |
| `scenarios/Carve.Scenario.Sky` | What M-7's fixed sky adds to a colour face (M-19): one quad facing each of 26 world normals, drawn grey, black and white from `front`, `back` and straight on; writes `sky.tsv`, the measurements `ColourRule.SkyLight` and `SkySpecular` were fitted to, and checks every pixel against `ColourRule`'s model (linked from the Crate; the expected colour is `ColourRule.Lit`); suite `scenario-sky` |
| `scenarios/Carve.Scenario.Rig` | The rigged gardener: `GardenerSpec.cs` linked from the Character scenario (and `CrateBuild.cs`, `CameraPixels.cs` from the Crate), hand-weighted with three planted faults (an unweighted torso patch, a hat vertex on the left shoulder, Elbow_L out of place), each found with `get_weights` or the `bones` and `weights` looks and fixed through the tools with the frame taken again; `export` refused while the patch remains and made once it is gone; last the fault `weigh` makes itself (torso vertices following the arm) fixed by a `set_weights` group override that a plain `weigh` keeps; suite `scenario-rig`, run by `scripts/run-scenario.ps1 rig` |
| `scenarios/Carve.Scenario.Cuts` | The cuts: one stage per cut, each a class in its own file starting from its own `define_mesh`, run in the order of `CutsStages.cs` (a later cut adds its file and one line there): `HolesStage.cs`, the holed plate, shaded with and without its inner face and in the wireframe; `DeleteStage.cs`, `delete`; `MergeBridgeStage.cs`, `merge_faces` and `bridge`; `PlaneCutStage.cs`, `plane_cut`; `LoopCutStage.cs`, `loop_cut` and the band extruded; `KnifeStage.cs`, `knife` and the slab's recess. Every pixel is worked from the geometry by a ray test that knows holes (`CutsGeometry.cs`, with `SeenNarrow` for a face a few pixels wide and the in-sight counts `CutsCounts`), with the checks the stages share in `CutsChecks.cs`, the shed's checks and camera linked, and the Crate's `ColourRule.cs` predicting a face from one of the same normal; suite `scenario-cuts` |
| `scenarios/Carve.Scenario.Parts` | The parts (M-9): the agreed rover built as a part tree through the tools alone, blockout first (`define_part`, `define_mesh`, `set_colour`, `keep_level 0`) then refined (the 16-sided wheel, the hub boss, the windscreen and the lens by `extrude`, `keep_level 1`), seen in 13 frames (`PartsScenario.cs`): level 0, the sheet, live, back left, top, the mast turned, the front axle alone, one wheel alone with its face ids (beside the helper frame `part-wheel-plain`, the same view without them) and at level 0, reopened after `save` and `open`, and one `move` on the hub boss seen in all four wheels, then `undo` (the helper frame `instance-undone`, byte-equal to the top frame). Every pixel is worked from the geometry `get_mesh` gives placed by `RoverSpec.cs`'s tree (`PartsWorld.cs`: the Cuts ray test, the shed's camera, the Crate's `ColourRule.cs`, all linked); `check` and `get_parts` are judged against the spec and `CARVE_PARTS_BREAK` (`mirror`, `no-instance-rotation`, `levels-ignored`, `part-not-isolated`) makes it wrong on purpose; after the reopen it exports the saved rover as `game/Content/models/rover.glb` in the run folder (T-97); suite `scenario-parts`, run by `scripts/run-parts-scenario.ps1` |
| `scenarios/Carve.Scenario.Gatehouse` | The gatehouse, `samples/gatehouse.carve`: a slab cut into a wall in seven stages through the tools (loop_cut and extrude, merge_faces, plane_cut, knife, delete and bridge), each seen from two directions, three in the wireframe with the new edges highlighted, and the last straight on, labelled and as a six-view sheet; every pixel is worked from the geometry (`GatehousePlan.cs`, with the shed's checks and camera, the cuts' ray test and `CutsChecks.cs`, and the Crate's `ColourRule.cs` linked, and `GatehouseGeometry` in `tests/Carve.Core.Tests` working the counts first); saved, reopened and compared, and exported; suite `scenario-gatehouse`, run by `scripts/run-gatehouse-scenario.ps1` with the checks in `tests/Carve.GatehouseLoad.Tests` (live triangles and the door's material read back through `IModelGeometryLoader.Load`, and the sample compared by content) |
| `scenarios/Carve.Scenario.Patina` | patina's scenario, run on the real `patina` (`run-scenario.ps1 patina -Server patina`, suite `scenario-patina`): one stage per file in the order of `PatinaStages.cs` (a later task adds its file and one line there). `GraphStage.cs` (T-113): define, edit, read back, push, a breaking edit refused, pop byte for byte, save, open and compare by content, and a planted bad input id opened, named and fixed; `ExportStage.cs` (T-115): the planks exported at 256 and compared byte for byte with `samples/graphs/planks/*.png`, the slots named, exported at 1024 and compared under `ExportMatch.cs`'s rule (RV-68: per texel and channel within 4 levels of the mean of the four 1024 texels at its centre; linked by `tests/Patina.Server.Tests`), the rule shown failing a half-tile shift, a u-mirror and a flat map; the normal under its own rule (a texel whose largest step to its 8 neighbours in the 256 export is 4 or less within 4, elsewhere within 32), shown failing a u-mirror, a flat map and shifts in u of a quarter stave, one texel and half a texel; and the refusals; `SwatchStage.cs` (T-114, T-128): the planks built through the tools, flat, tiled, three spheres and two sheets each checked from texels read from `samples/graphs/planks/` at 256 and from patina's own `export` at 64 and 128 (the scenario references no product library, RV-68), the sphere's pixels worked from its geometry by `SwatchSphere.cs` (the engine's UV sphere restated, each pixel traced to its triangle), a seam graph, the highlight, the metal plate's sheet and sphere from its exported texels and the metallic rule (T-134), then the breaks made on the scenario side (`swatch-breaks.txt`) |
| `scenarios/Carve.Scenario.Uvs` | The UVs: the barrel (`BarrelSpec` in `UvsScenario.cs`: `define_mesh`, `mark_edges` seams, `unwrap`, three islands, no UV warning) and the crate (the Crate scenario's `CrateBuild` calls on a mesh named `Crate`, moved 3 m along +X, `project_uvs box`) in one model, each frame zoomed on one mesh (render frames every mesh); the checker look worked from `get_mesh`'s UVs by `UvsChecker.cs` (points on each face, ray test, the cell rule and `CheckerColours`), the layout look by the Crate's `CrateUvLayout.cs` and `UvLayoutPixels.cs` (linked, with `CrateBuild.cs`, `CameraPixels.cs`, `CrateLooks.cs`, `CrateChecker.cs`, `CrateLabels.cs`); two faults made and found (ring 2 moved outward, the top cap extruded), then saved, reopened and exported, and the barrel compared with `samples/barrel.carve` (copied beside the scenario); suite `scenario-uvs` |
| `scenarios/Carve.Scenario.Materials` | The textured barrel: `samples/barrel.carve` given UVs (`project_uvs`, the caps into separate halves of the end-grain texture), four materials from `samples/textures/` and `set_material`, `check` silent, seen in nine frames (front, back, top, bottom, hoop close, sheet, 256 and 1024, mixed with colour faces, then the viewer's two), and after all of them (T-121) the layout look, `render mode=uvs image=` `staves-base.png`, 1024 square, every pixel worked from the texels and the layout rule by `UvImageFrame.cs` (nearest sampling, islands full and the padding half), saved as `samples/barrel-textured.carve`, reopened (frames byte-identical) and exported; every pixel is worked from `get_mesh`'s geometry and UVs and the generator's texels (`MaterialsSampler.cs`, `MaterialsChecks.cs`, with `UvsScene.cs`, `CrateBuild.cs`, `CameraPixels.cs` and `TextureSet.cs`, `Png.cs` linked); `CARVE_MATERIALS_BREAK` (`vflip`, `rune-column`, `metallic`, `mirror`) makes it wrong on purpose and it must fail; suite `scenario-materials`, run by `scripts/run-materials-scenario.ps1` |
| `scenarios/Carve.Scenario.Bakes` | The bakes: the wrench (`samples/wrench.carve`, copied beside the scenario) opened and baked through the tools, one stage per bake, each a class in its own file, run in the order of `BakesStages.cs` (a later bake adds its file and one line there): `SurfaceStage.cs`, `bake` of the masks and base colour at 1024 with padding 8 into the run folder with fps-arms' group colours, `define_material` `wrench` from the base colour and `set_material` on every face; seen in the `uvs` look with the base colour and with the `steel` mask under it (T-121's block rule, the islands also held to the colour worked from the groups; a 4x crop of a steel island's padding cut from the frame), the textured wrench from six sides and close on the jaw, the grip and face 80 (the pixel at the centre and near the corners of every face turned to the eye, in sight and clear of its edges by rays through the pixel, red-dominant for `red` and for nothing else; the x-mirror of the red box is not red, M-2), a six-view sheet, then exported and `render_glb` compared with `render` front and three-quarter under D-6's rule with a control that must fail it; `bakes.tsv`, `samples.tsv` and `probe.tsv` in the run folder; `CurvatureStage.cs`, the `curvature` map at 1024 and width 0.003 seen in the `uvs` look whole and cropped 4x on the jaw (a texel on each convex jaw edge reads 218 or more and the handle's flat middle exactly 128; a small L-block built through the tools, given box UVs and baked, reads 64 or less on its inner edge, its own `uvs` frame and a 4x crop, undone afterwards; `curvature.tsv`); then `OcclusionStage.cs` (T-124), `bake` of `occlusion` and `orm` at 1024 with 32 rays and reach 0.035 and fps-arms' roughness and metallic, `wrench` redefined with the base colour, the ORM in both slots and occlusion 1, seen in the `uvs` look with the occlusion under it, from six sides, the three-quarter and close frames and close on the square drive, checked for the crevice darker than the grip's middle, the grip open, the ORM's G and B exact, and `render_glb` against `render` again (`occlusion-probe.tsv`, `orm-differences.tsv`); then `BevelStage.cs` (T-125): `bake` `normal` with `bevel.width` 0.0016 at 1024 put on material `wrench-bevel` as `normal_texture`, the `uvs` look with it, zoom 8 on a convex grip (`steel`) edge from three directions without and with it, the six sides, texels clear of every hard edge (read from `set_shading`'s answer) held to N, each strip by the edge within 2 luma of the colour rule's shade for the mean normal, and `render_glb` against `render` with the map, with a control that must fail it (`bevel.tsv`); `CARVE_BAKES_BREAK` (`swap`, `faint`, `mirror`, `curvature`, `reach`, `dark`, `metal`, `bevel-g`, `bevel-wide`; each stage's are listed on `BakesChecks.Break`) makes it wrong on purpose and it must fail; the checks the stages share are in `BakesChecks.cs`, with the Uvs scenario's `UvsScene.cs`, the Crate's `CrateBuild.cs`, `CameraPixels.cs` and `ColourRule.cs`, `Png.cs` and Orcward's `ComparisonPng.cs` linked; suite `scenario-bakes` |
| `scenarios/Carve.Scenario.Morph` | The morph scenario (M-21), suite `scenario-morph`. First the arm (`ArmStages.cs`, `samples/arm.carve`): a tube along +X of 11 rings and 8 sides (`ArmPlan.cs`: the cage, `Bulk` as sparse `positions`, `Lean` from `samples/arm-lean.carve`, bones Shoulder > Elbow > Wrist, clip `Curl`, the planted fault `Bad`), built through the tools, smoothed and subdivided once (338 vertices, 336 quads), rigged and seen in seven frames (rest, Bulk 1, Bulk .5 with Lean .5, Lean 1, Bulk 1 from `front`, `Curl` at t = 1 with Bulk 1, `Bad` in the normals look); every pixel is worked from `get_mesh`'s positions by `ArmPlan`'s ray test (a silhouette probe at each ring and between rings, a sweep of sure pixels, hand guards for the curl and the fault) and `ArmGeometry` in `tests/Carve.Core.Tests` works the counts and every probe first (linking `ArmPlan.cs` and `CameraPixels.cs`); saved, compared byte for byte with the committed samples (copied beside the scenario), reopened with frames 1 to 6 byte-identical; then exported (skinned, with `Curl`) into `game/Content/models/arm.glb` of the run folder and drawn by `render_glb` at Bulk 0, 0.5 and 1 from +z (frames 8 to 10) against `render`'s frame under D-6's frame rule (`ComparisonPng.cs` linked from the Orcward scenario; `differences.tsv`, `compare/` triptychs, a control that must fail); `CARVE_MORPH_BREAK` (`mirror`, `weights`, `glbweights`) makes it wrong on purpose and it must fail; run by `scripts/run-morph-scenario.ps1`. Then T-103's cylinder with `Bulge` and `Pinch` (`MorphScenario.cs`) at target weights, posed and in the normals look |
| `scenarios/Carve.Scenario.Planks` | T-116: M-7's barrel with its staves from the planks graph: `open` `samples/barrel.carve`, `project_uvs` as M-7 does, `staves` and `rune-stave` bound to `samples/graphs/planks/` (base colour, normal, occlusion, roughness as `metallic_roughness_texture`), `iron`, `end-grain` and the rune's emissive from `samples/textures/`; five frames (front, back, top, a head-on stave-gap close-up at zoom 4, the sheet) with every pixel worked from the geometry, UVs and texels by M-7's `MaterialsSampler.cs` and `MaterialsChecks.cs` (linked, with `ColourRule.cs`), plus `PlanksChecks.cs` (the gap reads darker than every stave sample, staves differ in tone as `ColourRule.Lit` predicts from the planks' texels, and the PNGs' gaps are at least 3 texels each side); saved as `samples/barrel-planks.carve`, reopened and exported; the break table `CARVE_PLANKS_BREAK` (`t77-base`, `roughness-base`, `mirror`); suite `scenario-planks`, run by `scripts/run-planks-scenario.ps1` with the checks in `tests/Carve.PlanksLoad.Tests` (the saved file equal by content to the sample, the planks' maps on the staves, the `.glb` loaded) |
| `spikes/Carve.ModelViewer` | The model viewer, a stand-in for a game: loads a `.glb` through `IModelLoader` and captures fixed views. It is not the scenario harness, and nothing in the product uses it |
| `tools/Carve.SampleTextures` | Engine-free console app that writes `samples/textures/`, the textured barrel's eight 256 x 256 PNGs, from `TextureSet.cs` (integer-hash noise, fixed seed, no clock); `dotnet run --project tools/Carve.SampleTextures` remakes them, and `SampleTexturesTests` in `tests/Carve.Core.Tests` keeps the committed files equal to it pixel for pixel |
| `tools/Patina.SampleGraphs` | Engine-free console app that evaluates every `samples/graphs/*.texgraph` into `samples/graphs/<name>/<role>.png` at the graph's own size (`planks.texgraph`, T-112's staves: `base_colour`, `normal`, `roughness`, `occlusion`); `dotnet run --project tools/Patina.SampleGraphs` remakes them (`-- --canonical` first rewrites each graph in the format's canonical text), and `PlanksGraphTests` in `tests/Patina.Graphs.Tests` keeps the committed files equal to the graph pixel for pixel and holds the barrel's stave boundaries, the 6-texel gaps and the colour range; `bricks.texgraph` and `fabric.texgraph` (T-136) in the same way |
| `samples/textures` | The eight barrel textures (`staves-*`, `iron-*`, `end-grain-*`, `rune-emissive`), committed output of the generator |
| `tests/Carve.Core.Tests` | xunit tests of the kernel, incl. the engine-free guard; it references `Carve.Debug` and links four scenario files (`ShedChecks.cs`, `ShedGeometry.cs`, `CutsGeometry.cs`, `GatehousePlan.cs`) so that `GatehouseGeometry` works the gatehouse's and the cuts' counts with the scenarios' own camera and ray test |
| `tests/Carve.Server.Tests` | xunit tests of the server: `unit` tests of what a tool answers run on `ToolHarness`, which serves the same tools in the test process with no engine (`CarveServer.AddTools`, carve referenced under the alias `carve`); `unit` also holds the definition tests and the no-extension guard (`ProductReferencesNoExtensionTests`); the `regression` tests cover the stdio transport, extension loading, the debug extension, rendering, the exported `.glb` read back through the engine's loader (`UvExportTests`, `PbrReleaseTests`, no process), and the process-vs-harness end-to-end tests (`ToolFamiliesThroughProcessTests`, `ToolHarnessMatchesProcessTests`) |
| `tests/Carve.Server.Tests.Extension` | The extension the loader tests load, on the public API alone |
| `tests/Patina.Graphs.Tests` | xunit `unit` tests of `Patina.Graphs`: the round trip, every fault, determinism and tiling with test node types registered from outside (`TestNodes.cs`: `rotate`, which reads far from its texels, and `peek`, which reads past what it asked for, in a registry of their own), tile-size and cap invariance and the cache's bound (`EvaluationTests.cs`), wrapped reads (`TileReadTests.cs`), the packing, the five first nodes (`FirstNodesTests.cs`: noise, gradient, levels, blend, height_to_normal, worked by hand, tiling, size-invariance, a table of broken copies), the planks sample (`PlanksGraphTests.cs`), worley, fbm and warp (`WorleyFbmWarpTests.cs`, T-135: worked by hand, tiling, size-invariance, a table of thirteen broken copies), dependent input rectangles (`DependentInputTests.cs`, T-153: `pair`, `pair_short` and `warp_static` in `TestNodes.Dependent`; warp equal to the static warp at every tile size and cap, its tile count), the cobblestones sample (`CobblestonesGraphTests.cs`: the PNGs equal the graph, and check's rules measured on them) and the engine-free guard; suite `patina-unit`, with `samples/graphs/**` among its inputs; the pattern nodes (`PatternNodesTests.cs`, T-136: hand-worked texels, gap and bevel in texels, jitter in its slot, tables of broken copies) and the bricks and fabric samples held equal to their PNGs and to check's rules (`PatternSamplesTests.cs`) |
| `tests/Patina.Server.Tests` | patina's tools in this process (`PatinaHarness`, built with `Program.AddTools`, `unit`, suite `patina-unit` with `tests/Patina.Graphs.Tests`): every worked example in every description run as written, every refusal, all-or-nothing edits and the snapshot stack; carve's `ProductReferenceGuard.cs` linked to guard `patina`; and two `regression` classes (suite `patina-regression`), each starting the real `patina`: `PatinaProcessTests`, over stdio for `tools/list` and a round trip through a file, and `ExportBoundedMemoryTests` (T-115), the planks exported at 8192 with the peak working set at 2 GB or less, most of the suite's time |
| `tests/Carve.ModelViewer.Tests` | The viewer on its own fixture cube, through `run-viewer.ps1`; suite `viewer-cube` |
| `tests/Carve.ExportViewer.Tests` | The checks on what the viewer made of the exported crate, the last step of suite `scenario-export`. They read the run folder in `CARVE_EXPORT_RUN` and are skipped when it is unset, so `dotnet test Carve.slnx` passes without it |
| `tests/Carve.CharacterLoad.Tests` | The checks on the character scenario's run folder, the last step of suite `scenario-character`: the content folder read through `IModelGeometryLoader.Load` (the hat piece against the body's bones), `HumanoidRoleMap.Synty`, and `samples/gardener.carve` compared by content; `RunContent.cs`, the content-folder reading, is linked by `tests/Carve.SoftLoad.Tests` too. They read the run folder in `CARVE_CHARACTER_RUN` and are skipped when it is unset, so `dotnet test Carve.slnx` passes without it |
| `tests/Carve.SoftLoad.Tests` | The checks on the soft scenario's run folder, the last step of suite `scenario-soft`. They link `RunContent.cs` from `tests/Carve.CharacterLoad.Tests`, read the run folder in `CARVE_SOFT_RUN` and are skipped when it is unset, so `dotnet test Carve.slnx` passes without it |
| `tests/Carve.GatehouseLoad.Tests` | The checks on the gatehouse scenario's run folder, the last step of suite `scenario-gatehouse`: the exported `.glb` read through `IModelGeometryLoader.Load` (live triangles only, the door's material present) and `samples/gatehouse.carve` compared by content; `RunContent.cs` is linked from the character checks. They read the run folder in `CARVE_GATEHOUSE_RUN` and are skipped when it is unset |
| `tests/Carve.MaterialsLoad.Tests` | The checks on the materials scenario's run folder, the last step of suite `scenario-materials`: the exported `.glb` read through `IModelGeometryLoader.Load` (four materials, every field and texture byte equal to `samples/textures/`, no warning), `view.json` of the viewer run with `--sky`, the viewer frames by hue and order, and `samples/barrel-textured.carve` compared by content; `RunContent.cs` and `FramePixels.cs` are linked. They read the run folder in `CARVE_MATERIALS_RUN` and are skipped when it is unset |
| `tests/Carve.PartsLoad.Tests` | The checks on the parts scenario's run folder, the last step of suite `scenario-parts` (T-97): `rover.glb` read through `IModelGeometryLoader.Load` with `SplitParts` (the root, `Mast` and four wheels in pre-order, each part's transform equal to `Model.WorldTransform`'s, the wheels on one mesh, 40/40/124 triangles) and merged (bounds equal `Combined`'s, 576 triangles, the five agreed colours), `view.json` of the viewer run with `--sky`, the viewer frames by hue (olive chassis, sand cab, dark grey wheels, yellow lens) and by order (the cab right of the lens, which a mirrored view fails), and `samples/rover.carve` compared by content with the saved file and with `RoverSpec`'s model; `RoverSpec.cs`, `RoverSpec.Model.cs`, `RunContent.cs` and `FramePixels.cs` are linked. They read the run folder in `CARVE_PARTS_RUN` and are skipped when it is unset |
| `tests/Carve.AcceptanceLoad.Tests` | T-49's checks on a round folder: the saved model's two meshes, clips and no unweighted vertex, the three exports read through `Load`, `LoadAnimationSet` and the piece, and `differences.tsv`. Read `CARVE_ACCEPTANCE_RUN`, skipped when it is unset; no suite of its own, but a `dotnet-closure` input of `server-unit`, whose build compiles it; `scripts/run-acceptance-negatives.ps1` breaks a good round five ways and shows them failing |
| `tests/Carve.PartsAcceptance.Tests` | T-98's checks on a parts acceptance round folder: `cart.carve`, `game/Content/models/cart.glb` and `viewer/`. Reads `CARVE_PARTS_ACCEPTANCE_RUN`, skipped when it is unset; no suite of its own, but a `dotnet-closure` input of `server-unit`, whose build compiles it |
| `tests/Carve.OrcwardLoad.Tests` | The checks on the Orcward scenario's run folder, the last step of suite `scenario-orcward`: `Load`, `LoadAnimationSet` and the piece read from the exported files, no export warnings, and the table of differences (every good frame passes, every wrong file and twisted limb fails). Read the run folder in `CARVE_ORCWARD_RUN`, skipped when it is unset |
| `tests/Carve.MorphLoad.Tests` | The checks on the morph scenario's run folder, the last step of suite `scenario-morph` (T-106): `game/Content/models/arm.glb` read through `IModelGeometryLoader.Load` (`TargetNames` `[Bulk, Lean]`, `DefaultWeights` 0, no warnings, every position and normal delta equal to Carve's), `samples/arm.carve` and `arm-lean.carve` compared by content, and `differences.tsv` (every `render_glb` frame of the arm passes D-6's frame rule against `render`, the control fails it). `RunContent.cs` is linked from the character checks. Read `CARVE_MORPH_RUN`, skipped when it is unset |
| `tests/Carve.Scenario.Tiny` | The scenario the debug extension's tests stack on it |
| `tests.json` | The suites and their tiers, which `cairn <kind> <id> verify` and `cairn test run` work from |
| `scripts/verify.ps1` | The old whole run: build warnings-as-errors + all tests + the cube scenario (needs a Vulkan device). The gate no longer runs it |
| `scripts/run-scenario.ps1` | Builds, then runs one scenario on the real `carve`, or with `-Server patina` on the real `patina` (`patina.dll`, the same `--Carve:Extensions:N` arguments); returns its exit code and run folder |
| `scripts/run-viewer.ps1` | Builds, then runs the viewer on one model; returns its exit code |
| `scripts/run-export-scenario.ps1` | The export scenario, then the viewer on its content folder, then the checks of `tests/Carve.ExportViewer.Tests` (built first unless `-NoBuild`, each step bounded; a skipped check fails it); the run folder is the last line on stdout |
| `scripts/run-character-scenario.ps1` | The character scenario, then the checks of `tests/Carve.CharacterLoad.Tests` on its run folder (built first unless `-NoBuild`, the checks stopped after `-ChecksTimeoutSeconds`; a skipped check fails it); the run folder is the last line on stdout |
| `scripts/run-soft-scenario.ps1` | The soft scenario, then the checks of `tests/Carve.SoftLoad.Tests` on its run folder, as `run-character-scenario.ps1` does |
| `scripts/run-gatehouse-scenario.ps1` | The gatehouse scenario, then the checks of `tests/Carve.GatehouseLoad.Tests` on its run folder, as `run-character-scenario.ps1` does |
| `scripts/run-materials-scenario.ps1` | The materials scenario, then the viewer on its content folder with `--sky` (`viewer-front`, `viewer-angle`), then the checks of `tests/Carve.MaterialsLoad.Tests`, as `run-export-scenario.ps1` does |
| `scripts/run-parts-scenario.ps1` | The parts scenario, then the viewer on its content folder with `--sky` (`viewer-front`, `viewer-angle`, a merged load of `rover.glb`), then the checks of `tests/Carve.PartsLoad.Tests`, as `run-materials-scenario.ps1` does; the scenario's timeout is `-ScenarioTimeoutSeconds` (900) |
| `scripts/run-orcward-scenario.ps1` | The Orcward scenario, then the checks of `tests/Carve.OrcwardLoad.Tests` on its run folder, failing if one is skipped |
| `scripts/run-morph-scenario.ps1` | The morph scenario, then the checks of `tests/Carve.MorphLoad.Tests` on its run folder, as `run-orcward-scenario.ps1` does |
| `scripts/run-acceptance-check.ps1` | T-49's frame step on an acceptance round folder: the Acceptance scenario on it, then the checks of `tests/Carve.AcceptanceLoad.Tests` |
| `scripts/run-acceptance-negatives.ps1` | Breaks a good round folder five ways, runs the checks on each copy and writes `acceptance/negatives.tsv` with a `Caught` column; exits 1 when a break is not caught or a run is stopped; built first unless `-NoBuild`, each run of the checks stopped after `-ChecksTimeoutSeconds` |
| `scripts/bounded-run.ps1` | Dot-sourced by the run scripts: starts `dotnet` with quoted arguments (a trailing backslash survives) and stops it after its time limit, exit code 124 |
| `samples/` | Sample models, written by `CarveFile.Write`: `leaky-shed.carve`, the broken shed `check` is shown on, a version 2 file kept equal to `TestMeshes.LeakyShed()` by content (read, not compared by bytes) by `MeshHealthTests`, with its version 1 copy kept as a fixture in `tests/Carve.Core.Tests/Fixtures/leaky-shed-v1.carve` that reads to the same model; `gardener.carve`, the flat character, kept equal by content to the one scenario-character builds through the tools by `tests/Carve.CharacterLoad.Tests`; and `gardener-soft.carve`, the same subdivided one level and smoothed with the hat's brim creased (a version 3 file), kept equal by content to the one scenario-soft builds by `tests/Carve.SoftLoad.Tests`; and `gatehouse.carve`, the wall built by cuts (a version 4 file, with holes and tombstones), kept equal by content to the one scenario-gatehouse builds by `tests/Carve.GatehouseLoad.Tests` and to the one `GatehouseGeometry` builds in Core; and `barrel.carve`, the seamed barrel unwrapped (a version 5 file, with UVs and seams), kept equal by content to `TestMeshes.SeamedBarrel` unwrapped by `UnwrapSampleTests` and compared by content with the barrel scenario-uvs builds through the tools; and `barrel-textured.carve`, that barrel with its four materials (a version 6 file, texture paths `textures/<file>` reaching `samples/textures/`), kept equal by content to the one scenario-materials saves by `tests/Carve.MaterialsLoad.Tests`; and `arm.carve`, the arm with dials (a version 8 file: subdivided once, smooth, rigged with clip `Curl`, targets `Bulk` and `Lean`) with `arm-lean.carve`, its cage at 0.8 times every ring, each kept equal byte for byte to what scenario-morph saves; and `barrel-planks.carve` (T-116), the barrel with its staves from the planks graph (texture paths reaching `graphs/planks/`), kept equal by content to the one scenario-planks saves by `tests/Carve.PlanksLoad.Tests`; and `rover.carve` (T-97), the rover as a part tree with its two levels (a version 7 file), kept equal by content to the one scenario-parts saves and to `RoverSpec`'s model by `tests/Carve.PartsLoad.Tests`; and `wrench.carve` (T-122), the fps-arms wrench (private repo `Arcodiant/fps-arms`, `main` at `1535e3e`, `carve/wrench.carve`) as it is there, byte for byte and written by nothing here: a version 5 file, 120 faces all mapped, smooth, groups `rubber`, `steel`, `dark`, `red`, `black` and `all`, the model M-24's `bake` is shown on, kept readable and overlap-free at 1024 by `WrenchSampleTests`. `samples/graphs/` holds the texture graphs (`planks.texgraph`, T-112; `cobblestones.texgraph`, T-135: `worley` stones bent by a `warp` of two `fbm`s, dirt in the gaps) and their outputs (`planks/<role>.png`, `cobblestones/<role>.png`, written by `tools/Patina.SampleGraphs`) |
| `docs/` | cairn tracker (tasks, bugs, milestones, design docs, changelog) |
| `artifacts/` | Ignored: captures, review artefacts and raw content, kept across tasks |

## Build, test and verify

```
dotnet build Carve.slnx
dotnet test Carve.slnx
dotnet run --project src/Carve.Server
cairn <kind> <id> verify
```

A change is verified when `cairn <kind> <id> verify` passes and, for anything
that can be seen, the frames from that run have been looked at. Do not run the
scenario again to get them. A queued run's frames come from `cairn test
artifacts <suite>` (`scenario-cube` for the cube); a run on this machine leaves
them in `artifacts/scenarios/<name>/`. Run a scenario by hand only where no suite
covers what changed, and then add a suite. Say which of the two you did, and what
you did not check.

Every test class carries a `Tier` trait, `unit` or `regression`, by what it does:
one that starts `carve` or needs a device is `regression`, and `unit` means about
1 s or less per test. A class with no trait runs as `unit`. A new scenario joins
`Carve.slnx` and gets a `regression` suite in `tests.json`.

A task or bug's `**Verify:**` names `server-regression` only when it changes something that suite
alone covers: the stdio transport, extension loading, the debug extension, rendering, or the exported `.glb` read back through the engine's loader (`UvExportTests`, `PbrReleaseTests` hold it). Tool behaviour is
covered by `server-unit` (`ToolHarness` runs the tools in the test process). The milestone's landing on `main`
runs `server-regression` anyway, so a regression a task's verify did not run is caught there.

## Test scenarios

Everything that looks at the running product goes through **one scenario
harness**, committed in the first milestone and used for every visual check.
Never write a throwaway capture script or a one-off launcher; extend the harness.

Scenario runs are quiet and repeatable, so a run does not disturb the person at
the machine and its frames compare from run to run:

- **Audio muted**: the master volume at 0, as an override never saved to the
  user's settings. Carve makes no sound today; the rule stands for the day it does.
- **A fixed clock.**
- **A fixed seed** for every random choice.
- **A fixed window size** (the render target size, for offscreen renders).
- **A fresh user-data folder** each run, under the run's own output folder.

## Scenarios are extensions, stacked on a debug extension

Carve is a long-lived process with a host of its own, so it exposes an
**extension API** and carries no debug capability of its own. Three layers, each
depending only on the one below:

1. **The product** ships the extension API and loads extensions. It knows nothing
   of any particular extension.
2. **The debug extension** is the one place debug hooks live: the fixed clock and
   seed, the muted-audio override, the fresh user-data folder, scripted input
   (for Carve, scripted MCP tool calls through the real dispatch), state
   inspection, and headless running and frame capture. It is built on the public
   extension API like any other extension.
3. **Scenarios** are extensions stacked on the debug extension. Each scripts one
   situation and what is captured from it.

The scenario harness is the shipped product itself, started with the debug
extension and one scenario stacked on it. **There is no separate scenarios
executable.**

Scenarios are the API's first and most demanding client, on purpose: they need
freedoms the product's author might not think to give an extension (how the host
is composed, how time advances, what input arrives), and building them pressures
the API into shape. When a scenario needs something the API cannot do, widen the
API or the debug extension. **Never special-case the product or reach into its
internals; no internals access is granted to an extension.**

Neither the debug extension nor any scenario ships with the product. A guard test
committed in the first milestone fails if the product references any extension,
the debug extension and the scenarios among them, checked both in what its project
file declares and in what the built product actually references.

The product's own render tool is a product feature, not a debug hook: an agent
asks for a render through MCP like any other tool. The debug hooks are what makes
that render repeatable and what drives it unattended.

### Writing a scenario

One project per scenario, `scenarios/Carve.Scenario.<Name>/Carve.Scenario.<Name>.csproj`,
with `EnableDynamicLoading` set and project references to `Carve.Debug` and
`Carve.Extensibility` only. It holds a public `ICarveExtension` whose `ConfigureHost` does
`context.Services.AddSingleton<IScenario, MyScenario>()`, and the `IScenario`, which may take
`SeededRandom` or `IConfiguration` in its constructor. Exactly one scenario to a run.

`pwsh scripts/run-scenario.ps1 <name>` runs it: the run folder's path is on stdout and the
exit code is `carve`'s. `<name>` is the last part of the assembly name, lower-cased, and names
the folder, `artifacts/scenarios/<name>/<run number>/`, unless `Carve:Debug:RunFolder` says
otherwise; on a queued run the script sets that to `<name>/` under `CAIRN_ARTIFACTS`. That folder holds `frames/`, a fresh `user-data/`, and `run.json`, the log of every
call and result, in which an image is its size and hash.

`ScenarioDriver` is all a scenario has: `ListToolsAsync`, `CallAsync(tool, args)`,
`CaptureAsync(name, args)`, `AssertBytesEqual(what, expected, actual)`, `Fail(message)`, and `RunFolder`, the run folder's path, for a scenario that keeps files in it. A tool
error is a result from `CallAsync` and a failure from `CaptureAsync`. Compare a
`ScenarioFrame`'s `Pixels`, and sample with `Pixel(x, y)`.

### Headless runs

The product has no headless or capture flag of its own: no `--headless`, no
`--capture`, no `if (debug)`. Running without a window is the engine's own
configuration, found in its packaged docs, and the debug extension turns it on as a
default, set below the command line rather than forced, so a person can watch any
scenario in a window by switching it back there. The extension API lets an
extension add configuration and services before the host is built, since the engine
reads such a switch once, at build.

Carve has no window today (D-1), so this binds the day a window arrives, and the
extension API is built ready for it.

## Working rules

- **Ask, don't land.** A rule the spec does not settle, or a change that would
  loosen a check (a tolerance, a test's target, a baseline), goes back to the
  person rather than landing.
- **Keep the evidence.** Review artefacts and raw content are kept in `artifacts/`
  (ignored), where finishing a task does not delete them. Logs go to the temp
  folder (`$env:TEMP`), never into the repo.
- **Stop only your own processes.** An agent stops only processes it started,
  identified by a command line inside its own worktree, never a test host,
  `dotnet` or a runtime machine-wide.

## Engine docs: read these, not the engine source

The version-matched Empyrean docs ship inside each package. Find the cache with
`dotnet nuget locals global-packages --list`, then start at
`<cache>/arcodiant.empyrean/<version>/docs/AGENTS.md` and each package's own
`docs/AGENTS.md`, `docs/recipes/` and `docs/api/index.md`.

## Engine versions and engine needs

The engine version is pinned once, in `Directory.Build.props` (`EmpyreanVersion`,
plus any per-package patch pin with a comment saying why), never as a literal in a
`.csproj`. The packages come from nuget.org; the repo has no `NuGet.config`.

When Carve needs something the engine is missing, record it as an **engine need**:
add a row to D-1's *Engine needs* table (number, what is missing, priority, the
interim), and cite the number at the workaround site
(`// Interim until engine need N: ...`). Carve is not registered as an engine
request client; registering one is the person's call.

## Work tracking

Run `cairn status` at the start of a session and `cairn next` to pick work. Query
items with `cairn list` / `cairn show`; never grep `docs/` to find open work.


<!-- BEGIN cairn -->
This project's work-item tracker (tasks, bugs, batches, milestones, design
docs, changelog) is managed by the **cairn** skill — invoke it for any
tracker action. It is the single source of truth for the generic
methodology (layout, id reservation, merge-back protocol, linting); this
project's own variations on the defaults, if any, are documented below
this marker.

When work is fanned out to subagents, a member is not `Done` because the
subagent said so — it is `Done` when `cairn task <id> verify` exits clean.
Never mark a member `Done`, and never `finish` a container, on a
self-report alone.

Work landing on `main` needs a current, answered review: `finish` refuses
without one. See the skill's `references/review.md`.

Tests: check your work with `cairn <kind> <id> verify` or `cairn test run
<suite>`, which carry a pass forward and take the machine's test slots
(running here when no runner is up; with no `tests.json`, `verify` runs the
whole hook once and carries it). Never take a GPU lock or kill test hosts
by hand. A carried pass is real; use `--fresh` only with a reason you
state. See the skill's `references/testing.md`.
<!-- END cairn -->

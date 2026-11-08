---
description: >-
  Review of M-4
---
# RV-20 — Review of M-4

**Status:** Open

**Reviews:** M-4

**Round:** 1

**Previous:** —

**Diff:** —

**Reviewer:** four reviewers, each claude-opus-5-5

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: T-22 and T-23, the query language and the health check (claude-opus-5-5)

### The test for "volume taken relative to the shell's first vertex" cannot fail (Minor)

The Scope says the signed volume is "taken relative to the shell's first vertex", and the only test that claims to cover it is `Volume_does_not_depend_on_where_the_shell_is` (tests/Carve.Core.Tests/MeshHealthTests.cs:56-61). That test uses the unit cube moved by (-100, -200, -300). Every coordinate is a whole number, so every product is exact in double. A version that measures from the world origin, dropping the `- o` in `From` at MeshHealth.cs:131, also gives exactly 1; a throwaway build gave `1` both ways. A mesh that would catch it is a 0.1-unit cube at (1000.3, 2000.7, -3000.1): measured from the world origin its volume is 0.0010004878567997366, and from its first vertex 0.0010004878640756942. No other test fails without this Scope point.

**Outcome:** —

### `inside-out` is decided by the sign of rounding noise on a flat closed shell (Minor)

MeshHealth.cs:64 reports `inside-out` whenever `SignedVolume < 0`, with nothing relative to the shell's size. A two-sided flat sheet passes `PolygonMesh.Create` and gets no warnings; its signed volume should be 0, but in practice it is a tiny number of either sign. Example: four points on the plane z = 0.37x - 0.61y + 0.3, scaled by 1.37 and offset by (13.1, -7.3, 3.3), with a quad `[0,1,2,3]` on top and triangles `[0,3,2]`, `[0,2,1]` underneath. Its signed volume is -2.27e-7, and `check` reports `closed shell of 3 faces ... is inside-out: ... a game culls them and sees nothing`, which is wrong: the sheet is visible from both sides. The same sheet at other offsets came out at +1.1e-8, +6.2e-7 and +2.1e-6, and none of those was reported. A doubled face with the same vertices gives exactly 0, so only a sheet whose two sides are triangulated differently is hit. Any fix needs a threshold, which is a tolerance and goes to the person (T-23 Notes: "Any tolerance anywhere" is stop-and-ask).

**Outcome:** —

### A missing `)` is not reported when another error falls at the same character (Minor)

`Parser.Fail` (MeshQuery.cs:475) drops a new problem if the last one recorded has the same position, whatever its message. The intent is to skip the same error seen again on the way out of a rule, but it also drops a different error. `(y > 1 and x <` reports only `at character 15: expected a number`, and `(not` reports only `at character 5: expected a property, a flag, all, none, not or '('`; in both, the `expected ')'` at the same character is lost (checked in a throwaway build). A where cut off inside a bracket is a likely agent mistake, and it now takes two calls to learn both problems, against the Scope's "Every structural error that can be found is listed". Dropping a repeat only when the message also matches would fix it. No test covers two errors at one position.

**Outcome:** —

### `coincident-vertices` misses -0 against 0, while `select` treats them as equal (Minor)

MeshHealth.cs:162 compares positions by their raw bits, so `-0f` and `0f` count as different positions. `define_mesh` keeps a JSON `-0` as `-0f` (no normalisation anywhere in Core). So the cube plus a vertex at `[-0, 0, 0]` gives no `coincident-vertices` problem in `check`, yet `select vertices where x == 0 and y == 0 and z == 0` returns `[0, 8]` (checked). The two tools disagree about the same pair, and a game would see the two vertices at one point. The Scope says "bit-equal on all three", so this follows the letter of the spec; the description says "at exactly the same position", and that is what the person should rule on.

**Outcome:** —

### "is a number" error names the property without the axis the agent typed (Nit)

MeshQuery.cs:161 builds the message from `use.TypedName` only. `normal.y` on its own gives `'normal' is a number; compare it with ...`, and `min.x` gives `'min' is a number`. The agent typed `normal.y`, and the axis error at line 153 does quote `'normal.w'` in full. MeshQueryTests.cs:463 pins the shortened wording.

**Outcome:** —

## Findings: T-24, T-25 and T-26, the render pipeline (claude-opus-5-5)

### The test for a look following the mesh through an edit cannot fail on the break it is named for (Major)

`RenderTests.A_look_follows_the_mesh_through_an_edit` (tests/Carve.Server.Tests/RenderTests.cs:647) renders `mode: wireframe`, moves vertex 6, renders `mode: wireframe` again, and asserts only that the two PNGs differ; moving the vertex changes the image whatever look is drawn. Break: `RenderSystem.ShowMesh` (src/Carve.Server/RenderSystem.cs:94-103) calls `PutLook(mesh, space, Look.Shaded, Highlight.None)` instead of `_look, _highlight`. After the move the pump sees the request's spec equal to `_spec`, so it does not call `ApplyView` again (RenderPump.cs:378); the second render is shaded under a caption that says `wireframe`, and the test still passes because shaded differs from the earlier wireframe. A stale line mesh that is never rebuilt also passes, because the fill moved. No scenario checks a look after an edit either: the crate's look frames come before any edit, and the shed renders only shaded after its `move` (ShedScenario.cs:89). T-25's As built already says this test "was not seen to fail". So "the look meshes are rebuilt when the mesh … changes" has no check that proves it. A useful test would compare the post-edit wireframe with the wireframe of the moved mesh rendered in a fresh process, or check that it differs from the post-edit shaded frame.

**Outcome:** —

### A contact sheet can show two different meshes in one image (Minor)

`RenderTool.Run` posts every tile as its own request with the same `PostedVersion` (RenderTool.cs:248-251). The pump serves "the mesh current at its post or a newer one" per request; on any step after the first served request, a store change makes it call `ShowMesh` and return (RenderPump.cs:341-346), so later tiles show the newer mesh. Failing case: `render {"views":["+x","-x","+y","-y","+z","-z"]}` and a `move` as parallel tool calls, or a `move` from another client: tiles 1-2 show the mesh before the move and tiles 3-6 after it, in one PNG. The sheet caption takes its counts from `frames[^1]` only (RenderTool.cs:276), so nothing tells the agent the tiles disagree, against the description's "one call shows a mesh all round". No test posts a sheet with an edit racing it.

**Outcome:** —

### `views` with an explicit `null` single-view argument is refused (Minor)

The exclusion check is `given?.ContainsKey(name)` (RenderTool.cs:169). It ignores the value, while everywhere else in the tool a JSON `null` means absent (`Absent`, used for `views`, `highlight` and the keys of a `views` entry). `{"views":["+x","-x"],"view":null}` gives "views: not with view, direction, zoom or target". The published schema advertises `"default": null` for `view`, `direction` and `target` and `"default": 1` for `zoom` (checked with `tools/list`), so a client that sends nulls or fills in schema defaults cannot ask for a sheet at all. The refusal should go by `!Absent(value)`; for `zoom`, refuse only a value other than 1, or say in the text that zoom 1 is refused too.

**Outcome:** —

### Several Scope points have no test that would fail without them (Minor)

- T-25 "a camera change alone rebuilds nothing" (RenderSystem.cs:199): if `PutLook` ran on every `ApplyView`, every frame would be byte-identical and every test would pass.
- T-25 "old handles destroyed after the swap" (RenderSystem.cs:129-134 and 182-185): dropping either `resources.Destroy` leaks a GPU mesh on every look change or edit, and nothing notices.
- T-26 "`highlight` applies to every tile": `What_applies_to_a_sheet_applies_to_every_tile` covers `labels` and `mode` only; only the caption shows `highlight` on a sheet.
- T-25 Goal "at any of the three sizes": no look and no label is pixel-checked at 256 or 1024, apart from the crate's 1024 labels frame; nothing would fail if the look meshes reached only the 512 camera's stage.

**Outcome:** —

### `PutLook` leaks the meshes it has already made when a later `CreateMesh` throws (Minor)

`PutLook` (RenderSystem.cs:109-116) creates `next`, then `edges`, then `orange` before swapping anything in. Its doc comment says "a failure leaves the scene as it was", which holds for the scene, but if `CreateMesh(edgeLines)` or `CreateMesh(orangeLines)` throws, `next` (and `edges`) are never destroyed. The pump retries on the next request with the same spec (`_viewApplied` false) and leaks again on each retry. Narrow, since it needs a failing `CreateMesh`, but the comment claims the opposite.

**Outcome:** —

### The `columns` error calls 1.5 "outside 1 to 3" (Nit)

At RenderTool.cs:174-176, a non-integer `columns` such as `1.5` gets "columns: 1.5 is outside 1 to 3.", which is false. `RenderTests` (line 699) pins this text. "is not 1, 2 or 3" would be accurate.

**Outcome:** —

## Findings: the scenarios and frame checks, T-24 to T-27 (claude-opus-5-5)

### `shed-open-edges` cannot tell the open edges from the closed ones (Major)

The frame is there to show which edges are open. Its only positive check is `Line` on edge 6-7 (`scenarios/Carve.Scenario.Shed/ShedLooks.cs:96`), which passes if a single pixel within the 3x3 band along the segment is within 10 of orange. Its only negative check (`ShedLooks.cs:97-108`) looks at sample points inside faces 3 and 4, kept clear of every edge. Nothing checks that an edge outside the selection is not orange, so a `highlight` that ignores its `edges` list and draws every edge orange passes, and so does one that draws all of the mesh's open edges whatever was passed. The crate has the same gap: `crate-highlight-front` and `crate-highlight-wireframe-front` (`scenarios/Carve.Scenario.Crate/CrateLooks.cs:189-223`) check the bar's selected edge and an unhighlighted face's fill, but no unselected edge. The fix is a `Line` check that is not orange on a closed edge facing the camera, for example the +Z wall's corner 2-6 or the base edge 0-1.

**Outcome:** —

### The stray vertex's "not labelled" check never runs (Minor)

Vertex 24 is at (6, 0, 0). The product frames the face bounds, 0 to 4, so through the scenario's own `Camera` the vertex projects to about (1093, 829) in the 1024 frame (about 547 in x at 512), off the image. `Labels` (`ShedLooks.cs:181-192`) silently `continue`s on any point within 4 px of the edge or beyond it, so the check is skipped on every run; a product that labels unattached vertices would also draw that label off the image, so no frame from `front` can catch it. The milestone's *Never verified* list files this under "seen to fail only behind another"; in fact it cannot fail. Either assert it from a view that holds (6, 0, 0), or make the skip a `Note` so the spec's step is not reported as covered.

**Outcome:** —

### `shed-front-1024` has no checks, and `shed-1024-labels-both` has no positive check on the mesh (Minor)

`ShedLooks.cs:115` captures `shed-front-1024` and uses it only as the baseline for the labels: no `Caption`, no `Corners`, no `ShadedFrame`. Every check in `shed-1024-labels-both` (`ShedLooks.cs:116-119`) is relative to that frame (white in each label box, backing against the plain pixel, unmarked pixels equal to plain). So a 1024 render that draws no shed, or draws it with a broken camera while `LabelLayout`'s projection is right, passes both frames. `cube-front-1024` does check 1024 shading, but these two frames break the rule that every frame has a positive pixel check. The fix is `ShadedFrame(plain1024, new Camera(..., 1024), shed)` plus a caption check.

**Outcome:** —

### The wireframe never checks the background, and its sample-count guard counts samples it does not check (Minor)

`Samples(wire, ..., _ => Fill, background: false, ...)` (`ShedLooks.cs:41`) checks fill only where the ray test sees a face. For `seen == -1` it checks nothing but still counts the sample in `tested++` (`scenarios/Carve.Scenario.Shed/ShedChecks.cs:193-201`), so the `wireTested < 20` guard can be met by background samples alone. T-25 says the wireframe's faces are "drawn as today's triangles", that is, culled as in shaded. A wireframe that stopped culling would fill the inside of the remaining slope (face 11) where the sky should show through the gap; the shed would pass, and so would the closed crate, which cannot show a cull. Normals has the matching `behindSlope` check (`ShedLooks.cs:77-90`); the wireframe has none.

**Outcome:** —

### The expected pixels come from the product's own `get_mesh`, never from the sample file (Minor)

`Shape.ReadAsync` (`scenarios/Carve.Scenario.Shed/ShedGeometry.cs:235`, called at `ShedScenario.cs:47`) builds the ray test's mesh from `get_mesh`, so the expected and the actual frames both follow whatever `open` loaded. `check`'s counts, area, bounds and warnings constrain that only in part: an `open` fault that shifts the chimney in z (same area, bounds and topology) is drawn, read back and expected in the same place. Only the hard-coded `+z`-tile points and the hand-placed lines would notice, and the `+z` tile is blind to z. The sample already sits beside the assembly, so parse it and compare it with `get_mesh` vertex for vertex, or build `Shape` from the file.

**Outcome:** —

### The `move` error check accepts almost any error (Minor)

`ShedScenario.cs:97` requires only the substrings "3" and "9". The product's text is `the first listed edge, [3, 9], is not an edge of the mesh` (`src/Carve.Core/MeshEdits.cs:277`). An error for a different reason (a bad `by`, an unknown argument) that happens to contain both digits would pass, and the spec asks for "a tool error naming the pair". Assert `[3, 9]` and `is not an edge` instead. The retrospective lists this as weak; it is still open.

**Outcome:** —

### The wireframe check's points for the far wall's top edge are off the edge (Nit)

`ShedLooks.cs:50` uses (0, 2.53, 0.5) to (0, 2.51, 2.0), but edge 4-7 runs from (0, 2.55, 0) to (0, 2.5, 3), so at those z values it is at y 2.542 and 2.517. That is under a pixel at 512, and the 3x3 band absorbs it. Taking the endpoints from `shed.Vertices[4]` and `[7]` would say what is meant.

**Outcome:** —

## Findings: the whole diff (claude-opus-5-5)

### `move`'s description tells an unsure agent to move the edges `select` returns, "instead of stopping to ask", against the ruling that asking is the right recovery (Major)

`src/Carve.Server/MoveTool.cs:16` now says that when a pair is rejected, or the agent is not sure of one, it should "take real edges from select (kind edges, with a where such as open) and move those, instead of stopping to ask." The person's ruling of 2026-11-07 says otherwise in three places: T-28's Notes and amended pass criterion, the milestone Retrospective ("an agent that meets a wrong id asks rather than guesses … Judge recovery by the error being understood"), and D-1's *Tool shape* M-4 text ("the lever is the error text (B-2)"). All three count declining to guess as correct, while the tool text, which D-1 treats as the interface, tells the agent to do the opposite. "Move those" also reads literally as "move what select returned": on the shed, `select {"kind":"edges","where":"open"}` returns all eight open edges (4-5, 4-10, 5-6, 6-7, 7-8, 8-9, 9-11, 10-11 per T-23's table), so an agent that follows the sentence with its example `where` raises the whole eave, rakes and seam by the offset meant for one edge. Round 3 showed agents act on a sentence of this kind. The person restored round 2's text exactly, so this needs their ruling, not a silent edit.

**Outcome:** —

### `render` silently ignores a misspelt `highlight` key and a `columns` given without `views`, though the rest of the interface refuses unknown input (Minor)

`RenderTool.cs:544-545` reads only `vertices`, `edges` and `faces` from `highlight` and drops any other key, and `RenderTool.cs:174-177` range-checks `columns` and then ignores it when there is no `views`. So `{"highlight":{"face":[3]}}` returns a plain render with no error (the only sign is a caption with no `highlight` part), and `{"columns":2}` with a single view does nothing. The same tool refuses a misspelt key elsewhere (`{"views":[{"view":"+x","zom":2},"front"]}` is a tool error, `RenderTool.cs:405-411`), and `MeshToolSupport.UnknownArguments` gives the reason: "so that a misspelt name is a tool error rather than an argument silently dropped". Both readings are recorded in T-25's and T-26's As built, but the description (`RenderTool.cs:119`, `:127`, and the error list at `:136-139`) does not say either input is ignored. Ignoring extra keys is needed so a whole `select` result works as a highlight; the description should say only those three keys are read, or `highlight` could refuse keys other than those three plus `kind`, `count` and `of`.

**Outcome:** —

### Two of `render`'s description sentences are stale after M-4 (Minor)

`RenderTool.cs:140-141`: "A machine with no working Vulkan device gives a tool error naming Vulkan; define_mesh, open, move, extrude, get_mesh and undo still work there." This leaves out the two new engine-free tools, `select` and `check` (and `save` and `export`, already missing); an agent on a machine with no device can conclude it cannot ask the mesh anything. `RenderTool.cs:92`, a sentence M-4 rewrote: "Nothing is drawn but the mesh, and the labels and tile captions you ask for." M-4 also draws the normals look's spikes, the wireframe's edge lines, and the highlight's orange lines and stars, which later paragraphs of the same description say.

**Outcome:** —

### A `highlight` type error is named as if it were a top-level argument (Nit)

`RenderTool.cs:545` passes the bare list names to `Ids` and `Pairs`, so `{"highlight":{"faces":"top"}}` fails with `faces: expected an array of integer ids` (pinned at `RenderTests.cs:556`), and `render` has no `faces` argument. The top-level shape error does say `highlight:`, so the list errors would be clearer as `highlight.faces: …`.

**Outcome:** —

### D-1 and the README carry small stale or partial statements after M-4 (Nit)

`docs/design/D-1-carve-concept.md:158`, *Tool shape*: "`get_mesh` is how an agent learns the ids (no ids are drawn on renders)"; the M-4 sentence appended to the same bullet does not correct this, and `render` now has `labels` and `select` returns ids. D-1's new *Queries and selections* line says an answer "stays good until an edit removes or reshapes what it names", but no edit removes anything (`check`: "There is no delete yet"), and it leaves out `select`'s own caveat that undo frees the ids its step created. README *Asking the mesh* (`README.md:60`): the list of what `check` reports leaves out open edges (holes), the shed's most numerous warning and what T-28's prompt asks for next.

**Outcome:** —

## Summary

Round 1, after the refactor pass (`293f028`); `main` had not moved since M-4 forked, so no rebase came first. The diff (60 files, about 7,300 lines; 18 source files, 25 test and scenario files) was split by area over four Opus 5.5 reviewers run in parallel: the query language and health check (T-22, T-23); the render pipeline (T-24 to T-26); the scenarios and their frame checks (T-24 to T-27); and one pass over the whole diff for interfaces, the tool descriptions as one text, architecture rules and tracker hygiene. 23 findings: 3 Major, 15 Minor, 5 Nit.

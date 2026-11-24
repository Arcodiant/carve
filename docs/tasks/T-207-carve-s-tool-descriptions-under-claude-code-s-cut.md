---
description: >-
  Every carve tool description fits Claude Code's 4,096-character cut, held by a test, with the detail that was cut still reachable by an agent
---
# T-207 — carve's tool descriptions under Claude Code's cut

**Status:** In Progress

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** T-165, T-166

**Verify:** server-unit

## Goal

Every description `carve` serves in `tools/list` reaches an agent whole. Claude Code cuts an MCP tool description at
4,096 characters, and several of carve's are past it: `render` (about 18,600), `export` (about 14,000), `check` (about
8,300), `save` (6,286) and `open` (5,617), all from before M-29. What is cut today is still reachable by an agent
through the tools, and a test holds every description under the cut, as `NodeToolTests` does for patina (T-170).

## Scope

Written for the recommended option, a `help` tool (see "For the person to decide"); the parts marked **[alt]** change
if the person declines it.

- **Measure first, after T-165 and T-166 land.** Re-run the measurement below on the rebased task branch (lengths of
  `tools/list` as served, not the source literals) and write the figures here; the plan is against M-29 at `edb34ba`
  plus those two members' sentences.
- **The budget.** Every description in carve's `tools/list` is at most **3,686 characters** (Claude Code's cut of
  4,096 less 10%, T-170's measured cut and the margin the person approved for patina), counted as `string.Length`, no
  tool exempt, the new tool included. Parameter descriptions are not budgeted (no cut is known for them; the largest is
  586, `open.mesh`).
- **The mechanism: a `help` tool** (`src/Carve.Server/HelpTool.cs`, registered in `CarveServer.AddTools` after
  `bake`). **[alt]** none of this bullet; see the alternative under "For the person to decide".
  - `help {"tool":"render"}` returns that tool's reference as one text block: the detail that left its description,
    in the same words, with its worked examples kept as `Example: ` lines (`MeshToolSupport.ExampleMarker`) so tests
    read them the way they read a description's. `help {}` returns the list of entries, one line each (the tool and
    what its reference covers). A tool with no entry, or an unknown name, is a tool error listing the entries (patina's
    `nodes` refusal: `help: there is no entry for 'move'; the entries are bake, check, export, open, render, save,
    select`). It reads nothing of the model: it answers with no mesh, changes nothing and is not an undo step.
  - Each entry's text lives beside its tool's `Text`, as `public const string Reference` on the tool class
    (`RenderTool`, `ExportTool`, `BakeTool`, `CheckTool`, `SaveTool`, `OpenTool`, `SelectTool`); `HelpTool` holds
    only the name-to-`Reference` table and its own description (under 1,000 characters, naming every entry).
  - Every trimmed description ends with one pointer sentence of one form, `More: help {"tool":"<name>"} gives
    <what moved>.`, so an agent knows the rest exists and how to ask.
- **What stays in a description, for every tool:** what the tool does and when to call it; every argument by name with
  its one-line meaning (the schema's parameter descriptions carry the rest); the rules that change what a call does or
  would destroy (render's MISSING/EMPTY winding reading, open's REPLACES THE WHOLE MODEL, export's axes and units and
  `overwrite`); every sentence M-29 added about graph materials (T-160 to T-166), since T-168 depends on them; the
  return shape in one sentence; one short worked example run as written; the pointer to `help`. **What moves:**
  caption formats, colour tables, thresholds, file formats, the long error catalogues (each error still names itself
  when it happens), second and third worked examples, and per-feature detail an agent needs only once it uses that
  feature.
- **Tool by tool, over the cut** (figures from the table in Notes; aim at 3,000 to 3,400 so T-165/T-166 and later
  sentences fit):
  - **render** (19,122): keeps the purpose and what is drawn; views, direction, zoom/target, projection and size in
    a sentence each; one sentence per mode (`shaded` with materials and the graph-bake sentences, `wireframe`,
    `normals`, `checker`, `uvs` and `image`); highlight, labels and `views` sheets one sentence each, with the six-axis
    sheet example; the winding and EMPTY reading; pose, clip/time, targets, bones, weights, part/level one sentence
    each; the no-mesh and Vulkan errors. Moves: every caption format, the normals colour code, the checker hue table
    and its cube example, uvs mode's drawing rules and refused arguments, label placement, sheet geometry and retries,
    bone and weight drawing rules, and the error list (about 3,700 across the description).
  - **export** (14,015): keeps purpose, path/overwrite, axes and units, the three modes (model, piece, clips) one
    sentence each, parts-to-nodes in one sentence, skinned in one, graph materials (T-164's paragraph, shortened only
    if it must be, keeping its meaning), the limits warning in one sentence, the result line's shape, the crate
    example. Moves: "what is written" (about 2,600), the limits file format (about 1,600), the skinned detail, the
    piece and clips detail, the rover and material worked examples, the content-folder example, the full result line.
  - **bake** (8,410): keeps purpose, the UV layout in one sentence, the arguments sentence, one line per map, the
    return shape, the wrench `Example:`. Moves: each map's channel and normal-map detail (about 3,600), the `surface`
    entry rules, curvature, the errors (about 1,100) and the worked-example prose.
  - **check** (7,565 before T-166): keeps purpose, `mesh` and `targets`, the return shape, the name of every problem,
    warning and note with its fix in a clause (T-166's material warnings among them), the cube `Example:` and
    `Output:`. Moves: thresholds (signed volume, coincidence), the morph-target detail, the UV and envelope warnings'
    rules, the stats definitions.
  - **save** (6,286): keeps whole model vs one mesh, the .carve-vs-.glb rule, a one-sentence version list (2 to 10,
    what each adds), path rules, errors in one sentence, the result line, and the five-step workflow (open's and
    export's descriptions point at it). Moves: the cube file listing, the format paragraph, the part-tree detail and
    example, the graph-slot version detail.
  - **open** (5,617): keeps replace vs one mesh, the undo rule, materials and graph slots coming back (with T-160's and
    T-166's warnings), ids as saved, path rules, the return shape, the example. Moves: the error catalogue (1,274),
    the version 1 detail, the part-tree detail and its example.
  - **select** (5,571): keeps the grammar, the properties of each kind, the return shape, the three cube examples and
    one select-then-move. Moves: the twelve sample error messages, the float-comparison detail, the duplicated top-face
    example.
- **Under the cut but over the budget** (no `help` entry; tightened in place, nothing of substance cut): define_mesh
  (4,032), get_mesh (3,983), define_material (3,961 before T-165/T-166), project_uvs (3,809), unwrap (3,720). If one
  will not come under without losing a rule, it gets an entry like the seven.
- **Tests** (`tests/Carve.Server.Tests`, `unit`, on `ToolHarness`):
  - **New `HelpToolTests.cs`**: every description from `tools/list` at most 3,686, no tool exempt, with `ClientCut`
    and `Limit` constants and the comment as in `NodeToolTests`; `help` is in the list; each entry's tool ends with the
    pointer sentence; `help {}` names every entry; one entry returned, an unknown name and a tool with no entry refused
    in the message's words; help works with no mesh and leaves undo as it was. **[alt]** only the length test.
  - **`FileToolTests.Examples`** gains an overload over a text, so a test can take `Example: ` lines from a `help`
    result the same way.
  - **Every test that reads a sentence or example that moves** reads it from `help`'s entry instead; none is deleted,
    and no worked example stops being run as written. The files that read description text today (count of reads):
    EditToolDefinitionTests 13, TargetToolTests 11, UvToolDefinitionTests 8, MergeBridgeToolDefinitionTests 5,
    FileToolTests 4, SaveOpenToolDefinitionTests 4, SelectToolDefinitionTests 4, CheckToolDefinitionTests 3,
    GraphSlotToolTests 3, MaterialToolDefinitionTests 3, PartToolsTests 3, RenderDefinitionTests 3, and one or two in
    BakeToolTests, BakeNormalToolTests, CheckToolTests, CheckerArgumentTests, DefineMeshDefinitionTests,
    DefineMeshTests, ExportPartsToolTests, ExportToolDefinitionTests, GetWeightsToolDefinitionTests,
    HoledFaceToolDefinitionTests, HoledFaceToolTests, LoopCutToolDefinitionTests, MergeBridgeToolTests,
    NamedMeshDefinitionTests, NamedMeshTests, SkeletonToolDefinitionTests, SkeletonToolTests, SubdivideToolTests,
    UnwrapToolDefinitionTests, UvToolTests. Only those touching the twelve tools above should need changing.
  - **`PartToolsListTests`**: `help` joins `NewTools`; a pinned sentence that moves (`RenderPartTexts.Parts`,
    `RenderPartTexts.Image`, `RenderGlbTexts.Sampling` if render_glb is touched, `PartTexts.Check`) has its pin follow
    it into the entry's text; the digest is taken again once, last, with a line in its comment saying which
    descriptions T-207 changed.
  - **`GraphSlotToolTests.The_descriptions_this_task_lengthened_stay_under_claude_codes_cut`** is removed, superseded
    by the new test (which is stricter and covers its two tools).
- **README.md**: the "Use" section gains a short paragraph: each tool's description is complete enough to use it and
  stays under Claude Code's cut; `help {"tool":"render"}` (and the other entries) gives the rest; `help {}` lists them.
  Where a later section speaks of a detail that moved (render's looks, export's limits file, the .carve format), it
  says `help` has it. **[alt]** the paragraph says only that descriptions are kept under the cut.
- **CLAUDE.md**: a Working rule: a tool description of carve or patina is at most 3,686 characters (Claude Code cuts at
  4,096); detail goes to `help` (carve) or `nodes` (patina), held by `HelpToolTests` and `NodeToolTests`. The Layout
  rows for `src/Carve.Server` and `tests/Carve.Server.Tests` name `HelpTool` and `HelpToolTests`.
- **The split** (three agents, about 60 calls each; or two, with B and C merged):
  - **A, first: the mechanism and render.** Owns `HelpTool.cs`, `CarveServer.cs` (the one registration line),
    `HelpToolTests.cs`, the `Examples` overload in `FileToolTests.cs`, `RenderTool.cs` (`Text`, `Reference`,
    `RenderPartTexts`) and the render tests (RenderDefinitionTests, CheckerArgumentTests, the render reads in
    UvToolDefinitionTests, HoledFace*), `README.md` and `CLAUDE.md`. Lands the help tool with render's entry before B
    and C start, and takes the `PartToolsListTests` digest last, after B and C.
  - **B: export, save, open, bake.** Owns `ExportTool.cs`, `SaveTool.cs`, `OpenTool.cs`, `BakeTool.cs` and their
    tests (ExportToolDefinitionTests, ExportPartsToolTests, SaveOpenToolDefinitionTests, FileToolTests' save/open
    reads, GraphSlotToolTests, BakeToolTests, BakeNormalToolTests); adds four lines to `HelpTool`'s table.
  - **C: check, select and the five near the cut.** Owns `CheckTool.cs`, `SelectTool.cs`, `PartTools.cs`
    (`PartTexts.Check` only), `DefineMeshTool.cs`, `GetMeshTool.cs`, `MaterialTools.cs` (define_material's text only),
    `ProjectUvsTool.cs`, `UnwrapTool.cs` and their tests (CheckToolDefinitionTests, CheckToolTests,
    SelectToolDefinitionTests, SelectToolTests, DefineMesh*, MaterialToolDefinitionTests, ProjectUvsToolDefinitionTests,
    UnwrapToolDefinitionTests, the get_mesh reads in EditToolDefinitionTests and TargetToolTests); adds two lines to the
    table.
  - Shared lines that only add (`HelpTool`'s table, `PartToolsListTests`' pins) are kept on both sides; no agent edits
    another's tool file.
- **Done when:**
  - `HelpToolTests`' length test passes with no tool exempt: every carve description at most 3,686 in `tools/list`;
  - `help {}` and `help {"tool": T}` answer for render, export, bake, check, save, open and select, and refuse the rest
    in words that list the entries;
  - each of the seven descriptions ends with the pointer sentence, and every M-29 graph-material sentence is still in a
    description (not only in `help`);
  - every worked example that was run as written still is, from the description or from `help`'s entry;
  - `PartToolsListTests` passes with the digest taken again and its comment saying why;
  - `README.md` and `CLAUDE.md` say what is above;
  - `cairn task T-207 verify` passes (`server-unit`).

## Notes

- **Added to M-29 by the person on 2026-11-24,** to land before T-168, whose fresh agents work from the descriptions
  alone. Asked whether to cut the descriptions down inside M-29, file it off-milestone or leave them, the person chose a
  new member before T-168.
- **Found by:** T-160 (`save`, `open`), T-163 (`render`), T-164 (`export`) and T-166 (`check`), each of which added
  the sentences its spec asked for to a description already over the cut.
- **Precedent:** T-170 did this for patina (an index in `define_graph`'s description, the detail served by `nodes`).
- **Waits on T-165 and T-166,** which are still changing descriptions.
- **Planned by Claude Opus 5.5 (`claude-opus-5-5`), 2026-11-24.**
- **Measured** on `milestone/M-29` at `edb34ba` (before T-165 and T-166): a Release build of `src/Carve.Server` to
  `%TEMP%\t207\` (`--artifacts-path`, nothing written in the worktree), `initialize` then `tools/list` over stdio, the
  lengths of the served text (Python `len`, the same as `string.Length` here: no character outside the BMP). 42 tools,
  144,001 description characters in all. The server declares only `tools` and `logging` (no resources, no prompts, no
  `instructions`).

  | Tool | Description | Against 4,096 / 3,686 | Parameter descriptions (largest) |
  |---|---:|---|---|
  | render | 19,122 | over the cut | 4,131 (targets 427) |
  | export | 14,015 | over the cut | 1,170 (mode 262) |
  | bake | 8,410 | over the cut | 1,237 (maps 344) |
  | check | 7,565 | over the cut | 607 (targets 427) |
  | save | 6,286 | over the cut | 319 (mesh 220) |
  | open | 5,617 | over the cut | 837 (mesh 586) |
  | select | 5,571 | over the cut | 299 (mesh 180) |
  | define_mesh | 4,032 | under the cut, over the budget | 743 (name 237) |
  | get_mesh | 3,983 | under the cut, over the budget | 424 (mesh 180) |
  | define_material | 3,961 | under the cut, over the budget | 809 (base_colour_texture 116) |
  | project_uvs | 3,809 | under the cut, over the budget | 760 (mesh 180) |
  | unwrap | 3,720 | under the cut, over the budget | 503 (mesh 180) |
  | define_target | 3,533 | under | 906 (positions 251) |
  | knife | 3,255 | under | 309 |
  | the other 28 (below) | 2,912 to 262 | under | render_glb 1,194; the rest under 700 |

  The other 28: subdivide 2,912, define_clip 2,789, define_part 2,692, loop_cut 2,541, plane_cut 2,476, merge_faces
  2,455, bridge 2,405, extrude 2,343, mark_edges 2,334, move 2,309, set_uvs 2,148, set_material 1,927, get_parts 1,917,
  define_skeleton 1,904, delete 1,891, set_weights 1,848, get_weights 1,761, undo 1,714, weigh 1,632, render_glb 1,581,
  set_colour 1,390, keep_level 1,377, set_shading 1,157, get_skeleton 1,121, define_group 886, delete_target 686,
  get_clips 664, delete_clip 262. `select` and `bake` were not in the Goal's list and are over the cut too. render
  is 19,122 now (T-163 lengthened it).
- **What the seven over the cut hold, by kind** (approximate characters, from the served text split into sentences):
  - **render 19,122:** what it does and what is drawn 0.8k; camera arguments (views, direction, zoom, target,
    projection, size) 2.3k, of which caption formats 0.6k; shaded with materials and graph bakes 1.2k; wireframe and
    normals 1.0k; checker 1.8k (its cube worked example 0.55k); uvs and image 2.0k; highlight 1.1k; labels 0.9k; views
    sheets 1.5k; light and winding 0.4k; pose, targets, bones, weights, clip 1.5k; parts and levels 0.9k; **errors
    3.7k** (a 1,949-character list plus each feature's own). One paragraph, no line breaks; no `Example:` line.
  - **export 14,015:** purpose and arguments 0.25k; what is written (nodes, materials, UVs) 2.6k; axes, units and paths
    0.5k; parts 1.9k (rover example 0.4k); skinned 1.5k; game limits and their file 1.6k; result line 1.4k; clips mode
    1.0k; piece 0.9k; targets 0.25k; graph materials 0.55k; errors 1.1k; worked examples 0.9k.
  - **bake 8,410:** purpose and layout 0.5k; arguments 0.7k; the maps 3.6k; result 0.8k; errors 1.1k; worked example
    0.6k plus the `Example:` 0.3k.
  - **check 7,565:** purpose, arguments, result 0.8k; problems and fixes 1.0k; morph targets 1.0k; mesh warnings 1.0k;
    UV warnings 1.8k; material and envelope warnings 0.85k; notes and stats 0.85k; example and output 0.33k.
  - **save 6,286:** purpose, whole vs one mesh, .carve vs .glb 0.9k; materials, targets, versions 0.75k; the format
    and the cube file listing 1.2k; part tree 1.25k (example 0.36k); graph slots 0.3k; paths 0.25k; errors 0.5k; result
    0.5k; the five-step workflow 0.43k.
  - **open 5,617:** purpose, whole vs one mesh, version 1 0.73k; undo 0.45k; materials and graph slots 0.85k; ids,
    history, which file to keep 0.48k; paths 0.15k; result 0.37k; **errors 1.27k**; part tree 1.1k (example 0.32k).
  - **select 5,571:** purpose 0.33k; grammar 0.67k; properties by kind 1.33k; comparisons 0.27k; result and use 0.73k;
    errors 1.25k (twelve sample messages 0.77k); part tree 0.3k; worked examples 0.75k (the top-face one twice).
  - Across all seven, **errors are about 10k of the 66k**, and each error already names itself when returned, so they
    are the cheapest to move; caption formats, colour and channel tables, file formats and second worked examples are
    the next.
- **Tests that read description text.** `FileToolTests.Description` and `FileToolTests.Examples` (also reached as
  `ToolList.Examples`): `Examples` takes every line of a description that starts with `Example: `
  (`MeshToolSupport.ExampleMarker`), and about 40 tests run those as written; `SelectTool.ResultMarker` (`Result: `)
  and check's and get_mesh's `Output: ` are compared with what the call returns. `FileToolTests` also reads define_mesh's
  `Worked example (a unit cube): `. `PartToolsListTests` pins a SHA-256 of every pre-M-9 tool's whole entry (taken
  again by T-132, T-95, RV-71, T-160, T-164) and pins `PartTexts.*` and `RenderPartTexts.Parts` sentences once each.
  `GraphSlotToolTests` holds define_material and get_mesh under 4,096 and says save, open and render are not held.
  The files and their counts are in Scope.
- **How an agent reaches the detail, the options:**
  - **A help tool** (recommended): one tool taking a tool name. Costs one small tool file, a table, a test class, a
    tools/list digest retake. Reachable under T-168's conditions (`--tools ""`, `--allowedTools "mcp__carve__*"
    "mcp__patina__*"`), since it is a carve tool. **T-170's evidence:** patina's `nodes` (same shape) was the re-run
    agent's first call, `nodes {}`, and the session made 10 calls with no probing against round 1's 25 with three
    deliberately wrong graphs. That is one session; no later round is recorded calling or ignoring it.
  - **MCP resources or prompts:** carve declares neither today. Claude Code reaches resources only through its own
    built-in tools (resource listing and reading) or a user's `@` mention, and prompts only as a user's slash command;
    T-168 runs with `--tools ""`, which removes the built-ins, so an agent there could not read a resource at all.
    Not workable for T-168, and nothing shows agents reading them unprompted.
  - **Detail in errors and results:** already true for errors (each says what is wrong), which is why errors can
    leave first; but formats an agent needs before the call (the limits file, the .carve layout, caption and colour
    codes) cannot wait for a failure.
  - **Shorter writing, no second place:** enough for the five near the cut; for render and export it means losing
    about 15k and 10k characters an agent can reach today (only by a client that does not cut), including the limits
    file format and the uvs look's reading.
- **Verify:** `server-unit` (as the header has it). Tool text and a tool's answers are `ToolHarness` territory; no
  transport, rendering or export bytes change, so `server-regression` is not needed (the milestone's landing runs it).
  No scenario reads carve's tools/list as a whole (`AcceptanceScenario` calls tools by name;
  `ToolFamiliesThroughProcessTests` runs families, not the whole list), so no scenario suite is added.
- **Not established:**
  - **Whether Claude Code cuts parameter descriptions** in `inputSchema`. T-170 measured only tool descriptions. In
    this planning session (Claude Agent SDK, tools loaded on demand by tool search) `open`'s published-copy entry
    arrived whole, its description well over 4,096 (about 5,300) and `mesh`'s 586-character parameter description
    intact: so the cut is not applied on every path, and `claude -p` with tools loaded up front (T-170's probe and
    T-168's conditions) is the one measured. No parameter description is near any plausible cut, so none is budgeted.
  - **Whether the cut is still 4,096** in the Claude Code version T-168 will run; T-170 measured 2.1.329. Not probed.
  - **T-165's and T-166's final sentences**; the figures above are before them.
  - Characters, not tokens: the cut is by characters per T-170; the tool's result size for a 15k `help` entry was not
    checked against Claude Code's limit on an MCP result (well under its documented default by tokens).

### For the person to decide

1. **Does carve gain a tool, and what is it called?** Recommended: **yes, `help`**, `help {"tool":"render"}` for a
   tool's reference and `help {}` for the list, the seven over-the-cut tools having entries. `help` is the name an
   agent guesses; `guide` or `reference` work as well. Patina's `nodes` stays as it is (it is a node catalogue, not
   help on tools). **If declined:** the descriptions are cut to the budget by shorter writing alone; what does not fit
   is lost to agents (errors still name themselves; formats such as export's limits file and the .carve layout go to
   `README.md` only, which an agent under T-168's conditions cannot read); `HelpTool`, `HelpToolTests`' help checks
   and the `Reference` constants are not built; every test that reads a moved example has to be rewritten to hold the
   behaviour without the text, and that is a loosened check to bring back to the person for each one (Working rules).
2. **The margin:** 10% under the cut (3,686), as for patina. Recommended: yes, one number for both servers. If
   declined (say, the cut itself, 4,096): the five near the cut need no change and the seven trim less.
3. **The five between 3,686 and 4,096** (define_mesh, get_mesh, define_material, project_uvs, unwrap): recommended,
   trimmed in this task, as T-170 trimmed swatch and export. If declined, the test exempts them by name, which T-170's
   ruling ("no tool exempt") argues against.
4. **What a `help` entry holds:** recommended, only what left the description (the agent has read the description).
   The alternative, the whole description again followed by the detail, is simpler to read and costs nothing in the
   cut; say if you prefer it.
5. **Re-measure the cut before building?** Recommended: no separate probe; the 10% margin covers a small change, and
   T-168's first round records whether any description arrived cut. If you want it measured: one `claude -p` probe as
   T-170's (quote the last 150 characters of each carve description, no tool call), about one session's cost.

**Decided (2026-11-24).** By the person: carve gains the tool, named `help`. By the orchestrator, following patina's
T-170: the 10% margin (3,686), the five descriptions near the cut trimmed in this task, a `help` entry holding only
what left its description, and no separate probe of the cut before T-168's first round.

### Hand-off

Agent A, on Claude Sonnet 5.5 (`claude-sonnet-5-5`). Done: `help` tool (`src/Carve.Server/HelpTool.cs`, registered after `bake`),
`render` trimmed (3,564 characters, was 19,122) with `RenderTool.Reference` (20,126 characters), `HelpToolTests`, the
`FileToolTests.ToolList.Examples(string)` overload, the pins that followed render's text (`CheckerArgumentTests`,
`RenderDefinitionTests` read `HelpToolTests.WithReference(description, "render")`; `PartToolsListTests` pins render's
`Parts` paragraph once in the entry), `GraphSlotToolTests`' superseded length test removed, tool count 43. `server-unit` passes.

- **Add an entry (B, C):** (1) on the tool's class `public const string Reference = ...` holding the sentences that
  leave the description, word for word (move whole source lines; do not retype); (2) one line in `HelpTool.Entries`
  (tool, what it covers, the Reference): `help {}` and the tool's own description list from that table; (3) end the
  trimmed description with `More: help {"tool":"<name>"} gives <what moved>.`; (4) delete the tool from
  `HelpToolTests.Pending`. The test `Every_description_is_under_the_limit_but_the_tools_still_to_trim` then holds it
  at 3,686 and fails if a pending tool is already under. A tool that needs a Reference must be reached through
  `carve::Carve.Server.HelpTool.Entry(name)` in tests (the test project aliases the server as `carve`).
- **Tests that read moved text:** read `HelpToolTests.WithReference(Description(tools, name), name)` (description plus
  entry) so every pin keeps holding wherever the sentence is; a worked example in an entry is a line starting
  `Example: `, run with `ToolList.Examples(string)`. Pins to `PartTexts.*` that move: check `PartToolsListTests`.
- **Digest:** `PartToolsListTests.Before` was retaken now (render and `help`, which joins `NewTools`) because
  `server-unit` must pass at each commit; the Scope's "once, last" is not possible with three agents. B and C each
  retake it for their tools (run the test, copy the printed digest), the closer takes the final one and the comment.
- **Measured now (`tools/list` as served, after T-165/T-166):** render 3,564 (done); over 3,686 and in `Pending`: bake 8,684,
  check 8,310, export 14,015, save 6,286, open 5,676, select 5,571, define_mesh 4,032, get_mesh 3,983, define_material
  4,059, project_uvs 3,809, unwrap 3,720. help is under 1,000.
- **Render sentence check:** a script (`%TEMP%\t207_diff.py`) parsed the old and new string literals and split into 263
  sentences: every old sentence is word for word in `Reference` (two reported misses were splitter artefacts: the
  opening sentence's tail and a sentence joined to the next without a space; both are in the entry). Kept in the
  description, the graph-material sentences: the slot's evaluation sentence word for word, the `baked:` sentence with
  "to bake: lower" shortened to "(lower ...)", and T-165's graph `image` as the shortened clause plus the
  `Example: {"mode":"uvs","image":{"graph":...}}` line (which `GraphImageToolTests` still finds). The entry restates
  the opening sentence and `mode` list whole, so the first sentence names every edit as `EditToolDefinitionTests` reads.
- **Result size:** a 20,126-character reply is about 5,000 tokens; as I recall, Claude Code warns at 10,000 tokens and
  caps an MCP result at 25,000 by default (`MAX_MCP_OUTPUT_TOKENS`). Not checked against the docs in this run, so no
  entry is split by topic. The entry is whole-description-sized, not trimmed, in the sense that lines kept verbatim in the
  description are not repeated, but the tightened ones are kept in full.
- **Left for B:** export, save, open, bake. **For C:** check, select, define_mesh, get_mesh, define_material, project_uvs,
  unwrap. **For the close:** `README.md` (Use paragraph and the sections that speak of moved detail), `CLAUDE.md` (the
  Working rule and the Layout rows naming `HelpTool` and `HelpToolTests`), the final digest, `verify`, `### As built`.
  Not done by A: README and CLAUDE.md edits.

---
description: >-
  A graph declares named parameters that drive its node parameters, and swatch and export take overrides, so one graph gives variants; the bricks sample exposes mortar, tint and jitter
---
# T-139 — Exposed graph parameters

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-134, T-136

**Verify:** patina-unit, scenario-patina

## Goal

A graph names a few parameters that matter (mortar width, tint, wear amount), each driving one or more of its nodes'
parameters, so one graph gives variants. An agent sets them in one edit, and sees a variant in `swatch` or writes it
with `export` without editing the graph.

## Scope

- **The format** (version 2, through T-134's switch): a top-level `parameters` object, by name (`[a-z][a-z0-9_]*`):

  ```
  "parameters": {
    "mortar": { "value": 0.04, "drives": ["layout.gap", "ao.in_high"] },
    "tint":   { "value": "#a04a32", "drives": ["brick.colour"] } }
  ```

  - `drives` names node parameters as `"<node>.<param>"`. They must all be of one kind. The exposed parameter takes
    that kind and the narrowest range of the parameters it drives: the intersection, or for an enum the common choices.
    Optional `min` and `max` narrow it further.
  - `value` is checked against that range and is the value every driven parameter takes. A driven parameter's own
    value in its node is ignored while it is driven, so `get_graph` shows it as driven (`"gap": {"driven_by":
    "mortar"}`) and the canonical writer leaves it out.
  - A graph without `parameters` is written as version 1, byte for byte as before.
- **Faults**, each naming the parameter, in `GraphValidator`'s words: an unknown node or parameter in `drives`; a kind
  mismatch between two driven parameters; an empty range; a value out of range ("parameters['mortar']: is 0.5, but must
  be a number from 0 to 0.2"); a parameter driven twice.
- **The tools**:
  - `edit_graph` gains `set_parameter` (`name`, and `value`, `drives`, `min` or `max`: creates or changes one),
    `remove_parameter` (`name`: the driven parameters keep its value as their own) and `set_parameters`
    (`values`: several by name in one operation). They are all or nothing, as the other operations are.
  - `get_graph` lists `parameters` with each one's kind, range, value and what it drives.
  - `swatch` and `export` take an optional `parameters` object of overrides. The graph is not changed; an override out
    of range is a tool error naming the parameter and its range. The swatch sheet's title names the overrides.
  - Each description names this with a worked example that its tests run as written.
- **Tests (unit)**: the round trip (version 1 untouched, version 2 bit-exact); every fault; the range intersection
  worked by hand (a `float` 0..0.45 and a `float` 0..1 give 0..0.45); an override at swatch and export equal to the same
  value set in the graph, byte for byte; `remove_parameter` keeping the value; a table of broken copies (a driven
  parameter not substituted, the narrowest range not taken) and what caught each.
- **Content**: `samples/graphs/bricks.texgraph` (T-136's) exposes `mortar` (the gap), `tint` (the brick colour) and
  `jitter`, at its current values, so its PNGs keep their bytes (a test says so). A variant, mortar 0.08 and tint
  `#7a7a70` (grey stone bricks), is written by `tools/Patina.SampleGraphs` into `samples/graphs/bricks/variants/grey/` from
  a variant list in the tool, and kept equal by a test. scenario-patina draws both as sheets, every pixel worked from
  texels, and the person approves the variant's look.
- D-7's Open questions gain the rule, marked answered at M-28's planning: a graph's parameters are declared, drive node
  parameters by name, and are set on a `graph` node by name (T-140).

## Notes

- **Decided at planning (2026-11-19, by the person):** declared parameters that drive node parameters, set by name on a
  `graph` node in another graph (T-140), with `swatch` and `export` overrides for variants. The alternatives, `"$name"`
  references in node parameters and one `values` object on the graph node, were declined.
- Decides a format and the API T-140 and T-141 build on: Opus.
- **Write down for T-140 and T-141** (As built): how a graph is resolved with overrides (the one function both call);
  the fault wording; the version 2 field order.
- `check` (T-141) takes the same `parameters` overrides through the function above.
- Owns `TexGraphFile.cs`, `GraphValidator.cs`, `EditGraphTool.cs`, `FileTools.cs` (get_graph) and the overrides in
  `SwatchTool.cs` and `ExportTool.cs` in wave 3. T-137 and T-138 run beside it and touch none of them.
- **Stop and ask:** a rule the spec does not settle (an exposed parameter driving a list kind, say), or a committed
  PNG that changes.
- **Ruled (2026-11-20, by the person)**, the three the spec left open:
  - a graph parameter that drives a list (`Floats`, `Colours`) or a `Name` stays a fault;
  - an `IntPair` may be driven, with one range for both components;
  - a whole-number range narrowed by a non-whole `min` or `max` rounds inward.

### As built

Built by Claude Opus 5.5 (`claude-opus-5-5`), in two agents: the first wrote the format, faults, tools and content and
handed off at its call checkpoint; the second rebased onto M-28 (T-134's and T-153's Layout lines kept beside this one),
wrote the scenario stage, fixed the sheet title, and took the review.

**For T-140 and T-141: the one function.** `GraphParameters.WithOverrides(graph, overrides, registry)` (`src/Patina.Graphs/GraphParameters.cs`)
takes a `TextureGraph` and overrides by name (`IReadOnlyDictionary<string, ParamValue>`) and returns
`OverrideResult(Graph, Error)`: the graph with each override set as the parameter's value, or an error naming the
parameter and its range (`parameters['mortar']: is 0.5, but must be a number from 0 to 0.2`; an unknown name lists the
parameters). It does not change the graph given. `GraphParameters.Of(graph, registry)` gives each parameter not at fault
as `ExposedParameter(Name, Kind, Range, Value, Drives)`; `Range` is a `ParamSpec` named after the parameter, so its
`Check` and `Describe` give the words. A `graph` node (T-140) should read its settings by name and call `WithOverrides`
on the inner graph; `check` (T-141) should take its `parameters` argument through
`ParameterOverrides.TryApply(tool, graph, JsonElement?, registry, out graph, out overrides, out error)`
(`src/Patina.Server/ParameterOverrides.cs`), as `swatch` and `export` do, and name the overrides with
`GraphParameters.Describe(overrides)` (`mortar 0.08, tint "#7a7a70"`).

**Format** (version 2 through T-134's `TexGraphFormat`: `Current = Version1.With(ParametersField.Instance)`): `parameters`
is written after `seed` and before `nodes`, one parameter a line in name order, keys in the order `value`, `drives`,
`min`, `max`, the last two left out when absent; `drives` in the order given. A driven node parameter is left out of its
node by the writer; `TexGraphFile.WriteShowingDriven` is get_graph's form (`"gap": { "driven_by": "mortar" }`), and `Read`
takes it back (a `driven_by` naming a parameter that does not drive it is a load error). A graph without parameters is
written as version 1, byte for byte as before. The exposed parameter's range is the intersection of the driven
parameters' ranges (an enum's common choices), narrowed by `min` and `max`; the person's three rulings are in Notes.

**Faults** (`GraphParameters.Analyse`, appended by `GraphValidator` after the node faults; `GraphFault` gained a fifth field,
`GraphParameter`, with `Node` null). Each starts `parameters['<name>']: `; the full table is
`ExposedParametersTests.FaultCases`: an unknown node or parameter (listing what there is), a drive not `"<node>.<param>"`,
an empty `drives`, two kinds (`drives 'layout.gap', a number, and 'paint.colour', a colour; ...`), an empty range, a value
out of range, a node parameter driven by two parameters or named twice, `min`/`max` on a kind without order, a bad name,
and a list or `Name` driven. A driven node parameter takes the parameter's value and its own value in the node is not
checked.

**Tools:** edit_graph `set_parameter`, `remove_parameter` (the driven parameters keep the value as their own) and
`set_parameters`, all or nothing; get_graph lists each parameter's kind, range, value and drives, and shows driven node
parameters as `driven_by`; swatch and export take `parameters` overrides (the sheet's title and caption, the sphere's
caption and export's first line name them). Each description's worked example is run as written by
`ExposedParameterToolTests`.

**Sheet title** (found at review): a title naming overrides ran past the 512 px a cell's label takes and was cut there.
`LabelAtlas.TitleWidth` lets the title row draw to the atlas's width; a title that fits draws the same pixels (all 22
existing frames and PNGs equal by hash). Unit test: `ExposedParameterToolTests.LongTitleIsNotCutAtACellLabelsWidth`.

**Content:** `samples/graphs/bricks.texgraph` is version 2 exposing `jitter`, `mortar` and `tint` at their old values; every
committed PNG kept its SHA-256. `tools/Patina.SampleGraphs` has a variant list and writes
`samples/graphs/bricks/variants/grey/` (mortar 0.08, tint `#7a7a70`); a unit test holds it equal. D-7's open question is
added as answered; CLAUDE.md's Patina Layout line names `GraphParameters`.

**Scenario** (`SwatchStage.BricksAsync`): the bricks opened, exported at 256 with and without the grey overrides, each equal
byte for byte to the committed PNGs; frames `swatch-bricks-sheet` and `swatch-bricks-grey-sheet` (1544 x 538), every flat
and tiled cell checked against the exported texels; the grey caption names the overrides and the plain one does not; the
grey title has text past x 560. The break, made in the scenario: the grey sheet judged against the plain bricks' texels
must fail its base colour flat and tiled cells (logged to `swatch-checks.tsv`).

**Checked:** patina-unit and scenario-patina pass through `cairn task T-139 verify`. The frames, compared by hash against
job `J-20261120-063443-d8a47b` (scenario-patina on M-28's tip, Mantorok) and this branch's job
`J-20261120-064049-481cfd` (Xellotath): the 22 existing frames and PNGs are equal on both machines; the only frames
added are the two bricks sheets. **The person approved the grey variant's look** (Waystone, round 1, 2026-11-20).

**Surprising:** `ExportToolTests.Other_tools_answer_while_an_export_runs` timed out once on Xellotath (a call cancelled
at 39 s) and passed when run again on the same runner. It is a timing test, and nothing in this member touches it.

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

### Hand-off

Written by Claude Opus 5.5 (`claude-opus-5-5`), the first agent, at its call checkpoint (about 58 calls). One commit.

**Done (committed, unit tests green: Patina.Graphs.Tests 240 pass, Patina.Server.Tests unit 112 pass):**
- *Format*: `GraphParameter` (value, drives, optional min/max) and `TextureGraph.Parameters` (an `init` slot; `WithParameter`,
  `WithoutParameter`, `DriverOf`). `ParametersField` (in `GraphParameters.cs`) is added to `TexGraphFormat.Current`
  (`Current = Version1.With(ParametersField.Instance)`; `TexGraphFormat.Version1` is the bare format). Written after
  `seed`, one parameter a line in name order, keys `value`, `drives`, `min`, `max` (the last two left out when absent); drives
  in the order given. A driven node parameter is left out of its node by the writer. `TexGraphFile.WriteShowingDriven`
  is get_graph's form (`"gap": { "driven_by": "mortar" }`), and `Read` takes it back (a `driven_by` naming a parameter that
  does not drive it is a load error).
- *Faults* (`GraphParameters.Analyse`, appended by `GraphValidator` after the node faults): `GraphFault` gained a fifth
  field, `GraphParameter` (Node null). Wording is in `ExposedParametersTests.FaultCases`. A driven parameter takes the value
  (where it fits that node parameter's own range) and its own value in the node is not checked.
- *The one function*: `GraphParameters.WithOverrides(graph, overrides, registry)` -> `OverrideResult(Graph, Error)`;
  `GraphParameters.Of(graph)` gives each parameter not at fault as `ExposedParameter` with `Range`, a `ParamSpec` named after
  it (its `Check`/`Describe` give the words). Server side, `ParameterOverrides.TryApply(tool, graph, JsonElement?, registry, ...)`
  reads the `parameters` argument; swatch and export call it, check (T-141) should too.
- *Tools*: edit_graph `set_parameter`, `remove_parameter`, `set_parameters` (worked example and shapes in its text, run by
  `ExposedParameterToolTests`); get_graph's parameter lines (`GetGraphTool.WorkedBricksParameters`) and driven form; swatch
  and export `parameters` (title, caption and export head name the overrides). The edit's new-fault key includes `GraphParameter`.
- *Content*: bricks.texgraph is version 2 with `jitter`, `mortar`, `tint` at their old values; every committed PNG kept its
  SHA-256 (all 20 compared before and after the sample tool ran). `tools/Patina.SampleGraphs` has a variant list and wrote
  `samples/graphs/bricks/variants/grey/` (mortar 0.08, tint #7a7a70); a test holds it equal.
- D-7's open question added as answered; CLAUDE.md's Patina Layout line names `GraphParameters`.
- Test targets moved only where the spec moves them: swatch's and export's argument lists name `parameters`; the
  stand-in field test builds on `TexGraphFormat.Version1` and the unknown-key message lists `parameters`;
  `PatinaHarness.GraphText` skips get_graph's parameter lines.

**Left:**
1. scenario-patina: a bricks stage (after `MetalPlateAsync` in `SwatchStage.cs`): open the bricks, export at 256 with and
   without `{"mortar":0.08,"tint":"#7a7a70"}`, check each against the committed PNGs, capture `swatch-bricks-sheet` and
   `swatch-bricks-grey-sheet` (4 columns, 1544 x 538) and check every flat and tiled cell against the exported texels as the
   plate's sheet is checked; check the grey sheet's caption names the overrides. Nothing written there yet.
2. `cairn task T-139 verify`; compare the existing 10 planks frames and 16 PNGs by hash against main's job (fetch by job id).
3. Review round 1 with the person (Waystone, `G:\My Drive\artifacts\carve\M-28\T-139\round-1`): the grey variant's look
   (its sheet, scaled), which must be approved before landing, and two rulings below.
4. `### As built` (with the T-140/T-141 notes above), commit, `cairn lint`, `cairn task T-139 finish`.

**For the person to rule (the spec does not settle them):** a graph parameter may not drive a list (`Floats`, `Colours`)
or a `Name` (a fault says so); `IntPair` (`[x, y]`, one range for both) may be driven. An `Int` range narrowed by a
non-whole min/max rounds inward.

**Not checked:** no scenario run, no frame seen, no image opened.

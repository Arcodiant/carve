---
description: >-
  patina's check lists texture faults in the open graph's outputs or in PNG files: seams, base colour out of range, metallic not near 0 or 1, normals not of unit length, and an output of one value
---
# T-141 — The check tool

**Status:** In Progress

**Pri:** P1

**Milestone:** M-28

**Batch:** — (standalone)

**Depends on:** T-134, T-137, T-138, T-139

**Verify:** patina-unit

## Goal

An agent measures what it made, rather than judging it only from renders (D-7). `check` lists what is wrong with each
output, as carve's `check` lists mesh faults, with the fix in words, and never refuses anything.

## Scope

- **The rules**, in `Patina.Graphs` (`Checks/`, engine-free, so carve can run them on graph materials in M-29), on a
  role's evaluated `float`s for a graph, and on decoded bytes for a PNG. Each rule is a class, registered in one list
  (never switched on):
  - `base-colour-range`: a non-metal texel (metallic below 0.5, or no metallic) whose brightest channel is below 20 or
    above 245 in sRGB levels; a metal texel (metallic 0.5 or more) whose brightest channel is below 120. It is a fault
    when over 1% of texels are out. The message gives the share, the worst texel's colour and place, and the fix.
  - `metallic-not-binary`: over 5% of texels between 0.1 and 0.9.
  - `normal-not-unit`: over 0.1% of texels with |length - 1| > 0.02 (0.03 for a PNG, for the 8-bit rounding).
  - `one-value`: a greyscale output (roughness, metallic, occlusion, height, or a greyscale named output) whose texels
    all pack to one byte; "a mask of one value masks nothing".
  - `does-not-tile`: across the wrap in u or v, the mean step between the last and first column (or row) is greater than
    twice the interior mean step plus 2 levels.
- **The tool** `check`, in `src/Patina.Server/CheckTool.cs`, one line in `Program.Tools`:
  - arguments: `size` (default the graph's own; a power of two up to 2048), `parameters` (T-139's overrides), and
    `files` (a list of `{path, role}` for PNGs, resolved under the tool folder; with `files`, only those are checked, and
    no graph is needed);
  - result (approved at planning, below): `{ok, problems:[{check, output, message}], notes, stats}`. `ok` is true when
    there are no problems. stats gives each output's min, max and mean per channel;
  - a graph with faults is a tool error naming them, as `swatch` refuses; it changes nothing.
- **Description**, in carve's `check`'s manner: what each rule means for a game, and its fix, with two worked examples
  (a clean graph, and one with metallic 0.5 everywhere). The description tests run both as written.
- **Tests (unit)**:
  - each rule on hand-made images just inside and just outside its threshold, both for a graph and for a PNG;
  - a table of broken copies of each rule (the threshold's side, metal texels judged as non-metal, the wrap not
    compared) and what caught each;
  - **every committed sample graph checks clean**: a test over `samples/graphs/*.texgraph` (with each graph whose inputs
    all have defaults), and over the bricks variant. A committed sample that check flags is a stop, not a change to the
    rule. T-140's grime and old wall, landing beside this task, are covered by the same glob once both are in;
  - M-7's `samples/textures/*.png` checked with their roles, with the result written into this file. A problem there
    is reported to the person, not fixed here.
- **Content**: `samples/graphs/faults/` holds two small planted graphs that check flags and the description's second
  example uses: `chrome-in-paint.texgraph` (metallic 0.5) and `flat-mask.texgraph` (a named mask clipped to one value).
  Both are excluded from the clean test by folder, and T-143 can draw its plant from them.

## Notes

- **Decided at planning (2026-11-19, by the person):** the rules and thresholds above and the report as sketched; check
  takes the open graph and, optionally, PNG files; it reports and never refuses. The base-colour floor is 20, not the
  common 30, because 12.5% of the approved planks' texels (the gaps) have a brightest channel below 30 and none is below
  20. The planks stay as they are (M-27's ruling).
- **The report, as approved:**

  ```
  check {}          (open graph, its own size; optional size<=2048,
                     parameters, files:[{path, role}])
  {"ok": false,
   "problems": [
    {"check":"base-colour-range", "output":"base_colour",
     "message":"3.2% of texels are outside 20..245 (brightest channel), worst #0a0806 at (12, 40); over 1% is a fault"},
    {"check":"metallic-not-binary", "output":"metallic",
     "message":"41% of texels are between 0.1 and 0.9; over 5% is a fault"},
    {"check":"normal-not-unit", "output":"normal", ...},
    {"check":"one-value", "output":"wear",
     "message":"every texel is 1.000 (255); a mask of one value masks nothing"},
    {"check":"does-not-tile", "output":"base_colour", ...}],
   "notes":["metal texels (metallic>=0.5): 12%"],
   "stats":{"size":256, "outputs":{"base_colour":{"min":[..],"max":[..],"mean":[..]}, ...}}}
  ```

- Sonnet: the rules are settled. The messages' wording is the task's.
- Runs beside T-140, which owns the format, validator and `FileTools.cs`. This task adds its own files and one line in
  `Program.Tools`. A conflict that only adds is settled by keeping both sides.
- **Stop and ask:** a threshold that a committed sample fails, or any change to a rule's number.

### Hand-off

Written by Claude Sonnet 5.5 (`claude-sonnet-5-5`), stopped at about 22 tool calls on the planning stop-rule: a committed
sample fails a rule.

**STOP, for the person: `samples/graphs/planks.texgraph` fails `does-not-tile` on its `normal` output.** Every other
top-level sample (painted-metal, grime, fabric, bricks, old-wall, floor-tiles, cobblestones) and the bricks grey variant
check clean at their own sizes. The planks' normal: u wrap step 25.7 levels (mean over three channels) against an interior
mean of 3.5, over the threshold of 2 x 3.5 + 2 = 9. Cause measured: the normal's R channel has a stave edge at every 32nd
column pair (steps of 77, 35, 30 around columns 31, 63, ..., 255), and u = 0 is itself a stave edge, so the wrap step is
77 in every row, exactly what each interior edge has. The planks tile (they were looked at tiled in M-27); the rule, which
compares one wrap step with the *mean* interior step, reads any hard edge that falls on the wrap as a seam. The rule is
not changed here. Choices for the person: compare the wrap step against the interior steps' own distribution (for
example a high percentile or the largest edge step) instead of the mean; or accept the normal's edge on the wrap and
exempt it; or move the stave edge off the wrap in the sample.

**Done (uncommitted-to-finish; the branch is not finished):** the five rules as classes in `src/Patina.Graphs/Checks/`
(`CheckSet.cs`, `CheckRules.cs` with the one list `CheckRules.All`, and one file a rule), `src/Patina.Server/CheckTool.cs`
(arguments size, parameters, files; result `{ok, problems, notes, stats}` as agreed; StbImageSharp added to Patina.Server
to decode PNGs), one line in `Program.Tools` and the two tool-list tests, the two planted graphs in
`samples/graphs/faults/` (each raises only its one problem: chrome-in-paint a metallic of 0.45..0.55 over a base colour of
130..200, flat-mask a named `wear` clipped to one value), and `tests/Patina.Graphs.Tests/CheckSamplesTests.cs` (the sample
sweep, the bricks variant, the fault graphs, and the M-7 PNGs written to the test output).

**Left:** the person's ruling on does-not-tile above; the description's worked examples (`CheckTool.Worked*Result` are
placeholders to fill from a real run, with a test that runs each as written); the per-rule just-inside/just-outside tests
for a graph and a PNG and the broken-copy table (threshold side, metal judged as non-metal, wrap not compared); a tool
test over the harness; CLAUDE.md's Layout line; `As built`; `cairn lint`, `verify`, `finish`.

**M-7 PNGs, checked with their roles (to report, not fix):** end-grain-base + end-grain-normal: normal-not-unit 0.62%
(worst 0.033), does-not-tile base_colour (u) and normal (u and v). iron-base + iron-mr: metallic-not-binary 8.71%,
does-not-tile on base_colour (u 44.6 against 2.8, v 33.6 against 10.7), roughness and more (output cut off).
rune-emissive (as a named colour output): clean. staves-base + normal + occlusion: does-not-tile base_colour (v, 3.6
against 0.0 inside) and normal (u, 51.0 against 3.5), which is the planks' case again (an edge on the wrap) and a
flat-interior case: an interior step of 0 makes any wrap step over 2 levels a fault.

**Surprising:** the same rule would be flagged by anything whose edge falls on the wrap, which the staves-base case shows
is not rare.

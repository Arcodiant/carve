---
description: >-
  patina's check lists texture faults in the open graph's outputs or in PNG files: seams, base colour out of range, metallic not near 0 or 1, normals not of unit length, and an output of one value
---
# T-141 — The check tool

**Status:** Done

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

- **Rulings of 2026-11-20, by the person, after the stop on the planks:**
  1. **`does-not-tile` changes.** The agreed rule compared one wrap step with the mean interior step, so a pattern's hard
     edge falling on the wrap (the planks' stave edge at u = 0) read as a seam. The rule now measures the mean step (in
     packed levels, over the channels and all rows) at every column boundary, the wrap's included, and the same for rows in
     v; the wrap is a fault only when its mean step is greater than 2 x the 99th percentile (nearest rank) of the interior
     boundaries' mean steps, plus 2 levels. The message says the measured wrap step and the percentile it was judged against.
  2. **Four smaller rules accepted as built:** `metallic-not-binary` counts 0.1 and 0.9 as not binary; a roughness PNG's
     blue channel is read as metallic and is exempt from `one-value`; with `files`, `size` and `parameters` are ignored
     with a note; `does-not-tile` runs on every output in packed levels averaged over channels.
  3. Every other rule and threshold stays as agreed. A further sample that fails is again a stop.

### As built

Built by Claude Sonnet 5.5 (`claude-sonnet-5-5`) in two agents, both on that model: the first wrote the rules, the tool,
the planted graphs and the sample sweep and stopped on the planks failing `does-not-tile`; the second rebased onto
`milestone/M-28` (T-157 had landed), made the person's ruling above, and finished the tests, the worked examples and the
docs. **Status: done.**

- `src/Patina.Graphs/Checks/`: `CheckSet.cs` (`CheckOutput`, one output as floats, built from a graph's evaluator or from
  RGBA8 bytes; `CheckSet.OfGraph` and `OfPngs`), `CheckRules.cs` (the one list `CheckRules.All`, `Run`), and one file a
  rule: `BaseColourRangeRule`, `MetallicNotBinaryRule`, `NormalNotUnitRule`, `OneValueRule`, `DoesNotTileRule`. Engine-free.
- `src/Patina.Server/CheckTool.cs`: arguments `size`, `parameters`, `files` as agreed; result
  `{ok, problems:[{check, output, message}], notes, stats:{size, outputs}}`. A graph that does not validate is an error
  (as `swatch` refuses); one that evaluates is never refused. `files` decodes PNGs with StbImageSharp (added to
  Patina.Server), paths resolved under the tool folder. A note says metal texels' share ("metal texels (metallic 0.5 or
  more): 52%") and where a metallic was read from.
- `does-not-tile`'s message: `a seam in u (the step across the wrap is 9.0 levels; the 99th percentile of the steps inside
  is 3.0); ...`, plus the fix. With 32 columns the 99th percentile (nearest rank over 31 boundaries) is the largest
  interior step, so on small images the wrap is compared with the largest interior edge.
- `samples/graphs/faults/chrome-in-paint.texgraph` (metallic 0.45..0.55 over a base colour of 130..200, raises only
  `metallic-not-binary`) and `flat-mask.texgraph` (a named `wear` clipped to one value, raises only `one-value`).
- Tests: `CheckRulesTests` (38: every rule just inside and just outside for a graph and for a PNG; the wrap with a hard
  edge on it that tiles passes; a lone seam on a nearly flat and on a noisy image fails; the table of 14 broken copies,
  each with the case that tells it from the right rule, and a test that every rule has a row), `CheckSamplesTests` (every
  top-level sample and the bricks variant clean; the planted graphs; the M-7 PNGs written to the test output),
  `CheckToolTests` (the three worked examples as written through the harness and held to the description's text, the
  errors, `size`, `files` with its ignored-arguments note, a faulty graph refused and nothing changed).

**Every committed sample checks clean under the new rule**, the planks included: painted-metal, grime, fabric, bricks,
old-wall, floor-tiles, planks, cobblestones, and the bricks grey variant.

**M-7's PNGs, checked with their roles (reported, not fixed; they are not graphs):**
- `end-grain-base` + `end-grain-normal`: `normal-not-unit` on the normal, 0.62% of texels off by more than 0.03 (worst
  0.033). Its `does-not-tile` faults (base colour in u, normal in u and v) are gone under the new rule.
- `iron-base` + `iron-mr`: `metallic-not-binary` 8.71%; `does-not-tile` on `base_colour` (u: wrap 44.6 levels against a
  99th percentile of 5.2) and on `roughness` (u: 52.3 against 5.5). Before the change it also faulted in v and on more
  outputs; the two u seams that remain were not looked at by eye.
- `rune-emissive` (as a named output): clean.
- `staves-base` + `staves-normal` + `staves-occlusion`: `does-not-tile` on `base_colour` in v only (wrap 3.6 levels
  against a 99th percentile of 0.2), so a flat-interior image with a few levels' step over the wrap is a seam by the rule.
  The staves' normal, which faulted before, is clean now.

**For T-142 and T-143:**
- Call `check` after `define_graph` or `open`; `ok` and `problems` are the verdict, `stats` the measure. The faults to plant
  are the two graphs in `samples/graphs/faults/`; both raise exactly one problem, `chrome-in-paint` at the metallic 0.45..0.55
  (`metallic-not-binary`, 100% of texels) and `flat-mask` at `wear` (`one-value`, 1.000 (255)).
- The worked examples are in the description: bricks at 64 clean, chrome-in-paint (metal texels 52%), and PNG `files` for
  `samples/graphs/bricks/base_colour.png` and `normal.png` (paths relative to the tool folder, so the tool folder must be the
  repo root for that example).
- A `does-not-tile` fault on a graph with a real hard edge on the wrap does not appear; one on a smooth gradient over the wrap does.
- No engine need.

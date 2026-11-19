---
description: >-
  patina's check lists texture faults in the open graph's outputs or in PNG files: seams, base colour out of range, metallic not near 0 or 1, normals not of unit length, and an output of one value
---
# T-141 — The check tool

**Status:** Open

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

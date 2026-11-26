---
description: >-
  A way to take an absolute value or a difference in a texture graph, for thin lines such as veins and cracks, which today take two levels nodes multiplied
---
# T-185 — abs and difference in patina's maths

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

## Goal

A thin line (a marble vein, a crack in wood) is one node away from a noise: the distance of its value from a level. Today that takes two `levels` nodes multiplied together.

## Scope

- Decide the shape with the person before building: a `difference` mode on `blend` (the absolute difference of `a` and `b`), an `abs` about a level on `levels`, or a small maths node.
- Tests worked by hand, tiling and size-invariance as the other nodes have, and a sample graph that uses it (veined marble).

## Notes

- Raised by the stone-room project's feature request (2026-11-22, item 9).
- As of M-28's branch on 2026-11-22, `blend` has `mix`, `multiply`, `add`, `subtract`, `min` and `max`, and no node takes an absolute value. M-28's `distance` (T-138) measures from a mask's edge, which is a different thing.
- The same item's other three asks are covered: `tiles` has `stagger` and a `random` output that is one value for a tile across the stagger and the wrap (T-136, tested in `PatternNodesTests`), `distance` is T-138, and a curvature input is T-161 in M-29.
- **Open rules: stop and ask.** The node's shape is the person's.

### As built

- **Shape (decided with the person, 2026-11-26):** a `difference` mode on `blend`, `|a - b|`, beside `mix`, `multiply`, `add`, `subtract`, `min` and `max`. No new node. Opacity and mask apply as in every mode (`a` moved toward `|a - b|` by `opacity * mask`); on colour it works channel by channel; it is not clamped (it never goes below 0, unlike `subtract`).
- **To call:** `{"type":"blend","inputs":{"a":"<noise>","b":"<level or another image>"},"params":{"mode":"difference"}}`, then a `levels` with a small `in_high` (0.02 to 0.05) to turn the distance into a thin dark line. `BlendNode.Combine("difference", a, b)` is the function.
- **Catalogue:** `NodeCatalogue`'s `blend` line says what `difference` is for. The mode list itself comes from the registry.
- **Sample:** `samples/graphs/marble.texgraph` with `marble/{base_colour,normal,roughness}.png`. A 6-octave gradient `fbm` is `difference`d against a triangle ramp in v bent by a low-frequency `fbm` warp, which gives the main veins. A fainter crossing set comes from a u ramp, and the two are combined by `min` at 0.55. The stone is a `gradient_map` of a mottle. The veins are slightly rougher and slightly sunk in the normal. The sample is held by `PatternSamplesTests` (PNGs equal to the graph, check's rules, and `TheMarbleVeinsAreThinDarkLines`: 6.0% vein texels, 97.6% of them within 2 texels of stone), by `CheckSamplesTests` (checks clean) and by the scenario's `LibraryStage`, part 1 (suite `scenario-patina-library-1`). `marble/swatch.png` was copied from that suite's run, as T-142's sheets were.
- **Tests:** `FirstNodesTests` has the greyscale mode table, a hand-worked line `|u - 0.5|` across a gradient, colour per channel, `difference` of noises in the tiling table, size-invariance, and two broken copies (not absolute, opacity ignored) in the table.
- **README:** the Filters line lists `blend`'s modes and says what `difference` is for, and the Samples paragraph names `marble`.
- **Left open by the Scope:** the sample's vein direction, widths and colours, which are my choices, judged by a subagent from the base colour PNG ("reads as marble"). Its remaining notes: the fine veins are evenly spaced and nearly upright, and the main veins have frayed edges. I left these as they are.
- **Verified:** `cairn task T-185 verify` ran `patina-unit`, `scenario-patina`, `scenario-patina-library-1` and `patina-regression` fresh, and carried the rest. A subagent looked at the base colour PNG, the library sheet and the sphere frame. Nobody has looked at the normal and roughness PNGs on their own, outside the sheet.

---
description: >-
  The relationships a model declares between parts, how they are checked, and the measuring queries behind them
---
# D-3 — Constraints and measurement

**Status:** Reserved

**Raised by:** discussion of what Blender lacks for an agent, 2026-11-04.

**Default firmness:** Leaning · Rule

## Intent

An agent judges numbers far better than pixels. Blender gives it neither declarative checks nor measuring tools: every distance or overlap is hand-written bmesh code, and "does it still sit right" is re-checked by eye. Carve should let a model state its relationships, such as "the wheels rest on the ground", "the turret ring is centred on the hull top", "nothing passes through the hull" and "symmetric in X", and check them after every edit, so that "looks right" becomes something a check can pass.

Milestone planned against this doc: [M-11](../milestones/M-11-measure-and-constrain.md).

## Decision

### Relationships are declared, kept and re-checked

**Certainty:** Leaning · **Strictness:** Rule

A model's relationships between parts are declared through tools and stored with the model. Carve re-checks each one after every edit and reports any that broke.

**Alternatives:** measuring queries only, with the agent re-verifying on demand.
**Would change if:** re-checking on every edit is too slow or too noisy for agents to use.

### Measuring is a query, not a script

**Certainty:** Leaning · **Strictness:** Guideline

Distance, gap, overlap, clearance and ray casts are tool calls with exact arguments and numeric results. They are the same machinery the relationship checks use.

**Alternatives:** an escape hatch for running code, as Blender MCP has. D-1's precise tool surface argues against it.
**Would change if:** the set of measures agents need keeps growing past what tools can name.

## Open questions

- Which relationships come first, and how each is written as arguments.
- Whether a broken relationship is a warning on the edit, or refuses the edit.
- Tolerances: absolute in metres, or relative to the parts' size.
- One measuring tool with a kind, or a tool per measure.
- What overlap and clearance mean for open or non-manifold parts.

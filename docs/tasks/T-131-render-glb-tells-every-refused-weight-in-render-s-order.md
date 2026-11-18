---
description: >-
  render_glb tells a refused weight even when the engine cannot parse the file, and lists every bad targets entry in object order, as render and check do
---
# T-131 — render_glb tells every refused weight, in render's order

**Status:** Open

**Pri:** P3

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit, server-regression

## Goal

For the same `targets`, `render_glb` gives the same refusal lines as `render` and `check`, in the same order, whatever is wrong with the file.

## Scope

- **Unreadable file.** When the bytes are read but the engine's loader cannot parse them (`GlbScene.Prepare`'s `loader.Parse` catch), the type, non-finite and out-of-range weight refusals that `ReadTargets` holds are told with the path line. Today only the path line is told. Name checks still need a readable file.
- **Order.** Entries are told in object order, each with its type, weight or name line, as `TargetWeightsArgument` does for `render` and `check`. Today every type error comes before every weight or name line.
- **Tests.**
  - A regression test on the real engine with an unparseable `.glb`: `{"Bulge":-0.5}` names the weight and the path.
  - A test that `{"Bulge":2,"Other":"half"}` gives the same lines in the same order from all three tools, on a good file and on a missing one.
  - Correct T-108's As built where it states the order.

## Notes

- From M-21's review, round 3 (RV-66, 2026-11-18). This continues a thread found in RV-64 and RV-65. The person ruled to file it rather than run another round. Nothing is drawn in any of these cases; only the refusal text and its order differ.
- The person's weight-range ruling stands (D-6, *Morph targets*): refuse outside [0, 1], naming the target.

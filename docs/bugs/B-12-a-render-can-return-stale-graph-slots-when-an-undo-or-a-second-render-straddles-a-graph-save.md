---
description: >-
  render draws a model's graph slots as baked from the graph files that render read, so an undo or a concurrent render across a graph save cannot hand back an older bake unmarked
---
# B-12 — A render can return stale graph slots when an undo or a second render straddles a graph save

**Status:** Open

**Severity:** Minor

**Milestone:** — (off-milestone)

## Symptom

Found by RV-84, round 3 of M-29's review (2026-11-26), by two probes run against the real `RenderPump`,
`SheetVersions.Render` and `GraphBakes`. A `render` can answer with a frame whose graph slots are an older bake, with
nothing to mark it:

- an `undo` lands between a render's `Resolve` and its post, after the graph file was saved with new content: the
  undone model's old bake is drawn;
- two renders at once straddle a graph save: the older bake, finishing last, replaces the newer one in the store, so
  the later render's frame shows the old bake while its answer reports the new one.

Neither lasts past the next render, and both need a concurrent call and a graph save.

## Cause

The pump's one read of the bake (T-226) says whether a model was ever resolved, not whether it was resolved on the
graph files this render read. The cache is keyed by content, but which resolution a model holds in the store is not
tied to the render that asked for it.

## Fix

Not decided. Filed off-milestone by the person's choice (2026-11-26), who declined a fourth fix to the render path
inside M-29: it wants a design of its own, a bake tied to the files it read, per render, and not a patch. The path has
had three fixes for the neighbouring fault, a frame drawn with empty graph slots (T-163's shortcut, T-218, T-226).

## Verified by

The two probes of RV-84's finding, made tests that fail before the fix.

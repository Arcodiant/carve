---
description: >-
  A separable node's row pass runs across a whole band and its column pass down a tall strip, so blur and distance at 8192 stop redoing each tile's widened window and approach the 10 s that M-28 set
---
# T-159 — Strip passes for separable nodes

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** T-155

**Verify:** patina-unit, patina-regression

## Goal

`blur` and `distance` at 8192 cost close to their arithmetic's own share, not about 20 times it. Each running sum or
distance transform runs once across a band (rows) or a strip (columns), not once per 128-texel tile over that tile's
whole widened window. Every output stays byte-identical.

## Scope

- **The API** (T-155's separable passes): the row pass is asked for a band of tiles at once and the column pass for a
  strip, so a running sum is started once per band row, not once per tile.
- **The targets T-155 did not meet**, measured in Release on the machine T-135, T-153 and T-155 measured on:
  - blur smooth at radius 0.05, its own share (the run less its source alone): 10 s, from 120.4 s;
  - distance at range 0.1, its own share: 10 s, from 17.6 s.
- Within the 64 MiB cap, unless the person rules otherwise. T-155's agent estimated about 50 s for blur under the cap
  and about 15 s with a larger cap for separable nodes, so the cap is likely to be the question.
- Outputs byte-identical at every tile size and cap; the 2 GB peaks of T-155's regression tests hold.

## Notes

- **Decided (2026-11-20, by the person):** T-155 brought blur at 8192 from about an hour to 132 s and distance from
  474 s to 25 s, but missed 10 s on both. The person chose to land T-155 as it is and file strip passes here,
  off-milestone, owning the 10 s target.
- T-155's `### As built` has the figures, the harness and the cause: each tile's running sum runs over its whole widened
  window (2,586 texels for a 128-texel tile at radius 0.05), and the band makes blur's source about 4.3 times over.
- Opus: it changes an API M-29's mesh nodes may build on.

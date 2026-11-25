---
description: >-
  RV-82: the descriptions and docs findings
---
# T-220 — RV-82: the descriptions and docs findings

**Status:** Open

**Pri:** P1

**Milestone:** M-29

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** server-unit, server-regression

## Goal

What carve's descriptions, `help` entries, README and CLAUDE.md say is complete and true: `reads_mesh` is
explained, `render` says how `image` takes a graph, no description denies a tool that exists, and the counts and
tests around `help` are right.

## Scope

Each finding of RV-82 named here, by its heading there:

- "`reads_mesh`, in every graph slot that define_material and get_mesh answer, is explained nowhere an agent can read" (the descriptions)
- "render's description no longer holds T-165's image-graph sentences, against T-207's own rule, and its one `size` rule misleads for `image.size`" (the descriptions)
- "check's and select's descriptions say there is no delete, yet `delete` exists and get_mesh points to it" (the descriptions)
- "export's description lost a space: "the mesh's own:+Y up"" (the descriptions)
- "The count of `help` entries and T-207's figures are out of date or wrong in three places" (the descriptions)
- "Nothing holds the open and define_material entries, or a `More: help` pointer without an entry, and the digest leaves out help and bake" (the descriptions)
- "B-10's default-answer digests do not reach a holed face, a deleted face, a morph target or a graph slot" (the descriptions)
- "CLAUDE.md says the acceptance grip is checked "against the clean stand-in", which the person's ruling replaced" (the scenarios)
- "README's list of unit suites leaves out the new graphs-unit" (the scenarios)

- Every description stays at 3,686 characters or less (the length test, no exemption): a sentence added to one
  may move another into that tool's `help` entry, word for word, and nothing leaves an agent's reach. What M-29 added
  about graphs stays in the descriptions themselves (T-207's rule), `render`'s `image` graph form and its size rule
  among them.
- The tools/list digest pin (`PartToolsListTests.Before`) is retaken once, at the end, with a diff of the served
  descriptions against `origin/milestone/M-29` showing what moved it. T-218 may add a clause to `export`'s or
  `define_material`'s text: whoever lands second keeps both and retakes the pin.
- B-10's default-answer digests gain models that reach a holed face, a deleted face, a morph target and a graph
  slot, each digest taken from a build of `origin/main` (where `get_mesh` has no `include`), not from HEAD.
- **Owns:** the description strings and `help` entries of carve's tools, `HelpTool.cs`, `HelpToolTests`,
  `GetMeshPartsTests`, `PartToolsListTests`, `README.md`, `CLAUDE.md` (T-221 edits the T-167 and T-168 records, not
  these), T-207's and B-10's `### As built` where a figure is wrong.

## Notes

- **A fix member of RV-82,** the review of M-29 (`docs/reviews/RV-82-review-of-m-29.md`). Each finding listed in the
  Scope is read in full there: where it is, what is wrong, and how the reviewer showed it.
- Every finding gets a fix with a test that fails without it, or, where the finding is about a record, the record
  made true. A finding that turns out not to hold is answered `no change needed: <reason>` with the evidence, and one
  that cannot be fixed inside this member's files without a design change is reported, not forced.
- The member fills the `**Outcome:**` line of each of its findings in RV-82 (`fixed`, with the commit and the test's
  name; or `no change needed: <reason>`), and touches no other finding's line.

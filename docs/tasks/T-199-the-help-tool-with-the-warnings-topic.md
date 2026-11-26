---
description: >-
  help takes a topic beside tool; the warnings topic explains every warning kind, its grouping, its fix and the verbosity levels, and check's catalogue of warnings moves into it
---
# T-199 — help takes a topic, and the warnings topic

**Status:** In Progress

**Pri:** P1

**Milestone:** M-23

**Batch:** — (standalone)

**Depends on:** T-200

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

The reference text no one tool owns is served by `help` as a topic, starting with the one every M-23 task writes to: `warnings`.

## Scope

- **`topic` on `help`** (`src/Carve.Server/HelpTool.cs`): `help {"topic":"warnings"}` returns that topic's text as one block. `tool` is unchanged (released in 0.19.0). `warnings` is the only topic for now. `help {}` lists the tool entries as today, then the topics, one line each. Both arguments at once, a non-string, or an unknown topic is a tool error listing the topics. Topics are a second table beside `Entries` (name, what it covers, text); adding one is one line. help's description names the topics and says how to ask; it stays under 1,000 characters.
- **The `warnings` topic**, its text a `public const string` beside `MeshReport`'s server-side wording (planner's choice of class): every `MeshWarningKind` (ten: OpenEdge, NonManifoldEdge, InconsistentWinding, NonPlanarFace, UnusedVertex, UvUnmapped, UvFlipped, UvZeroArea, UvOverlap, MaterialNoEffect) and check's model and graph-material warnings, each with what it means for a game and its fix; T-197's grouping rule with one of its exact lines; T-198's smooth rule; the three verbosity levels as T-200's `### As built` has them (`silent` only `{"ok":true}`, `summary` the default, `full` every warning; a tool error whole at every level); the `select` query that names each kind. Export's limit warnings and the clip warnings stay in their tools' own entries.
- **check's description** moves its warning catalogue (the Warnings, UV warnings and Model warnings paragraphs) into the topic, sentence for sentence. It keeps what check does, one worked example, and a pointer: `More: help {"tool":"check"} gives ...; help {"topic":"warnings"} gives every warning, its fix and how warnings are grouped.` **open** and **export** each gain one pointer sentence to the topic. No other description changes or grows. Say in `### As built` where each paragraph went and each of the three lengths after.
- **Tests** (`unit`, `HelpToolTests`): the topic returned whole; `help {}` lists it; unknown topic, both arguments and a non-string refused in the message's words; every topic `help` lists is named by some description and every topic a description names exists; the topic's `Example: ` lines, if any, run as written. Tests that read a moved check sentence (`CheckToolDefinitionTests`, `CheckToolTests`, any found by search) read it from the topic. `regression`: `HelpThroughProcessTests` asks for the topic. The length test stands unchanged (3,686, no tool exempt).
- **`README.md`**: the `help` paragraph names `help {"topic":"warnings"}`; `## Dense meshes` points to it.
- **CLAUDE.md**: the cut rule's sentence gains "or a topic (`help {"topic":...}`) for text no one tool owns".

## Notes

- **Re-scoped after M-29 (2026-11-26).** M-29's T-207 had already built `help` (by tool) and brought every description under 3,686. T-203, whose remaining item was check's warnings and the pointer, is merged into this task.
- **Decides a public API**: the `topic` argument and the topic names. Write the topic list and the registration pattern in `### As built`.
- Depends on T-200 because it edits `CheckTool.cs`, `OpenTool.cs` and `ExportTool.cs` after T-200, and states T-200's levels as built. T-198 has landed, so the smooth rule is stated as it is.
- **Read M-23's *Decided at planning (2026-11-24)* and its re-scope paragraph first**: the person's rulings and the agreed sketches are there and bind this task.
- **Ask, don't land.** A rule neither this file nor the milestone settles, a changed number (the 3, the 2,000, the 3,686), or a check that would be loosened goes back to the person. An assertion that moves keeps asserting the same thing; say in `### As built` which moved where.
- Search for what reads the thing you change (`grep` over `tests/` and `scenarios/`) before changing it; do not list the readers from memory. Show each changed or new check failing on a wrong input, as a table in `### As built`.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built on Claude Sonnet 5.5, by one agent.

**Topics, a public API.** `help` takes `topic` beside `tool`. The only topic is `warnings`. Registration: a second table, `HelpTool.Topics` (name, what it covers, text); adding one is one line there, plus a description naming it with `help {"topic":"name"}`. `help {}` lists the entries, then `topic warnings: ...`. Both arguments, a non-string or an unknown topic is a tool error listing the topics (and, for both, the entries). `HelpTool.Topic(name)` and `TopicNames` are for tests.

**The text** is `WarningsTopic.Text` (`src/Carve.Server/WarningsTopic.cs`), headed by each `MeshWarningKind` name (ten), then check's model and graph-material warnings, T-197's grouping rule with one exact line, T-198's smooth rule with its note ending, and T-200's three levels. Export's and the clip warnings stay in their tools' entries. `CheckTool.Reference` is unchanged.

**Where each moved paragraph went.** check's Warnings, UV warnings, Model warnings and Graph material warnings paragraphs left `CheckTool.Text` for the topic (their sentences kept, with the fixes; the mesh kinds got a heading each). check keeps one sentence saying warnings are define_mesh's and the edits', plus the model's and graph materials'. The pointer is `More: help {"tool":"check"} gives ...; help {"topic":"warnings"} gives every warning, its fix and how warnings are grouped.` open and export gained the same clause on their `More:` line (it must stay the last single line).

**Description lengths** (before, after): check 3,650 to 2,195; open 3,225 to 3,324; export 3,305 to 3,390; help 629 to 781 (under 1,000). The limit of 3,686 is unchanged.

**README**: the `help` paragraph names `help {"topic":"warnings"}`; `## Dense meshes` gains one sentence pointing to it. **CLAUDE.md**: the cut rule gains "or a topic (`help {"topic":...}`) for text no one tool owns".

**Pins that moved**: `CheckToolDefinitionTests.Tools_list_has_check_and_its_description_teaches_the_fixes` reads the topic as well as the description, with the same phrases. No other test read a moved sentence (searched `tests/` and `scenarios/`). New in `HelpToolTests`: the topic whole and `help {}` listing it, every kind named, refusals in the message's words, topics and pointers matching both ways; `HelpThroughProcessTests` asks for the topic.

**Failing on a wrong input**

| Wrong input | Fails |
|---|---|
| `UvOverlap` heading renamed in the topic | `The_warnings_topic_is_returned_whole...` |
| `NonPlanarFace` heading renamed | the same |
| check's pointer names topic `warning` | `Every_topic_is_named_by_a_description_and_every_topic_a_description_names_exists` |
| both arguments accepted (tool alone refused instead) | 4 tests, `A_bad_topic_or_both_arguments...` among them |

Not checked: no frame is drawn. `README.md` and `CLAUDE.md` are in no suite's inputs (verify warns).

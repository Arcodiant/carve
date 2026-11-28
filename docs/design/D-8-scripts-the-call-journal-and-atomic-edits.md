---
description: >-
  A session's calls recorded and exported as a script, scripts with parameters and part functions that rebuild a model on each run, and an atomic edit layer under the mesh operations
---
# D-8 — Scripts, the call journal and atomic edits

**Status:** Reserved

**Raised by:** discussion session with the project owner, 2026-11-22, on the stone-room project's feature request of the same date (`feature-request-carve-patina.md` in that project's repo), and on T-71's earlier discussion of a stack language over mesh primitives.

**Default firmness:** Leaning · Rule

## Intent

An agent builds a model from a program it can run again: change a number, run, look. Today it can only do that outside Carve. Two projects have now written their `.carve` files from a script of their own:

- fps-arms (2026-11-15) built a rigged pair of arms and four tools from script-written files.
- stone-room (2026-11-22) built a textured room from a 300-line Python script, because "four beams at these x positions" was far easier there than through the edit tools. Its script had to get winding and UVs right by hand, and Carve could check nothing until the file was loaded.

The same request asks for functions that return parts (a beam bay, a hinge, a window sized to its opening), written once and placed many times.

This doc covers four layers, each usable without the next:

1. **A call journal**: what a session did, kept as data, exported as a script and run again.
2. **Scripts**: variables, loops and call-time parameters over the same statements.
3. **Part functions**: pure functions that return parts, placed as instances.
4. **Atomic edits**: a small set of kernel operations that every mesh edit is made of, and, if they hold, a way for a consumer to write an operation Carve does not have yet.

Milestones planned against this doc: [M-34](../milestones/M-34-say-it-again.md), [M-35](../milestones/M-35-a-model-from-a-script.md), [M-36](../milestones/M-36-parts-written-once.md) and [M-39](../milestones/M-39-atomic-edits.md). [M-37](../milestones/M-37-real-sizes.md) and [M-38](../milestones/M-38-light-and-glass.md) come from the same request and stand apart from it.

## Decision

### A call journal beside the undo history

The server keeps each successful, model-changing call with its arguments, beside the snapshot the undo history already keeps. `undo` removes the journal entry with its step, so an abandoned attempt is not in the script. Read-only calls (`select`, `get_mesh`, `render`, `check`) are not replayed.

The undo history cannot be exported as it stands: `ModelStore` keeps whole-model snapshots and each step's tool name, with no arguments, and stops at 100 steps. The journal is not capped at 100; only the snapshots are.

A session that began with `open` exports as "open this file, then these steps".

**Alternatives:** no journal, with scripts written from nothing; a journal the agent turns on.
**Would change if:** exported scripts are too literal to be worth editing, and agents write scripts from nothing instead.

### An exported script replays to the same model

**Certainty:** Leaning · **Strictness:** Invariant

Running a script exported from a session, on an empty model, gives a model equal to the session's, bit for bit. This is what makes the script format trustworthy, and it is the first test of the language.

**Alternatives:** equal by content within a tolerance.
**Would change if:** a tool turns out to depend on something outside its arguments and the model (none is known; `Carve.Core` has no clock and no `System.Random`).
**Enforced by:** not written yet. M-34 adds a replay test over every scenario's `run.json`.

### A script's statements are the tools

A statement in a script is a tool call with named arguments, the same names and the same refusals as over MCP. There is one vocabulary to describe and test, and a script gains a tool the day the tool exists. Generators (M-10's primitives, M-36's wall, prism and sweep) are tools as well, so they are available both ways.

A script runs as one undo step. An error names the script's line and leaves the model as it was. The result is counts and bounds for each mesh or part, with warnings folded as [M-23](../milestones/M-23-quiet-on-dense-meshes.md) folds them, never the geometry.

**Alternatives:** a separate scene-description format with its own primitives, as the request first put it.
**Would change if:** the tools' argument shapes read badly in a script and a second, script-only vocabulary is clearly shorter.

### The language

**Certainty:** Open · **Strictness:** Rule

**Options:**

- *An existing language, embedded* (JavaScript or Lua in an interpreter with time and memory limits and no file or network access). Agents write it fluently and it needs no description. A dependency, and a larger surface to keep deterministic.
- *Our own small language, compiled to a stack machine whose words are the tools.* No dependency, nothing to sandbox, determinism and a step budget by construction, and the compiled form is small flat data the Carve file could hold. The same machine could later run atomic edits (below). A parser, scoping and line-accurate errors to write and keep, and every session pays to learn the syntax.
- *A flat list of calls with named results*, produced by one of the above. No control flow in what Carve stores; loops unroll.

Raw postfix as the syntax an agent writes is not an option under consideration: an exported tool call has named arguments, and the request asks for named parameters, loops and small functions.

**Working assumption:** a conventional surface syntax (calls with named arguments, `let`, `for`, functions). M-34 needs only [the statement format](#the-statement-format), which every option shares.
**Decided by:** M-35's planning, with M-34's exported scripts as examples to write in each candidate. The person's call.

### The statement format

**Certainty:** Settled for M-34's statements · **Strictness:** Rule

A `.carvescript` is statements separated by newlines, each one tool call: `tool(name: <json>, name: <json>, ...)`, or `tool()` with no arguments. The tool name is `[a-z_][a-z0-9_]*`, as is each argument name; each value is one JSON value (RFC 8259), written as the MCP call took it, its numbers kept as their raw text. Whitespace, newlines included, may stand between any two tokens, so a statement may span lines, but two statements never share one; the one exception is that a newline may not stand between the tool name and its `(`, which the reader refuses. Nothing else is in the grammar: no comments, variables, keywords, loops or expressions, and no trailing comma. The person ruled the format on 2026-11-26 (M-34's planning); refusing a trailing comma and refusing a newline before the `(` are the implementer's defaults under T-230's Notes (refuse what the spec does not settle), not rulings, and open to loosening; `Carve.Core.Scripts` reads and writes it (T-230), the writer keeping a statement of 120 characters or less on one line and a longer one an argument a line.

M-35 decides everything around it ([The language](#the-language)): whether a statement may name its result, what a variable or loop looks like, and comments.

**Alternatives:** the JSON of a `tools/call` per line (no new syntax, but a name for every value twice over and hard to edit by hand); positional arguments.
**Would change if:** M-35's language needs a statement form this one cannot be written in.
**Enforced by:** `ScriptReaderTests` and `ScriptWriterTests` in `tests/Carve.Core.Tests/Scripts/`.

### Addressing in a script

**Strictness:** Guideline

A script names elements through what earlier calls returned, through named groups and through `select` expressions. Literal ids are allowed: they replay exactly, because ids are deterministic, but they break when an earlier step's topology changes.

An exported script holds literal ids at first. Recording the `select` an id list came from, in place of the ids, is a later refinement.

**Alternatives:** refuse literal ids in a script.
**Would change if:** exported scripts prove too brittle to edit.

### Parts are values, and functions that return them are pure

A script function returns a part: geometry with its materials, UVs and grain. The caller places it. Ids are local to the part being built, so a change inside one function cannot shift ids anywhere else.

Such a function does no I/O, runs under a step and face budget, and gives the same part for the same arguments. It is registered with the host, not a plain function of the language, so Carve sees each call: a second call with equal arguments is an `instance_of` the first ([M-9](../milestones/M-9-parts.md)), and a rerun rebuilds only the parts whose function or arguments changed.

Functions build at the origin; position is placement, never an argument, or two beams at different x would not share a mesh.

**Alternatives:** functions as plain script code that calls tools on the session's model, with no part value and no sharing.
**Would change if:** real scripts mostly need functions that edit an existing part in place.

### A generated part records where it came from

The Carve file records, for each generated part, its script and arguments. A rerun replaces exactly those parts and leaves the rest of the model alone. `open` can then say a part is out of date with its script.

**Alternatives:** the script is only a way to make a file, with no link kept.
**Would change if:** the link costs a file version and nobody reruns against a model with hand-made parts in it.

### A hand edit to a generated part

**Certainty:** Open · **Strictness:** Rule

**Options:**

- *Overwrite with a warning* that names the part, with a way to detach a part from its script so that a rerun leaves it alone.
- *Refuse the rerun* until the part is detached or the edit undone.
- *Replay the hand edits* after the regenerated part, from the journal.

**Working assumption:** overwrite with a warning, and a detach flag.
**Decided by:** M-35's planning.

### Mirrored placement

**Certainty:** Open · **Strictness:** Rule

A part placed with a negative scale has its winding reversed.

**Options:**

- *Refuse a mirror in placement*; a mirrored hinge is a second function call or a `mirror` edit (M-10).
- *A mirrored call becomes its own mesh*, with winding corrected, sharing nothing.

**Working assumption:** refuse.
**Decided by:** M-36's planning, with M-10's mirror in hand.

### Shared UVs against anchored UVs

**Certainty:** Open · **Strictness:** Rule

Instances share one mesh and so one set of UVs. That suits beams. A wall whose stone courses must line up with its neighbours ([M-37](../milestones/M-37-real-sizes.md)'s anchor) needs UVs that depend on where it is placed.

**Options:**

- *Per part*: a part with a world-anchored projection is never an instance.
- *Anchored UVs at export*: instances share geometry and each gets its own UV set.

**Working assumption:** per part.
**Decided by:** M-36 and M-37's planning, whichever comes first.

### A script builds whole meshes and edits them, never single elements

A script has `define_mesh`, the generators and the edit tools. It has no "add one face to this mesh" primitive. Every edit goes through T-71's provenance, so colours, groups, weights, UVs and hard edges are carried without the script's author doing anything. T-71's paper run of an agent-written bevel showed what happens otherwise: stretched UVs and a hard flag on the wrong edge, unnoticed by the author.

Winding and unmirrored UVs are promised for generators and for a projection made after the geometry is final. `define_mesh` in a script promises neither, and a moved vertex still keeps its corner UVs (T-71, decision 3).

**Alternatives:** element-level construction in scripts from the start.
**Would change if:** atomic edits (below) make element-level work safe.

### Every edit is a sequence of atomic operations

The kernel names a small set of atomic edits (a vertex from a blend of sources or an offset; a face from a source face; an edge split into a chain; removal; a face's loop rewritten), each of which keeps the mesh valid and carries every attribute layer. Every edit tool is written as a sequence of them. T-71's `EditProvenance` is most of this set already.

This is a C# API first. It is proved on one operation written on the atoms alone, in [M-39](../milestones/M-39-atomic-edits.md).

What it is expected to shorten is an operation that is mostly topology. The edits run 150 to 570 lines today, and most of that is geometric decision and refusal text, which an atomic layer does not touch.

**Alternatives:** leave the edits as they are, each calling `EditProvenance` its own way.
**Would change if:** M-39's operation is no shorter or safer on the atoms than without them.

### Operations written by a consumer

**Certainty:** Open · **Strictness:** Rule

A language over the atoms would let a consumer write an operation Carve lacks, inside Carve, where each step is checked, while the native one waits in the backlog. An operation several consumers have written for themselves is then a backlog item with a reference implementation.

**Options:**

- *Build it*, on the stack machine of *The language* if that option is taken, so scripts and operations share one machine.
- *Do not build it*; new operations are C# on the atomic API, and a consumer's workaround is a script of existing tools.

**Working assumption:** not built. No milestone is reserved.
**Decided by:** M-39's result, and whether M-35's language is ours.

### GPU acceleration waits for a measurement

**Strictness:** Guideline

Nothing here is designed for a GPU until a profile shows a need ([T-184](../tasks/T-184-profile-mesh-edits-bakes-and-renders-before-any-gpu-work.md)). The only recorded speed problem is patina's evaluation at 8192 (T-130).

If the need appears, the idea to test is the person's: operations built only from parts that each have a GPU translation are GPU-capable by construction. That holds for whole-array arithmetic with no decision between steps (stencils applied to every vertex, a bake over every texel, a texture graph), and gives compatibility, not speed: a chain of capable steps still needs fusing. Element-at-a-time topology edits stay on the CPU. So the atomic set would have two tiers, and the guarantee applies to the bulk tier.

**Alternatives:** design the atoms as bulk operations from the start.
**Would change if:** T-184 shows mesh edits or bakes are slow enough to matter on a consumer's models.

## Alternatives rejected

- **Exporting the undo history itself.** It holds snapshots and tool names, no arguments, and 100 steps.
- **Boolean subtraction** (the request's item 10). D-1's decision at M-5 stands: a cut is a set of mesh edits. M-36's wall with openings covers the case the request raised, and the request ranks it Low.

## Open questions

- Skeletons, weights, clips and morph targets in a script. The statements are the tools, so they come with the format, but nothing here is designed around them. fps-arms is the evidence that they are wanted.
- One script calling another, and where shared functions live.
- Whether patina's graphs and a script's parameters should be exposed the same way (T-139).
- Whether the journal should be readable in a session, as a tool, before any export.

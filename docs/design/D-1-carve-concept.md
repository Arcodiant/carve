---
description: >-
  What Carve is, who it is for, and the decisions and open questions that shape it
---
# D-1 — Carve concept

**Status:** Accepted

**Raised by:** project setup session, 2026-10-31

**Default firmness:** Leaning · Rule

## Intent

Carve is a 3D modelling tool whose user is a Claude agent. It runs as an MCP
server, so an agent building a game on the Empyrean engine can make the game's
models itself instead of depending on a person, a downloaded asset or a generator.

An agent has no hand and no eye on a viewport, so everything a modeller does by
pointing has to work as a call with exact arguments and a checkable result. The
agent defines and edits meshes by coordinates or with tools, looks at what it made
through renders, asks precise questions of the mesh, and exports a model the game
imports as content.

The person who owns the game benefits at one remove: they see models in the game,
and can read how each was made.

### What the content is

Carve is mostly code, but it has content, planned with the mechanics that show it:

- **Sample models**: the small models each tool is demonstrated and tested on.
- **Render-mode looks**: what a shaded view, a normals view or a UV view looks like,
  so an agent can read it.
- **Tool descriptions and worked examples**: the text agents learn the tools from.
  For a product used only through MCP, this text is the interface.

## Decision

### MCP server for agents

**Certainty:** Settled · **Strictness:** Invariant

Carve's interface is MCP tools. Everything a user can do, an agent can do through a
tool call, with no step that needs a person at a screen.

**Enforced by:** review only, for now; every milestone must be reachable through
MCP (`CLAUDE.md`, *Planning rules*).

### Empyrean games are the target

**Certainty:** Settled · **Strictness:** Rule

A model is finished when it imports into an Empyrean game and looks right there.
Other targets are not designed for.

### Editing by coordinates and by tools

**Certainty:** Settled · **Strictness:** Rule

An agent can define geometry directly by coordinates, and edit it with modelling
tools: cut, extrude, and moving a vertex, edge or face.

### Looking at the model

**Certainty:** Settled · **Strictness:** Rule

An agent can capture screenshots and render the model in different modes, UVs and
normals among them.

### Queries and selections

**Certainty:** Settled · **Strictness:** Rule

An agent can run simple query commands, such as "select all unattached vertices" or
"select all faces above y=10".

### C# on Empyrean

**Certainty:** Leaning · **Strictness:** Rule

C#/.NET 10, with renders drawn by Empyrean itself, so a screenshot shows the model
as the game would. The kernel, `Carve.Core`, stays engine-free. Rendering needs a
machine with a Vulkan device, even with no window.

**Alternatives:** a small software rasteriser of our own, which runs anywhere and
is byte-repeatable, but won't match the game.
**Would change if:** Empyrean cannot run windowless inside a long-lived server
process, or its renders do not compare from run to run closely enough for scenarios.

### Polygon meshes

**Certainty:** Leaning · **Strictness:** Rule

The mesh is polygons: faces of any size in an edge-connected structure, triangulated
on export. Operations keep their meaning on a logical face or edge loop.

**Alternatives:** triangles only, a simpler kernel that loses the logical faces.
**Would change if:** the structure's cost outweighs its use once cut and extrude
exist, seen in how agents actually use them.

### A Carve file, and .glb export

**Certainty:** Leaning · **Strictness:** Rule

Work persists in a native Carve file that keeps the mesh as authored. Models reach a
game as exported `.glb`, which Empyrean's `IModelLoader` loads.

**Alternatives:** `.glb` only, which loses the polygons on every save; server memory
only, which loses unexported work on a restart.
**Would change if:** none recorded.

### No window

**Certainty:** Leaning · **Strictness:** Rule

The shipped product has no window. Every look is an offscreen render through a tool
call.

**Alternatives:** an optional live viewer, so a person can watch an agent work.
**Would change if:** a person watching turns out to matter enough to pay for a frame
loop and input.

### The renderer lives in the server process

One generic host (`Host.CreateEmptyApplicationBuilder`) owns one container holding both the
engine and MCP, and the engine runs on a dedicated `frame` thread of its own under Carve's own
`IHostedService`, since `EmpyreanHost` refuses `IHostedService`. A Vulkan failure is held and
reported by whatever needs the engine; the process and MCP stay up.

A child process, so a device loss could not take the session down, was the rejected
alternative: holding the failure in process already keeps the session up, without a second
process and a protocol between the two.

## Open questions

- **Tool shape.** One tool per operation, or a few general ones? How are elements
  addressed: ids that survive edits, or selections held by the server? How much does
  one call return?
- **Undo and history.** Does an agent get undo? Is a model's history recorded so the
  person can read how it was made, or replayed to rebuild it?
- **The Carve file.** Text or binary, geometry or a list of operations? (Text would
  diff in git; the choice is open.)
- **Scope of a model.** Which of these does Carve author, and when: UVs and
  unwrapping, materials and textures, smoothing and normals, several parts or a node
  hierarchy, LODs, colliders, rigs and animation?
- **Render modes.** Which come first, and do any need something Empyrean does not
  have? The first look at its docs did not find a normals or UV debug view; a
  material or shader of Carve's own may do it.
- **Transport.** Stdio only, or also HTTP? How is the server installed for a game's
  Claude session?
- **Empyrean-specific export.** Scale, axes and naming conventions a game expects,
  and whether Carve checks a model against them.
- **Boolean and heavier operations.** Does "cut" mean a plane cut, a knife path, or
  boolean subtraction?

### Engine needs

| # | What is missing | Priority | Interim |
|---|---|---|---|
| 1 | A named error when Vulkan cannot start: with no driver, `IGameLoop.Initialise` throws `ArgumentNullException` for `ptr` from inside `Vulkan.Instance.Create` | Low | `EngineThread` words the fault itself, naming Vulkan |

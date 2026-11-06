---
description: >-
  An agent saves its mesh to a .carve file and opens it again in a later session, with a worked example from an empty file to a game's content folder
---
# T-17 — Save and open tools

**Status:** In Progress

**Pri:** P1

**Milestone:** M-3

**Batch:** — (standalone)

**Depends on:** T-15, T-16

**Verify:** server-regression

**Perf:** skip: Carve declares no heavy suites

## Goal

An agent keeps its work: `save` writes the current mesh to a `.carve` file, and `open` makes it current again, in the same carve process or a later one. The descriptions and the README walk a model from an empty file to a game's content folder.

## Scope

**Tools**, registered in `Program.cs` with closed schemas:

- **`save(path)`** writes `CarveFile.Write(current mesh)` through T-16's `ToolPaths`, with the extension `.carve`.
  - The result names the absolute path, `replaced` when it replaced a file, and the vertex and face counts.
  - Before `define_mesh` it is a tool error.
  - It does not change the mesh, the undo stack or the version.
- **`open(path)`** reads a `.carve` file with `CarveFile.Read` and makes the mesh current as one step, as `define_mesh` does: one undo takes it back to the mesh before.
  - Ids are the file's, so the agent's ids from before the save still name the same elements.
  - The result is `define_mesh`'s report (counts, closed, warnings), preceded by the absolute path.
  - Its errors:
    - a wrong extension;
    - a file that cannot be read;
    - every `CarveFile.Read` error, with its element name.
  - **A missing file** is a tool error naming the resolved absolute path and listing the `.carve` files in that folder (at most 20, sorted, with a count of the rest), or saying there are none or that the folder does not exist. This is the error the acceptance run's planted mistake meets (T-20); it must let an agent recover from its text alone.
  - A failed `open` leaves the mesh, the undo stack and the version unchanged.
- Carve keeps **no current file**: the path is given every time (agreed at planning).

**Descriptions and the worked example** (D-1's content: this text is the interface):

- `save` and `open` each say what the file holds and what it does not:
  - the polygons and ids exactly, not the undo history;
  - that the `.carve` file is the one to keep working in and `.glb` is only for the game;
  - the path rules, and every error.
- The `save` description includes T-15's cube file text, or the start of it, so an agent knows the format.
- The worked example, as one short sequence in `save`'s description, with `export`'s pointing to it:
  1. `define_mesh` the cube;
  2. edit;
  3. `save` to `crate.carve`;
  4. in a later session, `open` `crate.carve`;
  5. `export` to `<game>/Content/models/crate.glb`;
  6. the game loads it with `models.LoadModel(new ContentPath("models/crate.glb"))`.
- **README**: a short section, *From an empty file to a game*, on the same sequence. It shows the `.mcp.json` entry already there, the calls, where the files land, and the game's one line of loading code. This task is the README's only editor in M-3 apart from T-20's description fixes.

**Tests** (`regression` tier where they start `carve`, `unit` otherwise):

- Save, then `define_mesh` another mesh, then open: `get_mesh` equals what it was before the save, ids and positions bit for bit (crate and L-block). Then `undo` returns the other mesh.
- **A later session**: save in one `carve` process, stop it, open in a fresh one, and `get_mesh` is equal. This is the milestone's "later session" in a test.
- Save before `define_mesh`; save over an existing file (`replaced`); a wrong extension on each tool.
- `open` errors:
  - a missing file, both with `.carve` files listed and with none;
  - a folder that does not exist;
  - bad JSON;
  - an unknown version;
  - an invalid face.

  After each one, the mesh, the version and the undo stack are unchanged.
- The worked example in the description, run as written in a temp folder: define, save, open, export. The `.glb` parses (T-16's parse helper).
- `tools/list` lists `save` and `open` with closed schemas.
- **Show each new check failing** on a broken product: open not a step, save writing the previous mesh, the missing-file listing dropped, a failed open that changes the version. Give the table.

## Notes

- **Decided by the person at planning:**
  - the path rules: relative to carve's working directory, folders created, replace and say so, extensions checked, a missing file lists the folder's `.carve` files;
  - no current file;
  - the worked example in the descriptions and the README.
- **Open rules: stop and ask.**
  - Saving undo history.
  - `open` adding to the mesh rather than replacing it.
  - Remembering a current file.
  - Any change to T-15's format or T-16's `ToolPaths` rules. A defect in either is reported, not patched here, unless it is a one-line fix.
- Uses T-15's `CarveFile` and T-16's `ToolPaths` and parse helper. It does not touch `Directory.Build.props`, `tests.json`, `Carve.slnx` or `CLAUDE.md`: T-18 owns those in this wave.
- Sonnet is enough: it builds on decided APIs.
- No frames: `render` is unchanged. T-19 shows a reopened mesh on screen.
- Run `finish` from inside the worktree and leave the folder.

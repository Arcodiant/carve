---
description: >-
  An agent queries and selects elements, checks a mesh's health and reads normals and wireframe renders
---
# M-4 — Ask the mesh

**Status:** Planned

**One-liner:** *An agent queries and selects elements, checks a mesh's health and reads normals and wireframe renders*

## What this milestone delivers

An agent asks precise questions of a mesh and acts on the answers: "select all unattached vertices", "select all faces above y=10". Selections feed the edit tools. It can read stats, check a mesh's validity, and see normals and wireframe renders.

Content: a deliberately broken sample mesh for the checks to find, and the normals and wireframe looks.

## Open at planning

- The query form: a small language, structured arguments, or named tools per query?
- Are selections held by the server, named, or returned as id lists?
- Does a render show the selection?
- Which validity checks matter to an Empyrean import.
- How the normals and wireframe modes are drawn, and whether that raises an engine need.

## Required tasks

## Retrospective

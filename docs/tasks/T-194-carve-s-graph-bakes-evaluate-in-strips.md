---
description: >-
  carve bakes a graph material's slots in parallel strips, with its mesh nodes safe on several threads, baked times and the 5 s warning still right, and every frame unchanged
---
# T-194 — carve's graph bakes evaluate in strips

**Status:** Open

**Pri:** P1

**Milestone:** M-40

**Batch:** — (standalone)

**Depends on:** T-192

**Verify:** core-unit, server-unit, server-regression, scenario-materials, scenario-planks

## Goal

Where carve evaluates a texture graph (M-29: a material slot's bake for `render`, `export` and the `uvs` look), it
goes through T-192's driver. A bake at 1024 or 4096 uses the machine's cores, its bytes and every frame are what
they were, and what `render` reports about a bake is still true.

## Scope

- **This task is written before M-29's code exists.** M-29 lands before this milestone starts. Read the `### As
  built` of [T-163](T-163-graph-materials-evaluated-and-cached-for-render.md),
  [T-164](T-164-graph-materials-in-export.md), [T-165](T-165-a-graph-output-under-the-uv-layout-look.md) and
  [T-161](T-161-mesh-input-graph-nodes.md), then the code, and read this scope against them. Where they differ from
  what is assumed here in a way that changes what this task decides, stop and ask.
- **Read T-192's and T-191's `### As built`** for the driver and for what the audit found in the mesh nodes.
- **The bake.** Every place `src/Carve.Graphs` or `src/Carve.Server` evaluates a graph output passes the default
  `StripOptions` (through `OutputImage` if T-163 kept to it, as planned), with the call's cancellation token. A slot
  under 512 stays on one thread, by the driver's own rule.
- **What `render` and `export` report** (T-163: `baked: [{material, mesh, slot, size, ms}]`, and a warning past 5 s
  naming the slot and the node that cost most):
  - `ms` stays the slot's wall time.
  - The node that cost most is the one with the most time summed over the strips. However T-163 measured a node's
    time, it must now be right with several threads in the node at once.
  - The 5 s threshold stays on wall time. The injected-clock test of T-163 passes unchanged, or with a change that
    only makes its clock safe on threads; a change to what it asserts goes to the person.
- **The mesh nodes** (`mesh_curvature`, `mesh_occlusion`, `mesh_mask`, `mesh_position`): T-191 audited them. Here a
  test bakes each on the raised-hoop barrel at 1024 through the driver, 8 strips named, and holds the bytes equal to
  one strip's.
- **The collector.** `src/Carve.Server/Carve.Server.csproj` gains
  `<RuntimeHostConfigurationOption Include="System.GC.ConserveMemory" Value="9" />` with a comment naming this task.
  A `unit` test reads the built `carve.runtimeconfig.json` and holds the value. (T-193 does the same in patina's
  project file; the two files are not shared.)
- **Tests:**
  - M-29's `graphs-unit`: a slot bake at 1024 through the driver equals the one-strip bytes for the planks' four
    outputs and for `weathered-iron` and `weathered-staves` on the barrel; the mesh-node test above.
  - `server-unit`: `baked` has the shape it had; a cancelled `render` during a 2048 bake answers as before and leaves
    no `patina-strip-` thread.
  - Each new check is shown failing once on a broken copy, named in the As built.
- **Frames.** Every frame-drawing suite named in this file's `**Verify:**` passes unchanged. Add M-29's
  `graphs-unit` and `scenario-weathered` to that line in this task's first commit (they are not in `tests.json` as
  this is written). Compare every frame's hash in `run.json` with a run of the same suites on the commit this task
  branched from, fetched by job id, and say how many were compared and that none moved. The milestone's own verify
  runs every other suite with the setting in.
- **Measured, not asserted:** one bake of the weathered barrel's slots at 4096 in a Release build, on one strip and
  on the default: time and the process's peak working set. Keep the harness in `artifacts/M-40/T-194/` under the
  container's `branches/main`, no product flag, and put the figures in the As built for T-195.
- `README.md`: where carve's section says how long a graph bake takes or how it runs, it says bakes use the
  machine's cores. Say in the As built what changed. `CLAUDE.md`: the Layout row of `src/Carve.Graphs`, a clause.
- No file under `src/Patina.Server`, `src/Patina.Graphs/Checks` or `tools/` changes. T-193 owns those and runs
  beside this task.

## Notes

- **The person made M-40 depend on M-29 (2026-11-22)** so that this task is part of it.
- **Decided at planning by the person (2026-11-23):** `System.GC.ConserveMemory=9` in carve as in patina.
- **Not checked at planning:** anything in M-29's code, which was not on `main`; what the setting does to carve's
  renders. The frame hashes and the suites' times before and after are that check; put the times in the As built.
- **Goes back to the person, not into the code:** a frame or an exported `.glb` that changes; a bake's bytes that
  differ between one strip and several; a mesh node that cannot be made safe without changing its values; a suite
  more than 20% slower with the setting; any change to what the 5 s warning says or when it fires.
- carve's bake cache (512 MB, T-163) and the driver's 512 MiB budget are separate: the budget is held only while a
  slot is being evaluated. carve has no memory test; this task adds none and records the peak.
- Opus if T-163's node timing needs redesign for threads; otherwise mechanical. Decide at dispatch from T-163's As
  built.
- `server-regression` is long. Run `cairn lint` as a command of its own and read it before `finish`.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools.

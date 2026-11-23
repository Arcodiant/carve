---
description: >-
  patina's export, swatch, check and the sample tool evaluate in parallel strips, with System.GC.ConserveMemory set so an 8192 export stays under 2 GB, and every committed PNG and frame unchanged
---
# T-193 — patina's export, swatch, check and the sample tool evaluate in strips

**Status:** Open

**Pri:** P1

**Milestone:** M-40

**Batch:** — (standalone)

**Depends on:** T-192

**Verify:** patina-unit, patina-regression, scenario-patina, scenario-patina-library-1, scenario-patina-library-2

## Goal

Everything patina evaluates goes through T-192's driver: `export`, `swatch`, `check` and
`tools/Patina.SampleGraphs`. An agent calling `export` at 2048 or 8192 on a plain `patina` gets the same files in a
fraction of the time, and the process still peaks under 2 GB.

## Scope

- **Read T-192's `### As built` first**, for the driver's signatures and its measured table.
- **The callers:**
  - `src/Patina.Server/ExportTool.cs`: both `evaluator.Evaluate` calls (the output's and the metallic's) become
    `StripEvaluation.Evaluate` with the call's `cancellationToken`. The one RGBA8 buffer, the forced collections
    between outputs, the temporary file and the move stay as they are.
  - `src/Patina.Graphs/Checks/CheckSet.cs`: `CheckOutput.Evaluate` and `CheckSet.OfGraph` go through the driver, with
    an optional `StripOptions` so a test can name one strip.
  - `src/Patina.Server/SwatchTool.cs`: its `OutputImage` calls pass the default `StripOptions`.
  - `tools/Patina.SampleGraphs/Program.cs`: `OutputImage.Make` passes it too.
  - No file under `src/Carve.*` changes. T-194 owns those and runs beside this task.
- **The collector.** `src/Patina.Server/Patina.Server.csproj` gains
  `<RuntimeHostConfigurationOption Include="System.GC.ConserveMemory" Value="9" />`, with a comment naming this task
  and what it holds. Planning checked that this form writes `"System.GC.ConserveMemory": 9` into the built
  `runtimeconfig.json` and has the environment variable's effect (the blur at 8 strips: 1,146 MiB against 2,687 MiB).
  A `unit` test reads the built `patina.runtimeconfig.json` beside `patina.dll` and holds the value.
- **Tests added** in `tests/Patina.Server.Tests` (`unit`, on `PatinaHarness`):
  - `export` at 1024 of the planks and of painted-metal (so metallic is packed) writes, byte for byte, the PNG of
    `OutputImage.FileRgba8` on one strip.
  - `check` at 1024 gives the report it gives on one strip, for a clean sample and for a planted fault from
    `samples/graphs/faults/`.
  - `swatch` `flat` at 1024 gives the texels of one strip.
  - A cancelled `export` at 2048 answers as it does today and leaves no file and no `patina-strip-` thread.
  - Each new check is shown failing once on a broken copy, named in the As built.
- **Nothing committed changes.** `dotnet run --project tools/Patina.SampleGraphs` leaves `git status` clean under
  `samples/graphs/`. Every byte-for-byte check passes as written.
- **The six 8192 tests** (`ExportBoundedMemoryTests`, `TransformDistanceBoundedMemoryTests`) pass with the 2 GB
  bound and their deadlines untouched. Record each one's time and peak, before (from `main`'s last run of the suite,
  fetched by job id, or T-189's record) and after, in a table in the As built. Their summaries are corrected where
  they say how evaluation runs.
- **Frames.** `scenario-patina` and the two library suites pass unchanged. Compare every frame's hash in `run.json`
  with those of a run of the same suites on the commit this task branched from, fetched by job id (never `cairn test
  artifacts <suite>` alone, which returns the newest job from any branch), and say in the As built how many were
  compared and that none moved. No frame needs opening: this task draws nothing new.
- **What an agent reads:**
  - `ExportTool.Text`: where it says outputs are "evaluated and written one at a time, in tiles", it also says each is
    evaluated on the machine's cores, up to 8 strips. The sentence that quotes a time is T-195's; leave it.
    `NodeToolTests` holds every description at 3,686 characters or less.
  - `README.md`, "What is slow or bounded": the same fact, and that the bytes do not depend on the machine. The
    figures are T-195's. Say in the As built what changed in the README.
  - `CLAUDE.md`: the Layout rows of `src/Patina.Server` and `tools/Patina.SampleGraphs`, a clause each.

## Notes

- **Mostly mechanical, with long runs.** It decides nothing others build on. `patina-regression` took 1,162 s in
  M-28's last verify; give `verify` a long limit.
- **Decided at planning by the person (2026-11-23):** `System.GC.ConserveMemory=9` in patina and in carve; the 2 GB
  bound stays; no tool gains an argument.
- **Not checked at planning:** what the setting does to `swatch`'s renders and the engine's own allocations. The
  frame hashes and the suites' times, before and after, are that check; put the times in the As built.
- **Goes back to the person, not into the code:** a peak over 2 GB, or a wish to move the bound or a deadline either
  way; a committed PNG or a frame that changes; a suite more than 20% slower with the setting; the canary of M-33's
  `PngWriter` failing.
- `ExportStage`'s tool-list check and `NodeToolTests` are unaffected: the tool list does not change.
- Run `cairn lint` as a command of its own and read it before `finish`.
- **Write down in `### As built` for T-195:** the six tests' times and peaks; how the before figures were fetched.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools.

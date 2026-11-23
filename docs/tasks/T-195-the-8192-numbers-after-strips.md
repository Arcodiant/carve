---
description: >-
  export's description, the README and T-115's record carry every sample's 8192 time and peak memory measured after strips, in Release and Debug, beside the one-thread figures
---
# T-195 — The 8192 numbers after strips

**Status:** Open

**Pri:** P1

**Milestone:** M-40

**Batch:** — (standalone)

**Depends on:** T-193, T-194

**Verify:** patina-unit, patina-regression, scenario-patina

## Goal

What patina and carve say about a large evaluation is what they now measure. Every sample graph is timed at 8192
before and after strips, and `export`'s description, the README and T-115's record carry the figures.

## Scope

- **Read first** the `### As built` of T-192 (its measured table and harness), T-193 (the six tests' times and
  peaks), T-194 (the 4096 bake) and M-33's
  [T-189](T-189-export-s-8192-numbers-measured-again.md) (the planks through the real `patina` with the new encoder,
  and its harness in `artifacts/M-33/T-189/`).
- **Measure, on this machine, and say which machine** (cores, memory, runtime):
  - **The evaluator alone, Release, every sample graph under `samples/graphs/`** (M-29's two included) at 8192,
    every output: on one strip (`StripOptions(Strips: 1)`, which is the evaluator as it was before this milestone,
    64 MiB cap and all) and on the default. Time, strips, tiles made, `HeldBytes`, peak working set. One process a
    run. The one-strip runs are long (planning: the cobblestones 451 s; painted-metal was not measured on one thread
    and may be longer): start them in the background and do not wait on them in the foreground.
  - **The evaluator alone, Debug, every sample, on the default only.**
  - **The whole call through the real `patina`**, the planks at 8192, all four outputs, Release and Debug, twice
    each, with T-189's harness: the call's time, the peak working set and each PNG's bytes. T-189's record is the
    before.
  - Keep the harness and every raw table in `artifacts/M-40/T-195/` under the container's `branches/main`, outside
    the repo's tracked files, and write down how to run it. No product flag.
- **The table** (sample, one thread, strips, speedup, tiles made each way, peak) goes into this file's As built and
  into M-40's file under its *Why*, as a dated block beside planning's. Planning's table stays.
- **Text that quotes a time or a way of running:**
  - `ExportTool.Text`: the sentence on how long 8192 takes carries the measured figures, Release and Debug, for the
    planks and for the slowest sample. It still tells a client with a short call timeout what to do if the measured
    time warrants it. `NodeToolTests` holds every description at 3,686 characters or less.
  - `README.md`, "What is slow or bounded": the same figures, and blur's and distance's from T-193's table where it
    quotes them. Say in the As built what changed.
  - T-115's `### As built`: a dated line under its "Measured at 8192" item. The old lines stay.
  - `GraphEvaluator.MostBandTiles`' comment and any other comment in `src/Patina.Graphs/Evaluation/` that quotes an
    8192 figure this milestone changed: a dated clause, the old figure kept.
  - `tests.json`: `patina-regression`'s `est-seconds`, from this task's own run of the suite. Nothing else there.
  - `CLAUDE.md`: only a figure it quotes that is now wrong.
- **[T-130](T-130-faster-texture-graph-evaluation-at-8192.md)** (what threads do not fix) gets a dated note with
  what this task measured that bears on it: the tiles the cobblestones remake under strips, and each sample's time
  per output, which shows what sharing work between outputs could save.
- No test asserts a time. The times are a record, as T-155's and T-189's are.

## Notes

- **A measuring task.** It makes long real runs and little code, and decides nothing others build on. Expect a
  hand-off: the first agent writes the harness and starts the long runs, the second reads them and writes the text.
- **Decided by the orchestrator, told to the person at planning:** the one-thread Debug figures are not measured
  for every sample (hours of runs for a figure nobody quotes); the before in Debug is T-189's planks.
- **Each figure is one or two runs.** Planning's one-thread planks at 8192 took 26.6 s in one session and 38.1 s in
  another on the same machine. Quote a speedup only within one session, and say so.
- **Goes back to the person, not into the code:** a sample slower on the default than on one strip; a peak over
  2 GB; bytes that differ between the two runs of one sample; any wish to assert a time or move a bound or a
  deadline.
- `ExportTool.cs` and the README paragraph are also T-193's, so this task starts only once T-193 has landed.
- `patina-regression` is long. Give `verify` a long limit. Run `cairn lint` as a command of its own and read it
  before `finish`.
- Paths and C# strings written through a shell lose backslashes. Write files with the file tools.

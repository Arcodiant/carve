---
description: >-
  Export limits per game, not Orcward's constants
---
# T-51 — Export limits per game, not Orcward's constants

**Status:** In Progress

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** T-34

## Goal

`export`'s limit warnings (triangles, colours or submeshes, humanoid height) come from the game being exported for, not from Orcward's starting numbers fixed as constants.

## Scope

- To be planned: where a game's limits live (an `export` argument, a project or content-folder setting, or a profile), what the defaults are when none is given, and whether static exports warn too.
- D-6's *Warnings for a game's limits* is revised to match.

## Notes

- Raised by the person on 2026-11-09 during M-16: the limits T-34 ships are Orcward's numbers (D-6, Leaning, Guideline) and are not general to every user. T-34 lands them as named constants on skinned exports only, as D-6 says; this task replaces that.

### As built

Planned with the person on 2026-11-13 (the Scope left all of it open):

- **Where the limits live:** `carve-limits.json` in the game's content folder. `export` uses the first one in the `.glb`'s folder or a folder above it. The new argument `limits` (a folder holding `carve-limits.json`, or any `.json` file, relative to carve's working directory) names one instead.
- **No file:** no limit warnings. The result line says `; no game limits: no carve-limits.json in <folder> or a folder above it (write_limits: true starts one there)`. When a file is used it says `; limits from <path>`.
- **Static exports** warn on `max_triangles` and `max_submeshes` (primitives or distinct colours). Height is still checked only on a skeleton holding every `SyntyRequiredBones`.
- **`write_limits: true`** (new argument) fills the keys the file is missing with Orcward's starting numbers (`GameLimits.Starting`: 5000, 8, 1.4, 2.4) and never changes a key that is set. When no file was named or found it creates one beside the `.glb`. It is written only after the `.glb` is, so a refused export (an existing file without `overwrite`, any argument error) writes neither. The result says `(created with the starting numbers)` or `(filled <keys> with the starting numbers)`.
- **`subdivide`** no longer warns past 5000 triangles: it cannot know the game. Its description now says that export warns on the game's file.

**Code:** `src/Carve.Server/GameLimits.cs` holds two types. `GameLimits` is the record of four nullable limits, with `Parse`, `ToFileText` (fixed key order, LF), `FilledFrom`, `Warnings(model, triangles, submeshes)`, `Starting` and `None`. `LimitsSource` handles `Find` (named, or searched upward), `NeedsWrite`, `Write` (through `ToolPaths.Write`, so the replace is atomic) and `Describe`. `SkinnedExport` loses its constants and `Warnings`, and `SkinnedGlb` loses `Warnings`. `ExportTool` reads the limits before writing anything, in all three modes (clips mode has no mesh, so it never warns, but it still reads and fills the file). The file format is a closed, plain JSON object (no comments, since `write_limits` rewrites the file with the keys in a fixed order; RV-39). The `limits` argument keeps `ToolPaths.Resolve`'s path rules, and a blank argument, a non-`.json` file, a folder where the file should be (RV-39), a path under an existing file (RV-40), or a path at or under the `.glb` being exported (RV-41, ruled by the person 2026-11-13) is refused before the `.glb` is written. In the file: an unknown key, a count that is not a whole number of at least 1, a height not above 0, or a `min_height` above `max_height` (including one that a fill would create) is a tool error that writes nothing. Heights are narrowed to float, so a model exactly at a limit does not warn.

**For later tasks:** call `LimitsSource.Find(tool, limitsArgument, write, glbFullPath, out error)` and then `source.Limits.Warnings(model, triangles, submeshes)`. Use `GameLimits.Starting` for Orcward's numbers. D-6's *Warnings for a game's limits* is revised to match. Its *Would change if* is now about per-kind profiles (characters and props).

**Decisions left to me:** the file name and keys; that the search runs to the filesystem root; that a named folder or file that is missing is an error unless `write_limits` is given; that limit warnings come before mesh-health warnings in the result; and that clips mode handles the file like model mode.

**Tests:** `GameLimitsTests` (unit) covers parsing, refusals, file text, warnings, the upward search, a named folder or file, and fill and create. `ExportLimitsToolTests` (regression) runs the real `carve` for: static warnings from a file found upward and none at the limit; create, then fill only the missing keys; creating beside the `.glb`; a bad or missing file writing nothing; and a refused export writing no limits file. The existing limit tests in `SkinnedExportTests` now run on `GameLimits.Starting`. Two expected targets changed, as the ruling calls for: `ExportToolTests` (the result's limits clause, and the schema `path, allow_unweighted, limits, mode, overwrite, write_limits`) and `SubdivideToolTests` (no triangle warning).

Added in answer to RV-39 and RV-40: in `ExportLimitsToolTests`, a skinned export with and without a file, clips mode filling the file, static `max_submeshes` from a coloured face, a named `.json` created by `write_limits`, a model past every starting number with no file warning on none, and a refused non-`.json` `limits` argument. Every test that searches upward first checks there is no `carve-limits.json` above the temp folder (`NoLimitsFileAboveRoot`), and fails without writing if there is. In `GameLimitsTests`, `limits` arguments no file could be at (blank, a bad character, a non-`.json` file, a folder, a path under a file, a path at or under the `.glb`) and a comment are refused. The height cases name their own bound.

**Not verified:** no scenario shows a limits file. scenario-export, character and gatehouse export with no file, so their checks (no `warning(s)`, byte-identical `.glb`) still hold, but the scenarios were not extended to write or read a `carve-limits.json`. The upward search assumes no `carve-limits.json` exists above the test temp folder on the machine running the tests. Every test that searches upward checks for one first and fails without writing. No tool-level test exports a skinned model past a starting number with no file: the chain is small, and the 'no file' path is the same `LimitsSource` for every mode.

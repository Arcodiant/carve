---
description: >-
  The whole model saves to and reloads from the Carve file, and version 1 files still open
---
# T-35 — Carve file version 2

**Status:** Done

**Pri:** P1

**Milestone:** M-16

**Batch:** — (standalone)

**Depends on:** T-33

**Verify:** server-regression, scenario-cube, scenario-crate, scenario-shed, scenario-export

**Perf:** skip: Carve declares no heavy suites

## Goal

The whole character survives a restart: `save` and `open` carry meshes, colours, groups, skeleton, weights and clips.

## Scope

- **`CarveFile` version 2:** a `meshes` list of named objects (`vertices`, `faces`, `colours`, `groups`, `weights`), `skeleton` and `clips`, in a fixed key order so the same model writes the same text and diffs mean something. Floats read back bit-exact.
- **Version 1 files** open as one mesh called `mesh`. `Write` always gives version 2.
- **Errors** name the element, as T-15's do. Reading holds a file to exactly what the tools accept: the same refusals for bones, weights and clips.
- **`open`** replaces the whole model as one undo step.
- **Description:** `save` shows a short version 2 file.
- **Tests:** `unit` for round-trip bit-exactness of every field, text stability, version 1 still reading, and each refusal; `regression` for a save, a new process, `open` and an identical `get_mesh`, `get_skeleton` and frame.

## Notes

- The format is public and committed by games, so run on the stronger model.
- T-42 adds its keys as version 3, keeping 1 and 2 readable.
- **Carried from the M-1 to M-4 plans:** the byte-identical guard on every existing frame, by hash before and after; a positive pixel check on every new frame, and one a mirrored or flipped camera would fail; each new check shown failing, as a table.
- **Open rules: stop and ask.** Anything D-6 does not settle, any loosened check, and any change to a D-6 decision.
- Run `finish` from inside the worktree and leave the folder.

### As built

Built by three agents, all on Opus 5.5 (2026-11-09): the first wrote the format, the tools and the tests up to a 60-call hand-off; the second applied ruling 3 below, ran the suites and the break table, and wrote this up to a second hand-off; the third took the frame hashes after and the new `crate.carve` hash, and finished.

**Version 2, exactly** (games commit it; T-36 and T-42 build on it). `CarveFile.Write(Model)` always writes version 2, two-space indent, `\n` line ends and a final newline, so the same model writes the same text. Top-level keys in this order: `carve` (2), `meshes`, `skeleton`, `clips`. Each mesh is an object with keys in this order: `name`; `vertices`, one `[x, y, z]` a line; `faces`, one vertex-id list a line; `colours`, `{"colour", "faces"}` one a line, one entry per colour, its faces ascending, entries by their first face; `groups`, `{"name", "faces"}` in the model's group order; `weights`, `{"vertex", "bones": {bone name: weight}}`, vertices ascending, bones in skeleton order. An empty list is written `[]`. `skeleton` is `null` or a list of `{"name", "parent" (a bone name or null), "position" [x, y, z], "rotation" [x, y, z, w]}` one a line, in skeleton order. A clip is `{"name", "duration", "loop", "channels": {bone: {"rotation"?, "translation"?: {"interpolation", "keys": [{"time", "value"}]}}}}`, channels in the clip's order, rotation before translation. Floats are written round-trip (`R`, invariant culture), so `1` not `1.0`, and read back bit-exact; weights are doubles written the same way. Pose values, clip keys and the sampler's output are absolute local transforms, as ruled. The documented texts are `CarveFileV2Tests.ArmText` (every field, below) and `.CubeText` (a mesh with nothing else: `"colours": []`, `"groups": []`, `"weights": []`, `"skeleton": null`, `"clips": []`); `SaveTool.CubeFile` is the version 2 cube in `save`'s description.

```json
{
  "carve": 2,
  "meshes": [
    {
      "name": "arm",
      "vertices": [
        [0, 0, 0],
        [1, 0, 0],
        [0, 1, 0]
      ],
      "faces": [
        [0, 1, 2]
      ],
      "colours": [
        {"colour": "#C8503C", "faces": [0]}
      ],
      "groups": [
        {"name": "all", "faces": [0]}
      ],
      "weights": [
        {"vertex": 0, "bones": {"base": 1}},
        {"vertex": 2, "bones": {"base": 0.25, "joint": 0.75}}
      ]
    }
  ],
  "skeleton": [
    {"name": "base", "parent": null, "position": [0, 0, 0], "rotation": [0, 0, 0, 1]},
    {"name": "joint", "parent": "base", "position": [0, 1, 0], "rotation": [0, 0, 0, 1]}
  ],
  "clips": [
    {
      "name": "wave",
      "duration": 1,
      "loop": true,
      "channels": {
        "joint": {
          "rotation": {
            "interpolation": "LINEAR",
            "keys": [
              {"time": 0, "value": [0, 0, 0, 1]},
              {"time": 0.5, "value": [0, 0, 0.70710677, 0.70710677]},
              {"time": 1, "value": [0, 0, 0, 1]}
            ]
          },
          "translation": {
            "interpolation": "STEP",
            "keys": [
              {"time": 0, "value": [0, 1, 0]}
            ]
          }
        }
      }
    }
  ]
}
```

**Reading.** `CarveFile.Read` dispatches on `carve`: 2 is read whole; anything else goes through T-15's version 1 path unchanged, which opens one mesh named `mesh` and refuses other versions, its messages now saying "versions 1 and 2". `CarveFileResult` has `Model`, `Version` (1 or 2, 0 when refused) and `Mesh` (the only mesh when there is one). A file is held to exactly what the tools accept, through refusals moved into the Core and shared by tools and reader: `VertexWeights.Raw` and `.VertexProblem` (from `set_weights`), `FaceStyle.FaceProblem` and `.GroupNameProblem`, `Model.Create` (names, weights need a skeleton, duplicate clip names), `Skeleton.Create`, `Clip.Create`, `PolygonMesh.Create`. Each error names its element, e.g. `meshes[0].faces[3][3]`, `clips[1].duration`.

**`save` and `open`.** `save` with no `mesh` writes the whole model; with `mesh`, `Model.Piece`. `open` with no `mesh` replaces the whole model as one undo step (`ModelStore.OpenModel`); with `mesh`, `ModelStore.OpenMesh` / `Model.WithMeshFrom`, weights moved onto the model's bones by name. Both descriptions say all of it.

**The person's rulings, as ruled:**

- (2026-11-09) With no `mesh`, `save` writes the whole model, and `open` replaces the whole model as one undo step. With a `mesh` name they work on just that mesh: `save` writes that mesh plus the skeleton and clips its weights need, and `open` takes only that named mesh from the file, replacing or adding it, refused with the model unchanged if its weights name bones the open model lacks.
- (2026-11-09) Committed and baselined `.carve` files are compared by content, not bytes. The shed sample is v2 (`samples/leaky-shed.carve`, text equal to what `Write` gives), with a v1 copy kept as a fixture (`tests/Carve.Core.Tests/Fixtures/leaky-shed-v1.carve`); `MeshHealthTests.The_shed_file_is_the_code_s_mesh` compares both by content. The export scenario's `crate.carve` gets a new hash, an expected baseline update: sha256 `4367b294854572032016715c50ffdd1cafa15aaaf4b6146dd79e5828ade709cc` before, `4fc73e0f961d5a46ba4a00d849b323d69fdda601be1671fbbbc29d9e191fa95e` (export job J-20261109-143231-78b221, at ef33d56) after. The scenario's only check on it is its own re-save byte check, which still holds.
- Pose values, clip keys and the sampler's output are absolute local transforms.
- (2026-11-09, on the hand-off) Accepted: the changed test targets in `NamedMeshTests` (no refusal of save/open with several meshes and no `mesh`; "a name the file lacks is refused; the file's `lid` replaces the model's `lid`") and the message texts "versions 1 and 2" and "the model is unchanged" (in `SaveOpenToolTests` and `CarveFileTests`, where version 3 is now the unread version).
- (2026-11-09) Accepted: `save` with `mesh` on an unweighted mesh writes the mesh only, with no skeleton or clips.
- (2026-11-09) For a version 1 file only, `open` with any `mesh` name takes its single mesh and places it under that name, replacing or adding it (`OpenTool`, on `CarveFileResult.Version`; test `CarveFileVersion2Tests.Open_with_mesh_places_a_version_1_files_one_mesh_under_the_name_given`). For a version 2 file the name must exist in the file.
- (2026-11-09) Accepted: already-normalised weights in a file (at most 4, sum within 1e-12 of 1, `VertexWeights.Kept`) are kept bit for bit, and others are normalised and capped as `set_weights` does; duplicates in a file (mesh name, group name, a face coloured twice, a vertex weighted twice, clip name) are refused; `colours`, `groups`, `weights`, `skeleton`, `clips` and `loop` may be left out; a file with no meshes is refused; `open`'s report adds `bones` and `clips` when there is a skeleton, and a `meshes` list when there are several.

**Tests.** `CarveFileV2Tests` (unit: round-trip bit-exactness of every field, text stability, the documented texts, version 1 reading, each refusal, `Piece` and `WithMeshFrom`); `CarveFileVersion2Tests` (regression: the whole model across processes with identical `get_mesh`, `get_skeleton`, rest and clip frames and re-saved bytes; `open` replacing the whole model as one step; `save` and `open` of one mesh by bone name; a version 1 file opened under a name). `get_mesh` does not report weights, so the regression tests check them through a re-saved file. `EditToolTests.Tools_list_shows_each_schema` needed no update: `server-regression` passed with it unchanged, so the allowed expected-schema update was not used.

**Checked.** `server-regression` passed (job J-20261109-142340-fd46ed, 321 s). Every frame is byte-identical: the 81 frames of `scenario-cube`, `-crate`, `-shed` and `-export` hash the same before and after (`M-16/artifacts/review/T-35/hashes-before.txt`, `hashes-after.txt`; crate, shed and export from this branch's queued jobs at ef33d56, cube from a run here at d5f3f63 with `--fresh`, since the carried cube job's artifacts were shadowed by a sibling's later job). After rebasing onto T-34, all four suites' queued jobs at cc54057 (J-20261109-144222-9585d3, -144224-7a8fd3, -144228-ee122f, -144230-6f153b) give the same 81 hashes again (`hashes-after-rebase.txt`) and the same `crate.carve` hash. `scenario-shed` and `scenario-export` were added to **Verify:**, since the shed sample and the export scenario's saved file both change; `scenario-shed` then failed on its own reader of the sample, which works the expected pixels from the file apart from the product (`ShedGeometry.Shape.Parse`), and which now reads the one mesh of a version 2 file's `meshes` as well as a version 1 file's top level, its checks unchanged. Each new check is shown failing on a broken product in `M-16/artifacts/review/T-35/break-table.md` (16 breaks, each failing its checks, and a reformatted-shed control passing, as content comparison should). No new frames, so no new positive pixel check; the round-trip frames in `CarveFileVersion2Tests` are compared by bytes across processes.

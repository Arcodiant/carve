---
description: >-
  PngWriter and the no-package guard: three Nits from RV-81
---
# T-196 — PngWriter and the no-package guard: three Nits from RV-81

**Status:** Open

**Pri:** P2

**Milestone:** — (off-milestone)

**Batch:** — (standalone)

**Depends on:** — (none)

**Verify:** patina-unit

## Goal

The three Nits RV-81 found in M-33's fix commits are closed: the no-package guard holds however a package is declared,
the size refusal's limit is pinned by a test, and the text says where the `IDAT` bound comes from.

## Scope

- **`TheProjectFileDeclaresNoPackage`** (`tests/Patina.Graphs.Tests/PatinaGraphsIsEngineFreeTests.cs`) matches
  `PackageReference` case-sensitively and reads only `Patina.Graphs.csproj`. It passes with a lower-case
  `<packagereference>` (tested by the reviewer) and would not see one added by `Directory.Build.props` or a
  `.targets` (inferred). Compare names case-insensitively, or read the restored `project.assets.json`. Show it
  failing on both cases.
- **`RefusesASizeItCannotEncodeBeforeWritingAByte`** (`tests/Patina.Graphs.Tests/PngWriterTests.cs`) refuses
  262,144 x 2047, whose batch copy is past `int.MaxValue` as well as `Array.MaxLength`, so the test passes with the
  limit changed to `int.MaxValue`. Add a refused size just past `Array.MaxLength` (268,435,455 x 1 is one, at
  2,147,483,640 bytes) and one just under it that goes on to the length check. Neither needs a large buffer.
- **The `IDAT` bound's name.** `src/Patina.Graphs/Imaging/PngWriter.cs` and T-187's "Added for RV-80" line call
  `n + (n >> 12) + (n >> 14) + (n >> 25) + 7` zlib's `deflateBound`. .NET 10.0.15 bundles zlib-ng 2.2.5, whose own
  bound is looser. Say that the formula is madler zlib's, assumed for zlib-ng at level 6, and that the runtime check
  before each `IDAT` length is what holds if it is ever passed.
- **No byte of any PNG changes.** The canary and every byte-for-byte check pass as they are.
- Nothing a user or an agent can reach changes, so `README.md` is not touched.

## Notes

- Filed from [RV-81](../reviews/RV-81-review-of-m-33-checking-rv-80-s-answers.md) by the person's choice
  (2026-11-23), in place of a third review round on M-33. Nothing ships wrong: carve's largest export is 8192 square.
- Not checked by the reviewer: zlib-ng's real worst case at level 6, and an image near 2 GiB written.

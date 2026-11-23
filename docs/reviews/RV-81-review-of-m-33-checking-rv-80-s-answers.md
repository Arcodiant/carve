---
description: >-
  Review of M-33, checking RV-80's answers
---
# RV-81 — Review of M-33, checking RV-80's answers

**Status:** Answered

**Reviews:** M-33

**Round:** 2

**Previous:** RV-80

**Diff:** b62e21f05edef4bbbf9a5cb28ac8c31001263bf4 against origin/main

**Reviewer:** one reviewer, claude-opus-5-5 (Claude Opus 5.5), checking RV-80's nine answers against the code and reviewing the two fix commits

<!-- Diff: `<patch-id> against <base>`, the `git patch-id --stable` of `git diff <base>...HEAD`
     with the tracker's docs/ left out (reviews.diff_patch_id computes it). Reviewer: the model
     or agent that reviewed. The reviewer writes one ### per finding under ## Findings, saying
     what was found and where, with an **Outcome:** line left as —; the implementer fills each
     with `fixed`, `skipped: <reason>` or `no change needed: <reason>`, then sets
     **Status:** Answered. Several reviewers in one round write into this one file, each under
     its own `## Findings: <its part>` heading in place of ## Findings; every ### under any of
     them is a finding. See the cairn skill's references/artifacts.md and orchestration.md. -->

## Findings: RV-80's answers and the fix commits (claude-opus-5-5)

### `TheProjectFileDeclaresNoPackage` misses a package reference written in lower case, or added by an imported file
- **Where:** `tests/Patina.Graphs.Tests/PatinaGraphsIsEngineFreeTests.cs:37-54`
- **Severity:** Nit
- **What is wrong:** The test matches `e.Name.LocalName == "PackageReference"`, which is case-sensitive, but MSBuild item types are not case-sensitive. In a `git archive HEAD` copy under `%TEMP%` I added `<packagereference Include="StbImageWriteSharp" Version="$(StbImageWriteSharpVersion)" />` to `Patina.Graphs.csproj`. Restore took it up (`StbImageWriteSharp/1.16.7` is in `src/Patina.Graphs/obj/project.assets.json`), and all 38 tests in `PngWriterTests` and `PatinaGraphsIsEngineFreeTests` passed, this test included. The test also reads only the csproj, so it would not see a `PackageReference` that a `Directory.Build.props` or `.targets` adds (inferred; I did not test that case). RV-80 finding 9 asked for a guard on any package declared, referenced but not used. The fix catches only the exact spelling in the one file. Comparing names case-insensitively, or reading the restored `project.assets.json` (the project's resolved dependencies), would catch both cases. Rubric 2. Tested (lower case); inferred (imported file).

**Outcome:** skipped: filed off-milestone as T-196 by the person's choice (2026-11-23); nothing ships wrong

### The size-refusal test does not check the `Array.MaxLength` limit it claims
- **Where:** `tests/Patina.Graphs.Tests/PngWriterTests.cs:141-157` (against `src/Patina.Graphs/Imaging/PngWriter.cs:156-161`)
- **Severity:** Nit
- **What is wrong:** The refusal's limit is `Array.MaxLength` (2,147,483,591). The test's refused size is 262,144 x 2047, whose batch copy is 2^31 bytes, past `int.MaxValue` as well. In a scratch copy I changed the limit to `if (copy > int.MaxValue)` and every test in `PngWriterTests` and `PatinaGraphsIsEngineFreeTests` still passed. With that change, a size whose copy falls between the two limits would get past the refusal. One example is 268,435,455 x 1, whose copy is 2,147,483,640 bytes. `Write` would then write the signature and `IHDR`, and `AllocateUninitializedArray` would throw. That leaves the partial PNG RV-80 finding 5 was about. A refused case just past `Array.MaxLength`, with one just under it that goes on to the length check, would pin the limit. Rubric 2. Tested (the change to the limit); inferred (what an image of that size would do).

**Outcome:** skipped: filed off-milestone as T-196 by the person's choice (2026-11-23); nothing ships wrong

### The `IDAT` bound is given as zlib's `deflateBound`, but the runtime's zlib is zlib-ng
- **Where:** `src/Patina.Graphs/Imaging/PngWriter.cs:147-172`, `docs/tasks/T-187-a-png-writer-on-net-s-zlib-in-patina-graphs.md` (the "Added for RV-80" line)
- **Severity:** Nit
- **What is wrong:** The refusal limits each band to `n + (n >> 12) + (n >> 14) + (n >> 25) + 7`. That is madler zlib's tight `deflateBound` for its default parameters. .NET 10.0.15's `System.IO.Compression.Native.dll` contains the strings "zlib-ng 2.2.5", and zlib-ng's `deflateBound` is not this formula: it allows for `deflate_quick`'s 9-bit literals, about n/8. So the code comment and T-187's As built call this "zlib's deflateBound", but it is not a bound the runtime's zlib promises. The writer is still safe, because the new runtime check throws `InvalidOperationException` before an oversize length is written. But that check runs after the signature, the `IHDR` and any earlier `IDAT`s are on the stream, and the "refused before a byte is written" guarantee rests on this bound. Saying that the bound is zlib's formula, assumed for zlib-ng at level 6, and not the runtime's own, would make the text accurate. Rubric 1 and 7. Tested that the runtime bundles zlib-ng; zlib-ng's formula is from its source, inferred.

**Outcome:** skipped: filed off-milestone as T-196 by the person's choice (2026-11-23); nothing ships wrong

## Summary

One reviewer, since the round's diff is the two fix commits (`f9f1ec4`, `71075cc`) and its job is whether RV-80's nine
answers hold. All nine hold.

- **The bytes are unchanged by the fixes.** The writer at `8b5eba1` and at HEAD gave the same bytes for 26 images
  (smooth, noise, zeros and mixed; sizes not multiples of 64, more than 2048 and 4096 rows, 2048, 4096 and 8192
  square), also at 1 thread, and the canary's hash is reproduced. `git diff 8b5eba1 HEAD --stat -- samples` is empty.
- **Finding 5's refusal** was worked for every batch height from 1 to 2048: the largest `IDAT` bound while the batch
  copy fits is 2,147,092,584 bytes, so the `IDAT` refusal cannot be reached. Only refused sizes change their
  exception, to a subclass of the old one, and nothing depends on the old type.
- **Breaks made in a scratch copy:** the Adler combine without its modulus fails the new test alone; the refusal
  commented out fails its test.
- No byte-for-byte check, tolerance or pinned digest changed in the fix commits.
- `Patina.Graphs.Tests` passed 499 of 499 and `Patina.Server.Tests`' unit tests 163, with 6 skipped, in a copy of
  HEAD. Not run by the reviewer: the regression tests, the scenarios, finding 4's `BandsPerBatch` break again, an
  image near 2 GiB.

3 findings, all Nit.

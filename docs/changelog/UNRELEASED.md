# Unreleased

- T-187: Patina.Graphs gains PngWriter: RGBA8 PNGs with the up filter, bands of 64 rows deflated in parallel on .NET's zlib and stitched, the same bytes at any thread count, with a canary test that pins them to .NET 10's zlib

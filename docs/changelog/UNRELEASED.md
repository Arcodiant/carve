# Unreleased

- T-187: Patina.Graphs gains PngWriter: RGBA8 PNGs with the up filter, bands of 64 rows deflated in parallel on .NET's zlib and stitched, the same bytes at any thread count, with a canary test that pins them to .NET 10's zlib
- T-188: Png.Encode and export write through PngWriter, so an 8192 output encodes in well under a second; every sample PNG under samples/graphs/ is written again with its texels unchanged, and Patina.Graphs drops StbImageWriteSharp
- T-189: export's description, the README and T-115's record carry the 8192 times, PNG sizes and peak memory measured with the new encoder, in Release and Debug

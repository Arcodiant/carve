using Carve.Core.Baking;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// <see cref="Bake.Pad"/> against the padding T-120 shipped, kept here as written (a scan along each row, size squared
/// times padding): bit-identical on islands laid out across ties, at paddings from 1 to half the size (RV-74).
/// </summary>
[Trait("Tier", "unit")]
public class BakePadReferenceTests
{
    [Fact]
    public void Pad_matches_the_row_scan_it_replaced_bit_for_bit()
    {
        var layouts = new List<BakeLayout>
        {
            BakeLayoutTests.Lay(BakeLayoutTests.Celled(TestMeshes.Cube()), 64),
            BakeLayoutTests.Lay(BakeLayoutTests.Celled(TestMeshes.LBlock()), 128),
            BakeLayoutTests.Lay(BakeLayoutTests.Celled(TestMeshes.Cube()), 64, [2]),
            BakeLayoutTests.Lay(BakeLayoutTests.Celled(TestMeshes.Grid(3, 2)), 128, [0, 4]),
        };
        // Quads on whole and half texels in four bands, so many uncovered texels are as near two islands.
        var random = new Random(74);
        for (var n = 0; n < 8; n++)
        {
            var size = n % 2 == 0 ? 64 : 128;
            var uvs = MeshUvs.None;
            for (var f = 0; f < 4; f++)
            {
                if (random.Next(3) == 0 && f > 0) continue;
                float At(int lo, int hi) => (random.Next(lo * 2, hi * 2) / 2f) / size;
                var x0 = At(0, size / 2); var x1 = x0 + At(1, size / 3);
                var y0 = f / 4f + At(0, size / 16); var y1 = y0 + At(1, size / 8);
                uvs = uvs.WithFace(f, [new(x0, y1), new(x1, y1), new(x1, y0), new(x0, y0)]);
            }
            layouts.Add(BakeLayoutTests.Lay(TestMeshes.Cube().WithUvs(uvs), size));
        }

        foreach (var layout in layouts)
        {
            var size = layout.Size;
            var image = layout.NewImage(2);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++) { image[x, y, 0] = y * size + x; image[x, y, 1] = layout.IsCovered(x, y) ? 1 : -7; }
            foreach (var texels in new[] { 1, 2, 3, 5, 8, 13, size / 4, size / 2 - 1, size / 2 })
                Assert.True(Bake.Pad(image, layout, texels).Values.SequenceEqual(Reference(image, layout, texels).Values),
                    $"at {size} with padding {texels}");
        }
    }

    /// <summary>T-120's <c>Bake.Pad</c> as it was, after its argument checks.</summary>
    private static BakeImage Reference(BakeImage image, BakeLayout layout, int texels)
    {
        var size = layout.Size;
        var result = image.Clone();
        if (texels == 0 || layout.CoveredCount == 0) return result;

        // Down each column, the nearest covered row (the upper on a tie: the lower row-major index), or -1 past reach.
        var near = new int[size * size];
        System.Threading.Tasks.Parallel.For(0, size, x =>
        {
            var last = -1;
            for (var y = 0; y < size; y++)
            {
                if (layout.IsCovered(x, y)) last = y;
                near[y * size + x] = last >= 0 && y - last <= texels ? last : -1;
            }
            var next = -1;
            for (var y = size - 1; y >= 0; y--)
            {
                if (layout.IsCovered(x, y)) next = y;
                if (next < 0 || next - y > texels) continue;
                var i = y * size + x;
                if (near[i] < 0 || next - y < y - near[i]) near[i] = next;
            }
        });

        // Along each row, the covered texel nearest in 2D: each row's answer depends on nothing another row writes.
        var limit = (long)texels * texels;
        var channels = image.Channels;
        var source = image.Values.ToArray();
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            var values = new float[channels];
            for (var x = 0; x < size; x++)
            {
                if (layout.IsCovered(x, y)) continue;
                var best = long.MaxValue;
                var bestIndex = -1;
                for (var dx = 0; dx <= texels; dx++)
                {
                    var dx2 = (long)dx * dx;
                    if (dx2 > best || dx2 > limit) break;
                    for (var side = dx == 0 ? 1 : 0; side < 2; side++)
                    {
                        var sx = side == 0 ? x - dx : x + dx;
                        if ((uint)sx >= (uint)size) continue;
                        var row = near[y * size + sx];
                        if (row < 0) continue;
                        var dy = row - y;
                        var d2 = dx2 + (long)dy * dy;
                        if (d2 > limit) continue;
                        var index = row * size + sx;
                        if (d2 < best || (d2 == best && index < bestIndex)) { best = d2; bestIndex = index; }
                    }
                }
                if (bestIndex < 0) continue;
                source.AsSpan(bestIndex * channels, channels).CopyTo(values);
                values.CopyTo(result.Texel(x, y));
            }
        });
        return result;
    }
}

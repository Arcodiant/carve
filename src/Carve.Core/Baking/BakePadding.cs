namespace Carve.Core.Baking;

/// <summary>
/// The bakes (M-24): each map is a method here, built on a <see cref="BakeLayout"/>, one file each. All are engine-free,
/// at rest and deterministic: two runs give bit-identical floats. A bake is never stored in the Carve file.
/// </summary>
public static partial class Bake
{
    /// <summary>
    /// <paramref name="image"/> padded past its islands: a copy in which every texel <paramref name="layout"/> leaves
    /// uncovered, within <paramref name="texels"/> of the nearest covered texel (Euclidean distance between texel centres,
    /// in texels), takes that covered texel's values; a tie goes to the lowest row-major index. Covered texels are never
    /// changed, so padding never bleeds into another island, and texels further out keep their values. Padding goes from
    /// 0 (the image as it was) to half the layout's size.
    /// </summary>
    public static BakeImage Pad(BakeImage image, BakeLayout layout, int texels)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layout);
        var size = layout.Size;
        if (image.Width != size || image.Height != size)
            throw new ArgumentException($"the image is {image.Width} x {image.Height} but the layout is {size} x {size}", nameof(image));
        if (texels < 0 || texels > size / 2)
            throw new ArgumentOutOfRangeException(nameof(texels), texels, $"padding goes from 0 to {size / 2} texels at {size}");

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

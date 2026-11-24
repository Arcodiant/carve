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

        // Along each row, the covered texel nearest in 2D, as the lower envelope of one parabola per column that has a
        // nearest row: column c offers (x - c)^2 + dy(c)^2 at index row(c) * size + c, and a texel takes the least
        // (distance, index) pair. Two columns' offers cross once along the row, so the envelope is built left to right in
        // one pass with integer switch points, exact and linear in the row (Felzenszwalb and Huttenlocher's transform with
        // the tie rule folded into where one column takes over from the last). Each row's answer depends on nothing
        // another row writes.
        var limit = (long)texels * texels;
        var channels = image.Channels;
        var source = image.Values.ToArray();
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            var hull = new int[size];
            var from = new long[size];
            var count = 0;
            for (var c = 0; c < size; c++)
            {
                if (near[y * size + c] < 0) continue;
                var start = long.MinValue;
                while (count > 0)
                {
                    start = TakesOver(near, size, y, hull[count - 1], c);
                    // The last column wins nowhere once the new one takes over no later than the last took over.
                    if (count > 1 && start <= from[count - 1]) { count--; continue; }
                    break;
                }
                hull[count] = c;
                from[count] = start;
                count++;
            }
            if (count == 0) return;
            var k = 0;
            for (var x = 0; x < size; x++)
            {
                while (k + 1 < count && from[k + 1] <= x) k++;
                if (layout.IsCovered(x, y)) continue;
                var sx = hull[k];
                var row = near[y * size + sx];
                long dx = x - sx, dy = row - y;
                if (dx * dx + dy * dy > limit) continue;
                source.AsSpan((row * size + sx) * channels, channels).CopyTo(result.Texel(x, y));
            }
        });
        return result;
    }

    /// <summary>
    /// Sets every channel of each texel <paramref name="layout"/> leaves uncovered to <paramref name="value"/>, in place, and
    /// returns <paramref name="image"/>. Before <see cref="Pad"/>, it is what lies beyond the padding: a map's neutral value
    /// there, so a bake and the mesh node that reads it hold the same image over the whole square (T-165).
    /// </summary>
    public static BakeImage FillUncovered(BakeImage image, BakeLayout layout, float value)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(layout);
        for (var y = 0; y < layout.Size; y++)
            for (var x = 0; x < layout.Size; x++)
                if (!layout.IsCovered(x, y)) image.Texel(x, y).Fill(value);
        return image;
    }

    /// <summary>
    /// The first column x along row <paramref name="y"/> at which column <paramref name="q"/>'s nearest covered texel beats
    /// column <paramref name="p"/>'s (p &lt; q): nearer, or as near with the lower row-major index.
    /// </summary>
    private static long TakesOver(int[] near, int size, int y, int p, int q)
    {
        long rp = near[y * size + p], rq = near[y * size + q];
        long gp = (rp - y) * (rp - y), gq = (rq - y) * (rq - y);
        // p's offer less q's is d * x - n, rising with x: q is nearer once d * x > n, as near at d * x == n.
        var n = (long)q * q + gq - (long)p * p - gp;
        var d = 2L * (q - p);
        var floor = n >= 0 ? n / d : -((-n + d - 1) / d);
        var tieToQ = rq * size + q < rp * size + p;
        return floor * d == n && tieToQ ? floor : floor + 1;
    }
}

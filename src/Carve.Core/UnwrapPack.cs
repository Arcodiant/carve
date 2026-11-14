namespace Carve.Core;

/// <summary>
/// Turns each flattened island so its minimum-area bounding rectangle is axis-aligned and wider than tall, then
/// shelf-packs the islands at one common scale and fits them into the target rectangle (T-56). Rotations are built
/// from normalised direction vectors with <c>Math.Sqrt</c> only, so no trig enters and nothing is mirrored.
/// </summary>
internal static class UnwrapPack
{
    /// <summary>A later hull edge's rectangle replaces the kept one only when smaller by more than this, relative; the same tolerance decides "taller than wide".</summary>
    public const double TieFraction = 1e-9;

    /// <summary>The bisection's fixed step count.</summary>
    public const int FitSteps = 60;

    /// <summary>Rotates the points (screen axes) in place and moves their rectangle's min corner to the origin; returns its size.</summary>
    public static (double W, double H) Orient(double[] u, double[] v)
    {
        var hull = Hull(u, v);
        double bestArea = double.MaxValue, bestCx = 1, bestCy = 0;
        for (var i = 0; i < hull.Count; i++)
        {
            var (a, b) = (hull[i], hull[(i + 1) % hull.Count]);
            double dx = u[b] - u[a], dy = v[b] - v[a];
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len == 0) continue;
            double cx = dx / len, cy = dy / len;
            double minS = double.MaxValue, maxS = double.MinValue, minT = double.MaxValue, maxT = double.MinValue;
            foreach (var h in hull)
            {
                double s = cx * u[h] + cy * v[h], t = -cy * u[h] + cx * v[h];
                minS = Math.Min(minS, s); maxS = Math.Max(maxS, s);
                minT = Math.Min(minT, t); maxT = Math.Max(maxT, t);
            }
            var area = (maxS - minS) * (maxT - minT);
            if (bestArea == double.MaxValue || area < bestArea * (1 - TieFraction))
            {
                bestArea = area;
                (bestCx, bestCy) = (cx, cy);
            }
        }

        // Rotate the chosen edge onto +u: (u, v) -> (c u + s v, -s u + c v), determinant 1.
        for (var i = 0; i < u.Length; i++)
            (u[i], v[i]) = (bestCx * u[i] + bestCy * v[i], -bestCy * u[i] + bestCx * v[i]);
        var (w, h0) = Size(u, v);
        if (h0 > w * (1 + TieFraction))
            for (var i = 0; i < u.Length; i++)
                (u[i], v[i]) = (-v[i], u[i]); // a quarter turn, determinant 1
        double minU = double.MaxValue, minV = double.MaxValue;
        for (var i = 0; i < u.Length; i++) { minU = Math.Min(minU, u[i]); minV = Math.Min(minV, v[i]); }
        for (var i = 0; i < u.Length; i++) { u[i] -= minU; v[i] -= minV; }
        return Size(u, v);
    }

    private static (double W, double H) Size(double[] u, double[] v)
    {
        double minU = double.MaxValue, maxU = double.MinValue, minV = double.MaxValue, maxV = double.MinValue;
        for (var i = 0; i < u.Length; i++)
        {
            minU = Math.Min(minU, u[i]); maxU = Math.Max(maxU, u[i]);
            minV = Math.Min(minV, v[i]); maxV = Math.Max(maxV, v[i]);
        }
        return (maxU - minU, maxV - minV);
    }

    /// <summary>Andrew's monotone chain from the lowest (u, v), collinear points dropped.</summary>
    private static List<int> Hull(double[] u, double[] v)
    {
        var idx = new int[u.Length];
        for (var i = 0; i < idx.Length; i++) idx[i] = i;
        Array.Sort(idx, (a, b) => u[a] != u[b] ? u[a].CompareTo(u[b]) : v[a] != v[b] ? v[a].CompareTo(v[b]) : a.CompareTo(b));
        double Cross(int o, int a, int b) => (u[a] - u[o]) * (v[b] - v[o]) - (v[a] - v[o]) * (u[b] - u[o]);
        var hull = new List<int>();
        foreach (var p in idx)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        var lower = hull.Count + 1;
        for (var k = idx.Length - 2; k >= 0; k--)
        {
            var p = idx[k];
            while (hull.Count >= lower && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    /// <summary>
    /// Shelf-packs islands of sizes (w, h) in metres, in the given order, at the largest common scale that fits
    /// <paramref name="into"/> with margin <paramref name="m"/> between islands and to every edge; null when none fits.
    /// </summary>
    public static (double Scale, double[] U, double[] V)? Fit(IReadOnlyList<(double W, double H)> sizes, int[] order,
        (double U0, double V0, double U1, double V1) into, double m)
    {
        double maxW = 0, maxH = 0;
        foreach (var (w, h) in sizes) { maxW = Math.Max(maxW, w); maxH = Math.Max(maxH, h); }
        var sMax = Math.Min((into.U1 - into.U0 - 2 * m) / maxW, (into.V1 - into.V0 - 2 * m) / maxH);
        double lo = 0, hi = sMax;
        var u = new double[sizes.Count];
        var v = new double[sizes.Count];
        for (var step = 0; step < FitSteps; step++)
        {
            var mid = (lo + hi) / 2;
            if (Shelves(sizes, order, into, m, mid, u, v)) lo = mid;
            else hi = mid;
        }
        if (lo == 0 || !Shelves(sizes, order, into, m, lo, u, v)) return null;
        return (lo, u, v);
    }

    private static bool Shelves(IReadOnlyList<(double W, double H)> sizes, int[] order,
        (double U0, double V0, double U1, double V1) into, double m, double s, double[] u, double[] v)
    {
        double x = into.U0 + m, y = into.V0 + m, shelf = 0;
        var first = true;
        foreach (var i in order)
        {
            double w = s * sizes[i].W, h = s * sizes[i].H;
            if (!first && x + w > into.U1 - m)
            {
                y += shelf + m;
                x = into.U0 + m;
                shelf = 0;
            }
            if (x + w > into.U1 - m || y + h > into.V1 - m) return false;
            u[i] = x;
            v[i] = y;
            x += w + m;
            shelf = Math.Max(shelf, h);
            first = false;
        }
        return true;
    }
}

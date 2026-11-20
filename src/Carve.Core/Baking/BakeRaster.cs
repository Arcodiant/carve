namespace Carve.Core.Baking;

public sealed partial class BakeLayout
{
    /// <summary>
    /// The coverage pass behind <see cref="Create(PolygonMesh, int, IEnumerable{int}?)"/>, sequential so it is the same
    /// every run. Work is in texel space: uv (u, v) is the point (u * size, v * size), and texel (x, y)'s centre is
    /// (x + 0.5, y + 0.5), y down. Every coordinate is a float uv times a power of two, so each edge function below is
    /// exact in double (24-bit operands, products under 53 bits) and two triangles sharing an edge see exactly opposite
    /// values along it.
    /// </summary>
    internal sealed class Raster
    {
        public required int[] Slot { get; init; }
        public required int[] Triangle { get; init; }
        public required float[] Weights { get; init; }
        public required bool[] Clamped { get; init; }
        public required IReadOnlyList<(int Lo, int Hi)> Overlaps { get; init; }

        public static Raster Run(int size, IReadOnlyList<MeshTriangle> triangles, Corners?[] corners)
        {
            var owner = new int[size * size];
            Array.Fill(owner, -1);
            var overlaps = new SortedSet<(int Lo, int Hi)>();
            var holders = new Dictionary<int, List<int>>();
            var reach = ClampReach;
            var reach2 = 0.5; // ClampReach squared, exactly
            var shapes = new Shape?[triangles.Count];
            for (var t = 0; t < triangles.Count; t++)
                if (corners[t] is { } c && Oriented(c, size, out var p, out var map, out var area)) shapes[t] = new Shape(p, map, area);

            // Pass 1: each texel centre a triangle holds, by the top-left rule.
            for (var t = 0; t < triangles.Count; t++)
            {
                if (shapes[t] is not { } shape) continue;
                var p = shape.P;
                var (x0, x1, y0, y1) = Span(p, size, 0);
                for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    if (Holds(shape, x, y) is null) continue;
                    var i = y * size + x;
                    if (owner[i] >= 0)
                    {
                        // Every pair of faces that hold this centre, not only the first holder's.
                        if (!holders.TryGetValue(i, out var seen)) holders[i] = seen = [triangles[owner[i]].Face];
                        var g = triangles[t].Face;
                        foreach (var f in seen) overlaps.Add(f <= g ? (f, g) : (g, f));
                        seen.Add(g);
                        continue;
                    }
                    owner[i] = t;
                }
            }

            // Pass 2: texels no centre test took, within reach of a triangle: the nearest point of the nearest one.
            var clamp = new SortedDictionary<int, (double D2, int T, double A, double B, double C)>();
            for (var t = 0; t < triangles.Count; t++)
            {
                if (shapes[t] is not { } shape) continue;
                var (p, map) = (shape.P, shape.Map);
                var (x0, x1, y0, y1) = Span(p, size, reach);
                for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    var i = y * size + x;
                    if (owner[i] >= 0) continue;
                    var q = (X: x + 0.5, Y: y + 0.5);
                    var (d2, edge, s) = Nearest(p, q);
                    if (d2 > reach2) continue;
                    // Ascending triangles are ascending faces, so a strict "nearer" leaves a tie with the lower face.
                    if (clamp.TryGetValue(i, out var best) && !(d2 < best.D2)) continue;
                    var w = new double[3];
                    w[map[edge]] = 1 - s;
                    w[map[(edge + 1) % 3]] = s;
                    clamp[i] = (d2, t, w[0], w[1], w[2]);
                }
            }

            var slot = new int[size * size];
            Array.Fill(slot, -1);
            var count = owner.Count(o => o >= 0) + clamp.Count;
            var triangle = new int[count];
            var weights = new float[3 * count];
            var clamped = new bool[count];
            var n = 0;
            for (var i = 0; i < slot.Length; i++)
            {
                double a, b, cc;
                if (owner[i] >= 0) { triangle[n] = owner[i]; (a, b, cc) = Holds(shapes[owner[i]]!.Value, i % size, i / size)!.Value; }
                // A centre exactly on a triangle's bottom or right edge (an island's outer boundary, which the top-left rule
                // gives to no one) lies on the face: it takes the clamp's point, which is the centre, but counts as a centre.
                else if (clamp.TryGetValue(i, out var k)) { triangle[n] = k.T; (a, b, cc) = (k.A, k.B, k.C); clamped[n] = k.D2 > 0; }
                else continue;
                weights[3 * n] = (float)a; weights[3 * n + 1] = (float)b; weights[3 * n + 2] = (float)cc;
                slot[i] = n++;
            }
            return new Raster { Slot = slot, Triangle = triangle, Weights = weights, Clamped = clamped, Overlaps = [.. overlaps] };
        }

        /// <summary>A baked triangle in texel space: its corners ordered positive inside, the original corner of each, and twice its area.</summary>
        private readonly record struct Shape((double X, double Y)[] P, int[] Map, double Area);

        /// <summary>The weights of corners A, B, C at texel (x, y)'s centre when the triangle holds it by the top-left rule, else null.</summary>
        private static (double A, double B, double C)? Holds(Shape shape, int x, int y)
        {
            var p = shape.P;
            var q = (X: x + 0.5, Y: y + 0.5);
            var e0 = Edge(p[1], p[2], q);
            var e1 = Edge(p[2], p[0], q);
            var e2 = Edge(p[0], p[1], q);
            if (!Inside(e0, p[1], p[2]) || !Inside(e1, p[2], p[0]) || !Inside(e2, p[0], p[1])) return null;
            var w = new double[3];
            w[shape.Map[0]] = e0 / shape.Area; w[shape.Map[1]] = e1 / shape.Area; w[shape.Map[2]] = e2 / shape.Area;
            return (w[0], w[1], w[2]);
        }

        /// <summary>
        /// The triangle's corners in texel space, ordered so the edge functions are positive inside (counter-clockwise
        /// in the image with y down reads clockwise on screen; either way one order), with <paramref name="map"/>[k] the
        /// original corner (0 A, 1 B, 2 C) of point k, and twice its area. False when the uv triangle has no area.
        /// </summary>
        private static bool Oriented(Corners c, int size, out (double X, double Y)[] p, out int[] map, out double area)
        {
            p = [(c.UvA.X * (double)size, c.UvA.Y * (double)size), (c.UvB.X * (double)size, c.UvB.Y * (double)size), (c.UvC.X * (double)size, c.UvC.Y * (double)size)];
            map = [0, 1, 2];
            area = Edge(p[0], p[1], p[2]);
            if (area < 0)
            {
                (p[1], p[2]) = (p[2], p[1]);
                map = [0, 2, 1];
                area = -area;
            }
            return area > 0 && double.IsFinite(area);
        }

        /// <summary>The texels whose centres lie within <paramref name="pad"/> of the triangle's bounding box, clipped to the image.</summary>
        private static (int X0, int X1, int Y0, int Y1) Span((double X, double Y)[] p, int size, double pad)
        {
            double minX = Math.Min(p[0].X, Math.Min(p[1].X, p[2].X)), maxX = Math.Max(p[0].X, Math.Max(p[1].X, p[2].X));
            double minY = Math.Min(p[0].Y, Math.Min(p[1].Y, p[2].Y)), maxY = Math.Max(p[0].Y, Math.Max(p[1].Y, p[2].Y));
            var x0 = (int)Math.Max(0, Math.Ceiling(minX - pad - 0.5));
            var x1 = (int)Math.Min(size - 1, Math.Floor(maxX + pad - 0.5));
            var y0 = (int)Math.Max(0, Math.Ceiling(minY - pad - 0.5));
            var y1 = (int)Math.Min(size - 1, Math.Floor(maxY + pad - 0.5));
            return (x0, x1, y0, y1);
        }

        /// <summary>
        /// The edge function of a-b at q: <c>cross(b − a, q − a)</c>, worked from the lexically lower end so the same edge
        /// walked either way gives exactly opposite values.
        /// </summary>
        private static double Edge((double X, double Y) a, (double X, double Y) b, (double X, double Y) q) =>
            a.X < b.X || (a.X == b.X && a.Y <= b.Y)
                ? (b.X - a.X) * (q.Y - a.Y) - (b.Y - a.Y) * (q.X - a.X)
                : -((a.X - b.X) * (q.Y - b.Y) - (a.Y - b.Y) * (q.X - b.X));

        /// <summary>
        /// Inside an edge a-b: strictly, or on it when it is a top or left edge. In the image (y down) with the corners
        /// ordered positive inside, a top edge runs in +x with no change of y and a left edge runs up (−y); of the two
        /// triangles either side of an edge, exactly one counts it so.
        /// </summary>
        private static bool Inside(double e, (double X, double Y) a, (double X, double Y) b) =>
            e > 0 || (e == 0 && (b.Y < a.Y || (b.Y == a.Y && b.X > a.X)));

        /// <summary>The squared distance from q to the triangle's nearest edge point, that edge (k runs from p[k] to p[k+1]) and the parameter along it; the first edge on a tie.</summary>
        private static (double D2, int Edge, double S) Nearest((double X, double Y)[] p, (double X, double Y) q)
        {
            var best = (D2: double.PositiveInfinity, Edge: 0, S: 0.0);
            for (var k = 0; k < 3; k++)
            {
                var a = p[k];
                var b = p[(k + 1) % 3];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                var len2 = dx * dx + dy * dy;
                var s = len2 > 0 ? Math.Clamp(((q.X - a.X) * dx + (q.Y - a.Y) * dy) / len2, 0, 1) : 0;
                double ex = a.X + s * dx - q.X, ey = a.Y + s * dy - q.Y;
                var d2 = ex * ex + ey * ey;
                if (d2 < best.D2) best = (d2, k, s);
            }
            return best;
        }
    }
}

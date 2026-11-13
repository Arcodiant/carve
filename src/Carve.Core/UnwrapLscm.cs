namespace Carve.Core;

/// <summary>
/// Least-squares conformal maps (Levy et al. 2002) in double, solved by Jacobi-preconditioned conjugate gradient in a
/// fixed order (T-56). Each triangle gets a local orthonormal frame in its plane; its residual
/// <c>sum_j W_j U_j / sqrt(2A)</c>, <c>W_j = z_(j+2) - z_(j+1)</c>, is zero for <c>U = z</c>, so the map keeps
/// orientation in math axes. Two boundary vertices, the farthest apart in 3D, are pinned at (0, 0) and (d, 0).
/// No trig, no <c>System.Random</c>, no parallelism: two runs are bit-identical.
/// </summary>
internal static class UnwrapLscm
{
    /// <summary>A triangle with <c>2A &lt;= DegenerateFraction * L^2</c> (L the island's 3D bounding-box diagonal) contributes nothing.</summary>
    public const double DegenerateFraction = 1e-12;

    /// <summary>The flattened island in math axes (x right, y up), one (x, y) per island vertex, and the iterations CG took.</summary>
    public sealed record Flat(double[] X, double[] Y, int Iterations);

    /// <summary>A triangle's local frame: corner 1 at (L1, 0), corner 2 at (X2, Y2), corner 0 at the origin; TwoA = L1 * Y2.</summary>
    public readonly record struct Frame(double L1, double X2, double Y2, bool Degenerate)
    {
        public double TwoA => L1 * Y2;
    }

    public static Frame[] Frames(PolygonMesh mesh, UnwrapPatch patch)
    {
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var v in patch.MeshVertex)
        {
            var p = mesh.Vertices[v];
            minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y); minZ = Math.Min(minZ, p.Z);
            maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); maxZ = Math.Max(maxZ, p.Z);
        }
        var l2 = (maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY) + (maxZ - minZ) * (maxZ - minZ);

        var frames = new Frame[patch.Triangles.Count];
        for (var t = 0; t < frames.Length; t++)
        {
            var (a, b, c) = patch.Triangles[t];
            var pa = mesh.Vertices[patch.MeshVertex[a]];
            var pb = mesh.Vertices[patch.MeshVertex[b]];
            var pc = mesh.Vertices[patch.MeshVertex[c]];
            double ex = (double)pb.X - pa.X, ey = (double)pb.Y - pa.Y, ez = (double)pb.Z - pa.Z;
            double qx = (double)pc.X - pa.X, qy = (double)pc.Y - pa.Y, qz = (double)pc.Z - pa.Z;
            var l1 = Math.Sqrt(ex * ex + ey * ey + ez * ez);
            if (l1 == 0) { frames[t] = new Frame(0, 0, 0, true); continue; }
            ex /= l1; ey /= l1; ez /= l1;
            var x2 = ex * qx + ey * qy + ez * qz;
            double cx = ey * qz - ez * qy, cy = ez * qx - ex * qz, cz = ex * qy - ey * qx;
            var y2 = Math.Sqrt(cx * cx + cy * cy + cz * cz);
            frames[t] = new Frame(l1, x2, y2, l1 * y2 <= DegenerateFraction * l2);
        }
        return frames;
    }

    /// <summary>The two pinned island vertices: the boundary pair farthest apart in 3D, ties to the lower mesh-vertex pair, then the lower island-vertex pair; the first has the lower mesh vertex.</summary>
    public static (int P0, int P1, double D) Pins(PolygonMesh mesh, UnwrapPatch patch)
    {
        var boundary = new List<int>();
        for (var i = 0; i < patch.OnBoundary.Length; i++)
            if (patch.OnBoundary[i]) boundary.Add(i);
        int best0 = -1, best1 = -1, bestLo = 0, bestHi = 0;
        var bestD2 = -1.0;
        for (var i = 0; i < boundary.Count; i++)
            for (var j = i + 1; j < boundary.Count; j++)
            {
                int a = boundary[i], b = boundary[j];
                int ma = patch.MeshVertex[a], mb = patch.MeshVertex[b];
                var pa = mesh.Vertices[ma];
                var pb = mesh.Vertices[mb];
                double dx = (double)pb.X - pa.X, dy = (double)pb.Y - pa.Y, dz = (double)pb.Z - pa.Z;
                var d2 = dx * dx + dy * dy + dz * dz;
                int lo = Math.Min(ma, mb), hi = Math.Max(ma, mb);
                if (d2 > bestD2 || (d2 == bestD2 && (lo < bestLo || (lo == bestLo && hi < bestHi))))
                {
                    bestD2 = d2;
                    bestLo = lo;
                    bestHi = hi;
                    (best0, best1) = ma <= mb ? (a, b) : (b, a);
                }
            }
        return (best0, best1, bestD2 > 0 ? Math.Sqrt(bestD2) : 0);
    }

    /// <summary>Flattens one island, or null with an error when CG does not converge.</summary>
    public static Flat? Solve(UnwrapPatch patch, Frame[] frames, int p0, int p1, double d, List<MeshError> errors)
    {
        var n = patch.MeshVertex.Count;
        // Unknowns 2i (x) and 2i+1 (y); the pins' four are fixed.
        var fixedValue = new double[2 * n];
        var free = new int[2 * n];
        var freeCount = 0;
        for (var k = 0; k < 2 * n; k++)
        {
            var vertex = k / 2;
            free[k] = vertex == p0 || vertex == p1 ? -1 : freeCount++;
        }
        fixedValue[2 * p1] = d;

        var rows = new Dictionary<int, double>[freeCount];
        for (var r = 0; r < freeCount; r++) rows[r] = [];
        var rhs = new double[freeCount];

        Span<int> index = stackalloc int[6];
        Span<double> re = stackalloc double[6];
        Span<double> im = stackalloc double[6];
        for (var t = 0; t < frames.Length; t++)
        {
            var f = frames[t];
            if (f.Degenerate) continue;
            var (a, b, c) = patch.Triangles[t];
            // z0 = 0, z1 = L1, z2 = X2 + i Y2; W_j = z_(j+2) - z_(j+1).
            double w0r = f.X2 - f.L1, w0i = f.Y2;
            double w1r = -f.X2, w1i = -f.Y2;
            double w2r = f.L1, w2i = 0;
            var s = 1 / Math.Sqrt(f.TwoA);
            int[] vs = [a, b, c];
            double[] wr = [w0r, w1r, w2r], wi = [w0i, w1i, w2i];
            for (var j = 0; j < 3; j++)
            {
                index[2 * j] = 2 * vs[j];
                index[2 * j + 1] = 2 * vs[j] + 1;
                // (wr + i wi)(u + i v) = (wr u - wi v) + i (wi u + wr v)
                re[2 * j] = wr[j] * s; re[2 * j + 1] = -wi[j] * s;
                im[2 * j] = wi[j] * s; im[2 * j + 1] = wr[j] * s;
            }
            Accumulate(index, re, free, fixedValue, rows, rhs);
            Accumulate(index, im, free, fixedValue, rows, rhs);
        }

        // CSR, columns ascending.
        var start = new int[freeCount + 1];
        for (var r = 0; r < freeCount; r++) start[r + 1] = start[r] + rows[r].Count;
        var cols = new int[start[freeCount]];
        var vals = new double[start[freeCount]];
        var diag = new double[freeCount];
        for (var r = 0; r < freeCount; r++)
        {
            var keys = new int[rows[r].Count];
            rows[r].Keys.CopyTo(keys, 0);
            Array.Sort(keys);
            for (var k = 0; k < keys.Length; k++)
            {
                cols[start[r] + k] = keys[k];
                vals[start[r] + k] = rows[r][keys[k]];
                if (keys[k] == r) diag[r] = vals[start[r] + k];
            }
        }

        var x = new double[freeCount];
        var iterations = Cg(start, cols, vals, diag, rhs, x, out var residual, out var converged);
        if (!converged)
        {
            errors.Add(new MeshError($"faces[{patch.LowestFace}]",
                $"the flattening did not converge (relative residual {residual:R} after {iterations} iterations; the limit is {Unwrap.Tolerance:R} within {Unwrap.MaxIterations}); mark seams to cut the island into smaller ones"));
            return null;
        }

        var xs = new double[n];
        var ys = new double[n];
        for (var i = 0; i < n; i++)
        {
            xs[i] = free[2 * i] < 0 ? fixedValue[2 * i] : x[free[2 * i]];
            ys[i] = free[2 * i + 1] < 0 ? fixedValue[2 * i + 1] : x[free[2 * i + 1]];
        }
        return new Flat(xs, ys, iterations);
    }

    private static void Accumulate(ReadOnlySpan<int> index, ReadOnlySpan<double> coef, int[] free, double[] fixedValue,
        Dictionary<int, double>[] rows, double[] rhs)
    {
        for (var p = 0; p < 6; p++)
        {
            var fp = free[index[p]];
            if (fp < 0) continue;
            for (var q = 0; q < 6; q++)
            {
                var fq = free[index[q]];
                var product = coef[p] * coef[q];
                if (fq >= 0) rows[fp][fq] = rows[fp].TryGetValue(fq, out var old) ? old + product : product;
                else rhs[fp] -= product * fixedValue[index[q]];
            }
        }
    }

    /// <summary>Jacobi-preconditioned CG from zero; stops at <c>||r|| &lt;= Tolerance ||b||</c> or the cap.</summary>
    private static int Cg(int[] start, int[] cols, double[] vals, double[] diag, double[] b, double[] x, out double relative, out bool converged)
    {
        var n = b.Length;
        var r = new double[n];
        var z = new double[n];
        var p = new double[n];
        var ap = new double[n];
        var bNorm = Math.Sqrt(Dot(b, b));
        Array.Copy(b, r, n);
        relative = 0;
        converged = true;
        if (bNorm == 0) return 0;
        Precondition(diag, r, z);
        Array.Copy(z, p, n);
        var rz = Dot(r, z);
        for (var it = 0; ; it++)
        {
            relative = Math.Sqrt(Dot(r, r)) / bNorm;
            if (relative <= Unwrap.Tolerance) return it;
            if (it == Unwrap.MaxIterations) { converged = false; return it; }
            for (var i = 0; i < n; i++)
            {
                var sum = 0.0;
                for (var k = start[i]; k < start[i + 1]; k++) sum += vals[k] * p[cols[k]];
                ap[i] = sum;
            }
            var pap = Dot(p, ap);
            if (!(pap > 0)) { converged = false; return it; }
            var alpha = rz / pap;
            for (var i = 0; i < n; i++)
            {
                x[i] += alpha * p[i];
                r[i] -= alpha * ap[i];
            }
            Precondition(diag, r, z);
            var rzNew = Dot(r, z);
            var beta = rzNew / rz;
            rz = rzNew;
            for (var i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
        }
    }

    private static void Precondition(double[] diag, double[] r, double[] z)
    {
        for (var i = 0; i < r.Length; i++) z[i] = diag[i] > 0 ? r[i] / diag[i] : r[i];
    }

    private static double Dot(double[] a, double[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++) sum += a[i] * b[i];
        return sum;
    }
}

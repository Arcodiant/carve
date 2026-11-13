using System.Numerics;

namespace Carve.Core;

/// <summary>
/// The vector and plane-polygon helpers the cuts share (<see cref="FaceSplit"/>, <see cref="Knife"/>,
/// <see cref="PlaneCut"/>): double-precision vectors, a face plane's 2D basis, and point-in-polygon and crossing tests on
/// loops of vertex ids projected into it.
/// </summary>
internal static class PlaneGeometry
{
    internal readonly record struct Vector2D(double X, double Y)
    {
        public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
        public static Vector2D operator *(Vector2D a, double s) => new(a.X * s, a.Y * s);
    }

    internal static Vector3D D(Vector3 v) => new(v.X, v.Y, v.Z);

    internal static double Dot(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    internal static double SegmentDistance(Vector3D p, Vector3D a, Vector3D b)
    {
        var ab = b - a;
        var len2 = Dot(ab, ab);
        var t = len2 == 0 ? 0 : Math.Clamp(Dot(p - a, ab) / len2, 0, 1);
        var d = p - (a + ab * t);
        return Math.Sqrt(Dot(d, d));
    }

    internal static (Vector3D U, Vector3D V) Basis((double X, double Y, double Z) normal)
    {
        var n = new Vector3D(normal.X, normal.Y, normal.Z);
        var helper = Math.Abs(n.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var u = Cross(helper, n);
        u *= 1 / Math.Sqrt(Dot(u, u));
        return (u, Cross(n, u));
    }

    internal static Vector3D Cross(Vector3D a, Vector3D b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    internal static double Orient(Vector2D a, Vector2D b, Vector2D c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    /// <summary>Whether segments p-q and r-s cross properly (each strictly separates the other's ends).</summary>
    internal static bool Crosses(Vector2D p, Vector2D q, Vector2D r, Vector2D s)
        => Math.Sign(Orient(p, q, r)) * Math.Sign(Orient(p, q, s)) < 0 && Math.Sign(Orient(r, s, p)) * Math.Sign(Orient(r, s, q)) < 0;

    internal static double SignedArea(IReadOnlyList<int> loop, Func<int, Vector2D> at)
    {
        double area = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            var (a, b) = (at(loop[i]), at(loop[(i + 1) % loop.Count]));
            area += a.X * b.Y - b.X * a.Y;
        }
        return area / 2;
    }

    internal static bool InPolygon(IReadOnlyList<int> loop, Func<int, Vector2D> at, Vector2D p)
    {
        var inside = false;
        for (var i = 0; i < loop.Count; i++)
        {
            var (a, b) = (at(loop[i]), at(loop[(i + 1) % loop.Count]));
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) inside = !inside;
        }
        return inside;
    }

    /// <summary>Inside the outer loop and in no hole.</summary>
    internal static bool Inside(IReadOnlyList<int>[] loops, Func<int, Vector2D> at, Vector2D p)
        => InPolygon(loops[0], at, p) && !loops.Skip(1).Any(h => InPolygon(h, at, p));

    internal static double Length(Vector3D d) => Math.Sqrt(Dot(d, d));
}

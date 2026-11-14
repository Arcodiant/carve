using System.Numerics;

namespace Carve.Core;

/// <summary>
/// UVs by projection (T-55): planar, box and cylindrical, each from a mesh, a set of live faces and a rectangle of UV space,
/// returning one UV per <see cref="Face.Corners"/> entry (hole corners included) for those faces only. Engine-free and
/// deterministic: the maths is in double and each UV is rounded to float once. glTF axes: u right, v down.
/// </summary>
public static class UvProjection
{
    /// <summary>The cos of 1 degree: within it of vertical, a view's up is -Z (as render's).</summary>
    private static readonly double VerticalCosine = Math.Cos(Math.PI / 180);

    /// <summary>The rectangle of UV space a projection fills: <c>[u0, v0, u1, v1]</c>, with u1 &gt; u0 and v1 &gt; v0.</summary>
    public readonly record struct Rect(double U0, double V0, double U1, double V1)
    {
        public static Rect Unit { get; } = new(0, 0, 1, 1);
        public double Width => U1 - U0;
        public double Height => V1 - V0;

        /// <summary>Whether all four are finite and the rectangle has area (u1 &gt; u0, v1 &gt; v0).</summary>
        public bool IsValid => double.IsFinite(U0) && double.IsFinite(V0) && double.IsFinite(U1) && double.IsFinite(V1)
            && U1 > U0 && V1 > V0 && double.IsFinite(Width) && double.IsFinite(Height);
    }

    /// <summary>The six groups of a box projection, in cell order.</summary>
    public static readonly IReadOnlyList<string> BoxAxes = ["+x", "-x", "+y", "-y", "+z", "-z"];

    /// <summary>A box projection: every face's corner UVs, and which axis each face went to.</summary>
    public sealed record BoxResult(IReadOnlyDictionary<int, Vector2[]> Uvs, IReadOnlyDictionary<string, IReadOnlyList<int>> Axes);

    /// <summary>
    /// Lays the faces out as <c>render</c> shows them from <paramref name="direction"/> (from the point looked at toward the
    /// eye, any length): u to the image's right, v down, up being +Y or -Z within 1 degree of vertical. One uniform scale
    /// fits the projected bounds into <paramref name="into"/>, keeping their aspect and centred.
    /// </summary>
    public static IReadOnlyDictionary<int, Vector2[]> Planar(PolygonMesh mesh, IReadOnlyList<int> faces, Vector3 direction, Rect into)
    {
        var (right, up) = View(direction);
        var points = faces.ToDictionary(f => f, f => mesh.Faces[f].Corners.Select(c => Project(mesh.Vertices[c], right, up)).ToArray());
        var (min, max) = Bounds(points.Values);
        var scale = Fit(max.U - min.U, max.V - min.V, into.Width, into.Height);
        return Place(points, min, max, scale, into);
    }

    /// <summary>
    /// Each face goes to the axis its normal leans on most (ties to x, then y, then z), projected as that axis's head-on view
    /// shows it. The six groups sit in a 3 x 2 grid of equal cells inside <paramref name="into"/> (+x, -x, +y; then -y, +z, -z),
    /// all at one scale, each centred in its cell.
    /// </summary>
    public static BoxResult Box(PolygonMesh mesh, IReadOnlyList<int> faces, Rect into)
    {
        var axes = BoxAxes.ToDictionary(a => a, _ => new List<int>());
        foreach (var f in faces) axes[AxisOf(mesh.FaceNormal(f))].Add(f);

        var projected = new Dictionary<string, Dictionary<int, (double U, double V)[]>>();
        var scale = double.PositiveInfinity;
        var cellW = into.Width / 3;
        var cellH = into.Height / 2;
        foreach (var name in BoxAxes)
        {
            if (axes[name].Count == 0) continue;
            var (right, up) = View(AxisDirection(name));
            var points = axes[name].ToDictionary(f => f, f => mesh.Faces[f].Corners.Select(c => Project(mesh.Vertices[c], right, up)).ToArray());
            projected[name] = points;
            var (min, max) = Bounds(points.Values);
            scale = Math.Min(scale, Fit(max.U - min.U, max.V - min.V, cellW, cellH, unbounded: true));
        }
        if (double.IsPositiveInfinity(scale)) scale = 0;

        var result = new SortedDictionary<int, Vector2[]>();
        for (var cell = 0; cell < BoxAxes.Count; cell++)
        {
            if (!projected.TryGetValue(BoxAxes[cell], out var points)) continue;
            var (min, max) = Bounds(points.Values);
            var u0 = into.U0 + (cell % 3) * cellW;
            var v0 = into.V0 + (cell / 3) * cellH;
            foreach (var (f, uv) in Place(points, min, max, scale, new Rect(u0, v0, u0 + cellW, v0 + cellH))) result[f] = uv;
        }
        return new BoxResult(result, axes.ToDictionary(a => a.Key, a => (IReadOnlyList<int>)a.Value));
    }

    /// <summary>
    /// Whether <paramref name="seam"/> lies along <paramref name="axis"/> (its part across the axis, both made unit, is
    /// shorter than 1e-6), worked in double: the one test project_uvs refuses by and <see cref="Cylindrical"/> throws by
    /// (RV-43). Both must be non-zero.
    /// </summary>
    public static bool SeamAlongAxis(Vector3 axis, Vector3 seam) => Length(Across(axis, seam)) < 1e-6;

    private static double[] Across(Vector3 axis, Vector3 seam)
    {
        var a = Normalise(axis);
        var s = Normalise(seam);
        return Sub(s, Mul(a, Dot(s, a)));
    }

    /// <summary>
    /// u is the angle round <paramref name="axis"/> from <paramref name="seam"/> (a direction across the axis), increasing to
    /// the right as seen from outside with the axis pointing up; v is the distance along the axis, downward from the faces'
    /// highest point. Arc length (at the faces' mean radius) and height share one scale, fitted into <paramref name="into"/>
    /// keeping aspect. A face crossing the seam keeps its corners together, so it may run past the right edge.
    /// <paramref name="centre"/> is a point on the axis; null takes the faces' bounds centre.
    /// </summary>
    public static IReadOnlyDictionary<int, Vector2[]> Cylindrical(PolygonMesh mesh, IReadOnlyList<int> faces, Vector3 axis, Vector3? centre, Vector3 seam, Rect into)
    {
        if (SeamAlongAxis(axis, seam)) throw new ArgumentException("seam lies along the axis", nameof(seam));
        var a = Normalise(axis);
        var e1 = Normalise(Across(axis, seam));
        var e2 = Cross(a, e1);

        var vertices = faces.SelectMany(f => mesh.Faces[f].Corners).Distinct().ToArray();
        var c = centre is { } given ? D(given) : BoundsCentre(vertices.Select(v => D(mesh.Vertices[v])));
        var radius = vertices.Select(v =>
        {
            var q = Sub(D(mesh.Vertices[v]), c);
            return Length(Sub(q, Mul(a, Dot(q, a))));
        }).Average();
        var top = vertices.Max(v => Dot(Sub(D(mesh.Vertices[v]), c), a));

        var points = new Dictionary<int, (double U, double V)[]>();
        var fitting = new List<(double U, double V)>();
        foreach (var f in faces)
        {
            var corners = mesh.Faces[f].Corners;
            var angles = new double?[corners.Count];
            for (var k = 0; k < corners.Count; k++)
            {
                var q = Sub(D(mesh.Vertices[corners[k]]), c);
                var x = Dot(q, e1);
                var y = Dot(q, e2);
                if (Math.Sqrt(x * x + y * y) < 1e-9) continue;
                var t = Math.Atan2(y, x);
                angles[k] = t < -1e-9 ? t + 2 * Math.PI : Math.Max(t, 0);
            }
            // Corners on the axis (a cone's apex) have no angle: they take the first corner that has one.
            var reference = angles.FirstOrDefault(t => t is not null) ?? 0;
            var run = new double[corners.Count];
            for (var k = 0; k < corners.Count; k++)
            {
                var t = angles[k] ?? reference;
                while (t - reference > Math.PI) t -= 2 * Math.PI;
                while (reference - t > Math.PI) t += 2 * Math.PI;
                run[k] = t;
            }
            // The face as a whole starts in [0, 2pi): one that crosses the seam runs past 2pi rather than splitting.
            var lowest = run.Min();
            var shift = Math.Floor((lowest + 1e-9) / (2 * Math.PI)) * 2 * Math.PI;
            points[f] = corners.Select((vertex, k) =>
                (U: (run[k] - shift) * radius, V: top - Dot(Sub(D(mesh.Vertices[vertex]), c), a))).ToArray();
            // The fit stops at one turn (2 pi), so a face that crosses the seam overruns the right edge.
            fitting.AddRange(points[f].Select(p => (U: Math.Min(p.U, 2 * Math.PI * radius), p.V)));
        }
        var (min, max) = Bounds([fitting.ToArray()]);
        var scale = Fit(max.U - min.U, max.V - min.V, into.Width, into.Height);
        return Place(points, min, max, scale, into);
    }

    /// <summary>The unit right and up of the image <c>render</c> draws looking from <paramref name="direction"/>.</summary>
    private static (double[] Right, double[] Up) View(Vector3 direction)
    {
        var d = Normalise(direction);
        double[] hint = Math.Abs(d[1]) > VerticalCosine ? [0.0, 0, -1] : [0.0, 1, 0];
        var right = Normalise(Cross(Mul(d, -1), hint));
        var up = Cross(right, Mul(d, -1));
        return (right, up);
    }

    private static (double U, double V) Project(Vector3 p, double[] right, double[] up)
    {
        var q = D(p);
        return (Dot(q, right), -Dot(q, up));
    }

    private static string AxisOf(Vector3 n)
    {
        var (x, y, z) = (Math.Abs((double)n.X), Math.Abs((double)n.Y), Math.Abs((double)n.Z));
        const double tie = 1e-6;
        if (x >= y - tie && x >= z - tie) return n.X >= 0 ? "+x" : "-x";
        if (y >= z - tie) return n.Y >= 0 ? "+y" : "-y";
        return n.Z >= 0 ? "+z" : "-z";
    }

    private static Vector3 AxisDirection(string name)
    {
        var sign = name[0] == '+' ? 1f : -1f;
        return name[1] switch { 'x' => new(sign, 0, 0), 'y' => new(0, sign, 0), _ => new(0, 0, sign) };
    }

    /// <summary>The scale that fits a (w x h) extent into (width x height) keeping aspect; a zero extent does not limit it.</summary>
    private static double Fit(double w, double h, double width, double height, bool unbounded = false)
    {
        var scale = double.PositiveInfinity;
        if (w > 0) scale = Math.Min(scale, width / w);
        if (h > 0) scale = Math.Min(scale, height / h);
        return double.IsPositiveInfinity(scale) ? (unbounded ? double.PositiveInfinity : 0) : scale;
    }

    private static ((double U, double V) Min, (double U, double V) Max) Bounds(IEnumerable<(double U, double V)[]> sets)
    {
        var all = sets.SelectMany(s => s).ToArray();
        return ((all.Min(p => p.U), all.Min(p => p.V)), (all.Max(p => p.U), all.Max(p => p.V)));
    }

    private static SortedDictionary<int, Vector2[]> Place(Dictionary<int, (double U, double V)[]> points, (double U, double V) min, (double U, double V) max, double scale, Rect into)
    {
        var (cu, cv) = ((min.U + max.U) / 2, (min.V + max.V) / 2);
        var (mu, mv) = ((into.U0 + into.U1) / 2, (into.V0 + into.V1) / 2);
        var result = new SortedDictionary<int, Vector2[]>();
        foreach (var (f, list) in points)
            result[f] = list.Select(p => new Vector2((float)(mu + (p.U - cu) * scale), (float)(mv + (p.V - cv) * scale))).ToArray();
        return result;
    }

    private static double[] D(Vector3 v) => [v.X, v.Y, v.Z];
    private static double[] Normalise(Vector3 v) => Normalise(D(v));
    private static double[] Normalise(double[] v) => Mul(v, 1 / Length(v));
    private static double Length(double[] v) => Math.Sqrt(Dot(v, v));
    private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    private static double[] Sub(double[] a, double[] b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    private static double[] Mul(double[] a, double k) => [a[0] * k, a[1] * k, a[2] * k];
    private static double[] Cross(double[] a, double[] b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];

    private static double[] BoundsCentre(IEnumerable<double[]> points)
    {
        var list = points.ToArray();
        return new[] { 0, 1, 2 }.Select(k => (list.Min(p => p[k]) + list.Max(p => p[k])) / 2).ToArray();
    }
}

using System.Numerics;
using Carve.Scenario.Crate;

namespace Carve.Scenario.Morph;

/// <summary>
/// The arm of M-21 (T-105): a tube along +X, shoulder at x = 0 and wrist at x = 0.6, 11 rings of 8 sides, with two targets, a rig and a clip.
/// Pure arithmetic with no product in it, so that the Core test <c>ArmGeometry</c> works every number and every pixel check
/// before the scenario is written, and the scenario draws on the same ones (a scenario links it, as the gatehouse's does its plan).
/// </summary>
internal static class ArmPlan
{
    public const int Rings = 11;
    public const int Sides = 8;
    public const double Step = 0.06;

    /// <summary>The ring radii, shoulder to wrist.</summary>
    public static readonly double[] Radii = [.050, .055, .058, .055, .048, .040, .046, .048, .042, .034, .028];

    /// <summary>Rings 1 to 3 and 6 to 8 are Bulk's, by 1.35.</summary>
    public static readonly int[] BulkRings = [1, 2, 3, 6, 7, 8];

    public const double BulkScale = 1.35;
    public const double LeanScale = 0.8;

    public const int CageVertices = Rings * Sides;
    public const int CageEdges = Rings * Sides + (Rings - 1) * Sides;
    public const int CageFaces = (Rings - 1) * Sides + 2;

    /// <summary>Bone name, parent and rest position local to the parent (the wrist at x = 0.6 in the world).</summary>
    public static readonly (string Name, string? Parent, float[] Position)[] Bones =
    [
        ("Shoulder", null, [0f, 0f, 0f]),
        ("Elbow", "Shoulder", [0.3f, 0f, 0f]),
        ("Wrist", "Elbow", [0.3f, 0f, 0f]),
    ];

    /// <summary>Elbow 90 degrees about +Z, as a quaternion rounded as the tools take it.</summary>
    public static readonly float[] ElbowQuarterTurn = [0f, 0f, 0.70711f, 0.70711f];

    public static int Id(int ring, int side) => ring * Sides + side;

    /// <summary>
    /// A cage vertex: ring <paramref name="ring"/> at x = ring x 0.06, the vertex at angle 0 in the YZ plane (y = r cos, z = r sin: counter-clockwise
    /// seen from +X), rounded to six places as the tools are given them.
    /// </summary>
    public static double[] Vertex(int ring, int side, double scale = 1)
    {
        var a = 2 * Math.PI * side / Sides;
        var r = Radii[ring] * scale;
        return [Math.Round(ring * Step, 6), Math.Round(r * Math.Cos(a), 6), Math.Round(r * Math.Sin(a), 6)];
    }

    /// <summary>The 88 vertices of the cage, ring after ring, every ring's radius times <paramref name="scale"/>.</summary>
    public static double[][] Cage(double scale = 1) =>
        [.. Enumerable.Range(0, Rings).SelectMany(i => Enumerable.Range(0, Sides).Select(k => Vertex(i, k, scale)))];

    /// <summary>The 82 faces: 80 side quads facing out, then the cap at the shoulder (facing -X) and the cap at the wrist (facing +X).</summary>
    public static int[][] Faces()
    {
        var faces = new List<int[]>();
        for (var i = 0; i < Rings - 1; i++)
            for (var k = 0; k < Sides; k++)
                faces.Add([Id(i, k), Id(i, (k + 1) % Sides), Id(i + 1, (k + 1) % Sides), Id(i + 1, k)]);
        faces.Add([.. Enumerable.Range(0, Sides).Reverse()]);
        faces.Add([.. Enumerable.Range(Id(Rings - 1, 0), Sides)]);
        return [.. faces];
    }

    /// <summary>Bulk, as the <c>positions</c> of <c>define_target</c>: the 48 vertices of rings 1 to 3 and 6 to 8 at 1.35 times their radius.</summary>
    public static Dictionary<string, double[]> Bulk()
    {
        var result = new Dictionary<string, double[]>();
        foreach (var i in BulkRings)
            for (var k = 0; k < Sides; k++) result[Id(i, k).ToString()] = Vertex(i, k, BulkScale);
        return result;
    }

    /// <summary>The vertex ids of a ring range.</summary>
    public static int[] RingIds(int from, int to) => [.. Enumerable.Range(Id(from, 0), (to - from + 1) * Sides)];

    /// <summary>The cage's weights: rings 0 to 4 Shoulder, ring 5 half and half, rings 6 to 10 Elbow.</summary>
    public static (int[] Vertices, Dictionary<string, double> Weights)[] RigWeights() =>
    [
        (RingIds(0, 4), new() { ["Shoulder"] = 1 }),
        (RingIds(5, 5), new() { ["Shoulder"] = 0.5, ["Elbow"] = 0.5 }),
        (RingIds(6, 10), new() { ["Elbow"] = 1 }),
    ];

    /// <summary>The cage's ring 2 vertices with y above 0 (angles 0, 45 and 315 degrees): <c>Bad</c> sends each to y = -0.08.</summary>
    public const double BadY = -0.08;

    public static int[] BadVertices() => [.. Enumerable.Range(0, Sides).Where(k => Vertex(2, k)[1] > 0).Select(k => Id(2, k))];

    // ---- the mesh as the pictures see it ----

    /// <summary>The live faces of a mesh as loops of vertex ids, with its positions.</summary>
    public sealed record Soup(Vector3[] V, int[][] F)
    {
        public (Vector3 Min, Vector3 Max) Bounds(IEnumerable<int>? ids = null)
        {
            var used = ids ?? F.SelectMany(f => f).Distinct();
            var points = used.Select(i => V[i]).ToArray();
            return (points.Aggregate(Vector3.Min), points.Aggregate(Vector3.Max));
        }

        public Soup With(Vector3[] positions) => new(positions, F);
    }

    /// <summary>The positions at the weights: <c>base + sum (weight x (target - base))</c>, each target given as absolute positions of the vertices it moves.</summary>
    public static Vector3[] Morph(Vector3[] rest, IEnumerable<(Dictionary<int, Vector3> Target, float Weight)> targets)
    {
        var result = (Vector3[])rest.Clone();
        foreach (var (target, weight) in targets)
            foreach (var (id, at) in target) result[id] += weight * (at - rest[id]);
        return result;
    }

    /// <summary>The Elbow turned <paramref name="degrees"/> about +Z at the elbow joint (0.3, 0, 0), blended by each vertex's Elbow weight (the Shoulder holds the rest).</summary>
    public static Vector3[] Curl(Vector3[] positions, float[] elbowWeight, float degrees)
    {
        var turn = Matrix4x4.CreateRotationZ(degrees * MathF.PI / 180);
        var joint = new Vector3(0.3f, 0, 0);
        var result = new Vector3[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            var turned = joint + Vector3.Transform(positions[i] - joint, turn);
            result[i] = Vector3.Lerp(positions[i], turned, elbowWeight[i]);
        }
        return result;
    }

    /// <summary>The lowest and highest y the surface reaches on the plane x = <paramref name="x"/> (the silhouette from +Z at that x).</summary>
    public static (float Low, float High) Silhouette(Soup soup, float x)
    {
        var (low, high) = (float.MaxValue, float.MinValue);
        foreach (var f in soup.F)
            for (var k = 0; k < f.Length; k++)
            {
                var (a, b) = (soup.V[f[k]], soup.V[f[(k + 1) % f.Length]]);
                if ((a.X - x) * (b.X - x) > 0 || a.X == b.X && a.X != x) continue;
                var t = a.X == b.X ? 0 : (x - a.X) / (b.X - a.X);
                var y = a.Y + t * (b.Y - a.Y);
                (low, high) = (MathF.Min(low, y), MathF.Max(high, y));
                if (a.X == b.X) (low, high) = (MathF.Min(low, MathF.Min(a.Y, b.Y)), MathF.Max(high, MathF.Max(a.Y, b.Y)));
            }
        return (low, high);
    }

    // ---- cameras: where a point lands and which ray a pixel looks along ----

    /// <summary>A camera worked from the rest bounds: a point to pixels, and a pixel (given at its centre) to a ray.</summary>
    public sealed record Cam(Func<Vector3, Vector2> Project, Func<float, float, (Vector3 Origin, Vector3 Direction)> Ray, int Size)
    {
        public (int X, int Y) Pixel(Vector3 p) => CameraPixels.Holding(Project(p));
    }

    private const float Margin = 0.08f;

    /// <summary>
    /// <c>render</c>'s orthographic view along +Z (up +Y, +X to the right) of the box <paramref name="min"/> to <paramref name="max"/>: the drawn space
    /// puts the box's centre at the origin and its half-diagonal at 1, and the frame holds the larger half-extent in x or y with 8 % margin each side.
    /// With <paramref name="mirror"/> the camera is the one from -Z, so that +X runs to the left.
    /// </summary>
    public static Cam Orthographic(Vector3 min, Vector3 max, int size, bool mirror = false)
    {
        var centre = (min + max) / 2;
        var radius = (max - min).Length() / 2;
        var half = MathF.Max((max.X - min.X) / 2, (max.Y - min.Y) / 2) / radius / (1 - 2 * Margin);
        var flip = mirror ? -1f : 1f;
        return new Cam(
            p =>
            {
                var d = (p - centre) / radius;
                return new Vector2((flip * d.X / half + 1) / 2 * size, (1 - d.Y / half) / 2 * size);
            },
            (x, y) =>
            {
                var d = new Vector2(flip * (x / size * 2 - 1) * half, (1 - y / size * 2) * half);
                return (new Vector3(centre.X + d.X * radius, centre.Y + d.Y * radius, 10), -Vector3.UnitZ);
            },
            size);
    }

    /// <summary>The perspective view from <paramref name="direction"/> (the corner 'front' is (1.6, 1.2, 2.2)), by the Crate scenario's camera.</summary>
    public static Cam Perspective(Vector3 min, Vector3 max, Vector3 direction, int size)
    {
        var project = CameraPixels.Perspective(min, max, direction, size);
        var unproject = CameraPixels.Unproject(min, max, direction, size);
        return new Cam(project, unproject, size);
    }

    public static readonly Vector3 FrontDirection = new(1.6f, 1.2f, 2.2f);

    // ---- the ray test ----

    /// <summary>Does the ray meet any face (each fanned into triangles, both sides).</summary>
    public static bool Hits(Soup soup, Vector3 origin, Vector3 direction)
    {
        foreach (var f in soup.F)
            for (var k = 1; k + 1 < f.Length; k++)
                if (Triangle(origin, direction, soup.V[f[0]], soup.V[f[k]], soup.V[f[k + 1]])) return true;
        return false;
    }

    private static bool Triangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
    {
        var (e1, e2) = (b - a, c - a);
        var p = Vector3.Cross(d, e2);
        var det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-12f) return false;
        var s = o - a;
        var u = Vector3.Dot(s, p) / det;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, e1);
        var v = Vector3.Dot(d, q) / det;
        if (v < 0 || u + v > 1) return false;
        return Vector3.Dot(e2, q) / det > 0;
    }

    /// <summary>
    /// What pixel (x, y) shows by the ray test: true when the rays through its centre and its four neighbours (<paramref name="reach"/> pixels away) all meet the mesh,
    /// false when none does, null when they disagree (on an edge or a silhouette).
    /// </summary>
    public static bool? Seen(Soup soup, Cam cam, int x, int y, float reach = 1f)
    {
        var hits = 0;
        foreach (var (dx, dy) in new[] { (0f, 0f), (reach, 0f), (-reach, 0f), (0f, reach), (0f, -reach) })
        {
            var (o, d) = cam.Ray(x + 0.5f + dx, y + 0.5f + dy);
            if (Hits(soup, o, d)) hits++;
        }
        return hits == 5 ? true : hits == 0 ? false : null;
    }

    // ---- the pixels a frame is asked about ----

    /// <summary>How far from a probe's pixel centre the ray test looks (in pixels) before it calls the pixel certain.</summary>
    public const float ProbeReach = 0.5f;

    /// <summary>The margin of a silhouette probe in metres (about 1.4 pixels).</summary>
    public const float ProbeMargin = 0.002f;

    /// <summary>A pixel and whether the mesh covers it (by the ray test), with what it was chosen for.</summary>
    public sealed record Probe(int X, int Y, bool Drawn, string What);

    /// <summary>The x of a silhouette probe: each ring but the two end ones and each middle between rings (19 in all; the caps' rims are steep and left to the sweep).</summary>
    public static float[] ProbeXs() =>
        [.. Enumerable.Range(1, 2 * (Rings - 1) - 1).Select(i => (float)Math.Round(i * Step / 2, 6))];

    /// <summary>
    /// The silhouette seen from +Z, probed at each <see cref="ProbeXs"/>: a point <paramref name="margin"/> inside the surface's top and bottom must be
    /// drawn, one <paramref name="margin"/> outside background. The half-height is worked from the mesh by slicing it at that x.
    /// </summary>
    public static List<Probe> SilhouetteProbes(Soup soup, Cam cam, float margin)
    {
        var probes = new List<Probe>();
        foreach (var x in ProbeXs())
        {
            var (low, high) = Silhouette(soup, x);
            foreach (var (y, drawn, what) in new[]
            {
                (high - margin, true, "inside the top"), (high + margin, false, "outside the top"),
                (low + margin, true, "inside the bottom"), (low - margin, false, "outside the bottom"),
            })
            {
                var (px, py) = cam.Pixel(new Vector3(x, y, 0));
                probes.Add(new Probe(px, py, drawn, $"x {x:0.###}, y {y:0.####}, {what} (half-height {(what.Contains("top") ? high : -low):0.#####})"));
            }
        }
        return probes;
    }

    /// <summary>A grid of pixels (every <paramref name="stride"/>th, over the mesh's box and a margin) whose coverage the ray test is sure of.</summary>
    public static List<Probe> Sweep(Soup soup, Cam cam, int stride)
    {
        var (min, max) = soup.Bounds();
        var corners = new[] { min, max, new(min.X, max.Y, max.Z), new(max.X, min.Y, min.Z), new(min.X, min.Y, max.Z), new(max.X, max.Y, min.Z) }.Select(cam.Pixel).ToArray();
        var (x0, x1) = (Math.Max(4, corners.Min(c => c.X) - 12), Math.Min(cam.Size - 4, corners.Max(c => c.X) + 12));
        var (y0, y1) = (Math.Max(4, corners.Min(c => c.Y) - 12), Math.Min(cam.Size - 4, corners.Max(c => c.Y) + 12));
        var probes = new List<Probe>();
        for (var y = y0; y < y1; y += stride)
            for (var x = x0; x < x1; x += stride)
                if (Seen(soup, cam, x, y) is { } drawn) probes.Add(new Probe(x, y, drawn, $"pixel ({x}, {y}) by the ray test"));
        return probes;
    }

    /// <summary>
    /// Frame 6's guards, by hand: with the Elbow at 90 degrees about +Z the forearm stands on the elbow (0.3, 0, 0) and runs up +Y, so the points
    /// above the elbow are drawn and the points where the forearm lay at rest are background, and the other way about at rest.
    /// </summary>
    public static List<Probe> CurlProbes(Cam cam, bool curled)
    {
        var probes = new List<Probe>();
        void Add(float x, float y, bool drawnWhenCurled, bool drawnAtRest, string what)
        {
            var (px, py) = cam.Pixel(new Vector3(x, y, 0));
            probes.Add(new Probe(px, py, curled ? drawnWhenCurled : drawnAtRest, $"({x}, {y}), {what}"));
        }

        Add(0.30f, 0.24f, true, false, "above the elbow, where a curled forearm is");
        Add(0.30f, 0.12f, true, false, "above the elbow, the forearm's lower half");
        Add(0.45f, 0.00f, false, true, "where the forearm lies at rest");
        Add(0.38f, 0.24f, false, false, "beside the curled forearm");
        Add(0.12f, 0.00f, true, true, "the upper arm");
        Add(0.30f, -0.09f, false, false, "below the elbow");
        return probes;
    }

    /// <summary>The probes the ray test is sure of at the probe's own pixel (<see cref="ProbeReach"/>): the silhouette next to a spike is too steep for a margin of a pixel and a half.</summary>
    public static List<Probe> Sure(Soup soup, Cam cam, IEnumerable<Probe> probes) =>
        [.. probes.Where(p => Seen(soup, cam, p.X, p.Y, ProbeReach) == p.Drawn)];

    /// <summary>
    /// The planted fault seen from +Z: ring 2's three upper vertices sent to y = -0.08 make a spike below the arm at x = 0.12, wide enough
    /// 9 mm below the rest silhouette (-0.053) to catch a pixel: drawn with <c>Bad</c> at 1, background without it.
    /// </summary>
    public static List<Probe> BadProbes(Cam cam, bool bad)
    {
        var (px, py) = cam.Pixel(new Vector3(0.12f, -0.062f, 0));
        return [new Probe(px, py, bad, "(0.12, -0.062), 9 mm under the rest silhouette, in the spike Bad makes")];
    }
}

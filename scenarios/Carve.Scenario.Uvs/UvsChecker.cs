using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Crate;
using static Carve.Scenario.Crate.CameraPixels;

namespace Carve.Scenario.Uvs;

/// <summary>A camera as <c>render</c> is asked for it: a direction, and a zoom about a target (the mesh looked at), over the bounds of every mesh.</summary>
internal sealed record Cam(string View, Vector3 Direction, int Size, float Zoom, Vector3 Target, int Left = 0, int Top = 0, int CaptionWidth = 520, bool Labelled = false)
{
    public object Entry => new { view = View, zoom = (double)Zoom, target = new[] { (double)Target.X, Target.Y, Target.Z } };
}

internal sealed record Sample(string Mesh, int Face, int Column, int Row, bool Mapped, (int X, int Y) Pixel);

/// <summary>
/// The checker look of any mesh, worked from the geometry and the UVs <c>get_mesh</c> gives (T-57's cell rule, T-61's meshes): points
/// spread over each face's triangles, kept only where the face looks at the eye, a ray test finds it in sight, the point is 4 px clear
/// of every edge (and of a label or caption) and well inside a cell (a fifth of the cell clear of its edge, the cell the same under both
/// diagonals of a quad); then the pixel is magenta where the face has no UVs, white at cell (0,0), else its column's hue.
/// </summary>
internal static class UvsChecker
{
    public static readonly List<string> Log = [];
    private const int Grid = 10, EdgeClear = 4;

    public static Func<Vector3, Vector2> Camera(Scene scene, Cam cam) => Perspective(scene.Min, scene.Max, cam.Direction, cam.Size, cam.Zoom, cam.Target);

    /// <summary>The largest zoom in 8 down to 1 at which every vertex of <paramref name="mesh"/> lies 8% in from the frame's edge.</summary>
    public static float Fit(Scene scene, SceneMesh mesh, Vector3 direction, int size)
    {
        var target = (mesh.BoundsMin + mesh.BoundsMax) / 2;
        foreach (var zoom in new[] { 8f, 7f, 6f, 5f, 4f, 3.5f, 3f, 2.5f, 2f, 1.5f, 1f })
        {
            var pixel = Perspective(scene.Min, scene.Max, direction, size, zoom, target);
            if (mesh.Vertices.All(v => { var p = pixel(v); return p.X >= size * 0.08f && p.X <= size * 0.92f && p.Y >= size * 0.08f && p.Y <= size * 0.92f; })) return zoom;
        }

        return 1;
    }

    public static List<Sample> Sweep(ScenarioDriver driver, ScenarioFrame frame, Scene scene, SceneMesh mesh, Cam cam, string what)
    {
        var pixel = Camera(scene, cam);
        var eye = Eye(scene.Min, scene.Max, cam.Direction, cam.Target);
        var edges = scene.Meshes.SelectMany(m => m.Faces.SelectMany(l => l.Select((v, i) => (pixel(m.Vertices[v]), pixel(m.Vertices[l[(i + 1) % l.Length]]))))).ToArray();
        var labels = cam.Labelled ? Enumerable.Range(0, mesh.Faces.Length).Where(f => Vector3.Dot(mesh.Normal(f), eye - mesh.Centre(f)) > 0).Select(f => pixel(mesh.Centre(f))).ToArray() : [];
        var samples = new List<Sample>();
        for (var f = 0; f < mesh.Faces.Length; f++)
        {
            var loop = mesh.Faces[f];
            var normal = mesh.Normal(f);
            if (normal.LengthSquared() < 1e-12f || Vector3.Dot(normal, eye - mesh.Vertices[loop[0]]) <= 0) continue;
            mesh.Uvs.TryGetValue(f, out var uv);
            for (var t = 1; t + 1 < loop.Length; t++)
            {
                Vector3 a = mesh.Vertices[loop[0]], b = mesh.Vertices[loop[t]], c = mesh.Vertices[loop[t + 1]];
                for (var i = 1; i < Grid; i++)
                for (var j = 1; i + j < Grid; j++)
                {
                    float w1 = i / (float)Grid, w2 = j / (float)Grid, w0 = 1 - w1 - w2;
                    var p = a * w0 + b * w1 + c * w2;
                    var (column, row) = (0, 0);
                    if (uv is not null)
                    {
                        var at = uv[0] * w0 + uv[t] * w1 + uv[t + 1] * w2;
                        if (AlternativeUv(mesh, loop, uv, p) is not { } other || Vector2.Distance(at, other) > 0.006f) continue;
                        var (fu, fv) = (at.X * 8 - MathF.Floor(at.X * 8), at.Y * 8 - MathF.Floor(at.Y * 8));
                        if (fu is < 0.2f or > 0.8f || fv is < 0.2f or > 0.8f) continue;
                        (column, row) = ((int)MathF.Floor(at.X * 8) & 7, (int)MathF.Floor(at.Y * 8) & 7);
                    }

                    var centre = pixel(p);
                    var (x, y) = Holding(centre);
                    if (x < 4 || y < 4 || x >= cam.Size - 4 || y >= cam.Size - 4 || (x < cam.CaptionWidth && y < 28)) continue;
                    if (labels.Any(l => MathF.Abs(l.X - centre.X) < 16 && MathF.Abs(l.Y - centre.Y) < 10)) continue;
                    if (edges.Any(e => Geo.DistanceToSegment(centre, e.Item1, e.Item2) < EdgeClear)) continue;
                    if (!Geo.InSight(scene, mesh, f, eye, p)) continue;
                    var sample = new Sample(mesh.Name, f, column, row, uv is not null, (x + cam.Left, y + cam.Top));
                    samples.Add(sample);
                    Check(driver, frame, what, sample);
                }
            }
        }

        if (samples.Count < 12) driver.Fail($"{frame.Name}: {what}: only {samples.Count} samples are in clear sight");
        return samples;
    }

    /// <summary>The UV of a point under the other fan of the face (from corner 1): equal to the first where the corners' UVs are an affine image of the face; null off the face.</summary>
    private static Vector2? AlternativeUv(SceneMesh mesh, int[] loop, Vector2[] uv, Vector3 p)
    {
        var n = loop.Length;
        for (var t = 1; t + 1 < n; t++)
        {
            int i0 = 1 % n, i1 = (t + 1) % n, i2 = (t + 2) % n;
            var (w0, w1, w2) = Geo.Barycentric(p, mesh.Vertices[loop[i0]], mesh.Vertices[loop[i1]], mesh.Vertices[loop[i2]]);
            if (w0 >= -1e-5f && w1 >= -1e-5f && w2 >= -1e-5f) return uv[i0] * w0 + uv[i1] * w1 + uv[i2] * w2;
        }

        return null;
    }

    private static void Check(ScenarioDriver driver, ScenarioFrame frame, string what, Sample s)
    {
        var p = frame.Pixel(s.Pixel.X, s.Pixel.Y);
        var (ok, expected, column) = CheckerColours.Judge(p.R, p.G, p.B, s.Mapped, s.Column, s.Row);
        if (!ok || Log.Count < 6000) Log.Add($"{frame.Name}\t{what}\t{s.Mesh} face {s.Face}\tcell ({s.Column}, {s.Row})\tat {s.Pixel}\t{p}\texpected {expected}\t{(ok ? "ok" : "FAIL")}");
        if (!ok) driver.Fail($"{frame.Name}: {what}: {s.Mesh} face {s.Face} cell ({s.Column}, {s.Row}) at {s.Pixel} is {p}, expected {expected}; nearest column {column}");
    }

    /// <summary>
    /// What a frame must show: the cell (0,0) wherever a face laid there looks at the eye, at least <paramref name="columns"/> different
    /// columns, <paramref name="magenta"/> unmapped samples, and where <paramref name="pairs"/> a light cell brighter than the dark one below it on one face.
    /// </summary>
    public static void Need(ScenarioDriver driver, ScenarioFrame frame, string what, Scene scene, SceneMesh mesh, Cam cam, List<Sample> samples, int columns, bool pairs, int magenta = 0)
    {
        var eye = Eye(scene.Min, scene.Max, cam.Direction, cam.Target);
        var origin = new Vector2(1 / 16f, 1 / 16f);
        var owners = mesh.Uvs.Where(e => UvLayoutPixels.Inside(e.Value, origin)).Select(e => e.Key).Where(f => Vector3.Dot(Vector3.Normalize(mesh.Normal(f)), Vector3.Normalize(eye - mesh.Centre(f))) > 0.3f).ToList();
        if (owners.Count > 0 && !samples.Any(s => s.Mapped && (s.Column, s.Row) == (0, 0)))
        {
            driver.Fail($"{frame.Name}: {what}: face {owners[0]} holds uv (0,0) and faces the eye (cosine over 0.3), but no sample of cell (0,0) is in clear sight");
        }

        var distinct = samples.Where(s => s.Mapped).Select(s => s.Column).Distinct().Count();
        if (distinct < columns) driver.Fail($"{frame.Name}: {what}: only {distinct} of the {columns} columns asked are sampled");
        if (samples.Count(s => !s.Mapped) < magenta) driver.Fail($"{frame.Name}: {what}: {samples.Count(s => !s.Mapped)} magenta samples, expected at least {magenta}");
        if (!pairs) return;
        var found = 0;
        foreach (var light in samples.Where(s => s.Mapped && (s.Column + s.Row) % 2 == 0 && (s.Column, s.Row) != (0, 0)))
        {
            if (samples.FirstOrDefault(d => d.Mapped && d.Face == light.Face && d.Column == light.Column && d.Row == light.Row + 1) is not { } dark) continue;
            found++;
            var (lp, dp) = (frame.Pixel(light.Pixel.X, light.Pixel.Y), frame.Pixel(dark.Pixel.X, dark.Pixel.Y));
            if (CheckerColours.Value(lp.R, lp.G, lp.B) <= CheckerColours.Value(dp.R, dp.G, dp.B))
            {
                driver.Fail($"{frame.Name}: {what}: face {light.Face}'s light cell ({light.Column}, {light.Row}) is {lp}, no brighter than the dark cell below it, {dp}");
            }
        }

        if (found == 0) driver.Fail($"{frame.Name}: {what}: no light cell above a dark one on one face is in clear sight");
    }

    /// <summary>The highlight, seen through the plain frame's own samples: the highlighted face's pixels are tinted orange (red kept, blue cut), every other pixel unchanged.</summary>
    public static void Tint(ScenarioDriver driver, ScenarioFrame plain, ScenarioFrame lit, List<Sample> samples, int face)
    {
        int tinted = 0, same = 0;
        foreach (var s in samples)
        {
            var (a, b) = (plain.Pixel(s.Pixel.X, s.Pixel.Y), lit.Pixel(s.Pixel.X, s.Pixel.Y));
            if (s.Face != face)
            {
                if (a.R != b.R || a.G != b.G || a.B != b.B) driver.Fail($"{lit.Name}: face {s.Face} at {s.Pixel} is {b}, but {a} without the highlight");
                same++;
                continue;
            }

            tinted++;
            if (!(b.R >= a.R * 0.8 - 5 && b.B <= a.B * 0.6 + 6 && b.B < b.R && b.G < b.R + 3 && b.G > b.B)) driver.Fail($"{lit.Name}: highlighted face {face} at {s.Pixel} is {b}; plain {a}: expected red kept, blue cut, an orange tint");
        }

        if (tinted < 8 || same < 8) driver.Fail($"{lit.Name}: only {tinted} samples on the highlighted face and {same} off it");
        Log.Add($"{lit.Name}\thighlight face {face}\t{tinted} tinted\t{same} unchanged");
    }
}

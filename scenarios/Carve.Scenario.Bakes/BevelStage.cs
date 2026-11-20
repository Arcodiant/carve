using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Orcward;
using Carve.Scenario.Uvs;
using StbImageWriteSharp;
using static Carve.Scenario.Bakes.BakesChecks;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Bakes;

/// <summary>
/// T-125: the bevel normal map. <c>bake</c> <c>normal</c> with <c>bevel.width</c> 0.0016 at 1024 (5 texels at the wrench's 3.1
/// texels per mm), padding 8, and add it to the material (<c>wrench-bevel</c>: stage 1's base colour and the map as
/// <c>normal_texture</c>). Frames: the <c>uvs</c> look with the map under it; zoom 8 on a convex jaw edge from three directions, each
/// without and then with the map; the wrench from the six sides and the three-quarter view. Checks: texels farther than the width from
/// every break edge of their face hold N itself, (128, 128, 255) or a level from it; in the close frames the strip by the edge lies
/// between the two faces' shades; <c>render_glb</c> of the export with the map agrees with <c>render</c> under D-6's rule.
/// <c>CARVE_BAKES_BREAK</c>: <c>bevel-thin</c> bakes width 0.00001; <c>bevel-g</c> puts the map on with G inverted.
/// </summary>
internal sealed class BevelStage : IBakesStage
{
    private const int Size = 1024;

    /// <summary>fps-arms' 1.6 mm bevel.</summary>
    internal const double Width = 0.0016;

    private const double ThinWidth = 0.00001;

    /// <summary>The spec's own rule: a texel with no edge within reach is (128, 128, 255) or one level from it.</summary>
    private const int FlatLevels = 1;

    /// <summary>
    /// How far, in luma, a strip may lie outside the two faces' shades. PENDING THE PERSON'S RULING (T-125's hand-off): 0 is the
    /// spec's "between" as written; the probe's numbers are in <c>bevel.tsv</c>.
    /// </summary>
    private const double BetweenSlack = 0;

    private static readonly Vector3 ThreeQuarter = new(1, 0.6f, 1.5f);

    public async Task RunAsync(ScenarioDriver driver)
    {
        var here = Path.GetDirectoryName(typeof(BevelStage).Assembly.Location)!;
        var hard = HardEdges(Path.Combine(here, "wrench.carve"));
        var scene = await Scene.ReadAsync(driver, "wrench");
        var mesh = scene["wrench"];
        var groups = mesh.Raw.GetProperty("groups").EnumerateArray()
            .ToDictionary(g => g.GetProperty("name").GetString()!, g => g.GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()).ToArray());

        var width = Break == "bevel-thin" ? ThinWidth : Width;
        var textures = Path.Combine(driver.RunFolder, "textures");
        var bake = await CallJsonAsync(driver, "bake", new { mesh = "wrench", folder = textures, size = Size, padding = 8, maps = new[] { "normal" }, bevel = new { width } });
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "bevel-bake.json"), bake.ToString());
        if (bake.GetProperty("skipped_faces").GetArrayLength() != 0 || bake.GetProperty("warnings").GetArrayLength() != 0) Note($"bevel bake: expected no skipped faces and no warnings, got {bake}");
        var normal = bake.GetProperty("files")[0].GetProperty("path").GetString()!;
        var png = Texels.Read(normal);
        var shown = Break == "bevel-g" ? InvertGreen(png, Path.Combine(textures, "wrench.normal.g-inverted.png")) : normal;

        var tsv = new List<string> { "check\twhat\tvalue\tdetail" };
        CheckFlat(mesh, hard, png, tsv);

        // Close on a convex jaw edge, from three directions, without the map (stage 1's material) and then with it.
        var edge = JawEdge(mesh, hard, groups["dark"].ToHashSet());
        var close = Directions(edge).Select((d, k) => new View($"bevel-jaw-{k + 1}", d, Size, 8, edge.Mid, Mirror: false)).ToArray();
        var plain = new Dictionary<string, ScenarioFrame>();
        foreach (var view in close) plain[view.Name] = await driver.CaptureAsync(view.Name + "-plain", view.Args);

        await CallJsonAsync(driver, "define_material", new { name = "wrench-bevel", base_colour_texture = Path.Combine(textures, "wrench.base_colour.png"), normal_texture = shown });
        await CallJsonAsync(driver, "set_material", new { material = "wrench-bevel", group = "all" });
        foreach (var view in close)
        {
            var frame = await driver.CaptureAsync(view.Name, view.Args);
            CheckStrip(view, "plain", plain[view.Name], scene, mesh, edge, tsv, judge: false);
            CheckStrip(view, "normal", frame, scene, mesh, edge, tsv, judge: true);
        }

        await driver.CaptureAsync("wrench-uvs-normal", new { mode = "uvs", image = normal, size = Size });

        var sides = new View[]
        {
            new("wrench-bevel-front", Vector3.UnitZ, Size), new("wrench-bevel-back", -Vector3.UnitZ, Size), new("wrench-bevel-left", -Vector3.UnitX, Size),
            new("wrench-bevel-right", Vector3.UnitX, Size), new("wrench-bevel-top", Vector3.UnitY, Size), new("wrench-bevel-bottom", -Vector3.UnitY, Size),
            new("wrench-bevel-three-quarter", ThreeQuarter, Size),
        };
        var frames = new Dictionary<string, ScenarioFrame>();
        foreach (var view in sides) frames[view.Name] = await driver.CaptureAsync(view.Name, view.Args);

        await CompareExportAsync(driver, frames, sides, tsv);
        await File.WriteAllLinesAsync(Path.Combine(driver.RunFolder, "bevel.tsv"), tsv);
    }

    /// <summary>The marked hard edges of <c>samples/wrench.carve</c>, which get_mesh does not report: on a smooth mesh these are the breaks.</summary>
    private static HashSet<(int, int)> HardEdges(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("meshes")[0].GetProperty("hard_edges").EnumerateArray()
            .Select(e => (Math.Min(e[0].GetInt32(), e[1].GetInt32()), Math.Max(e[0].GetInt32(), e[1].GetInt32()))).ToHashSet();
    }

    private static IEnumerable<(int A, int B)> Loop(int[] face) => face.Select((v, i) => (Math.Min(v, face[(i + 1) % face.Length]), Math.Max(v, face[(i + 1) % face.Length])));

    private static float ToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Math.Clamp(Vector3.Dot(p - a, ab) / Math.Max(ab.LengthSquared(), 1e-20f), 0, 1);
        return Vector3.Distance(p, a + ab * t);
    }

    /// <summary>
    /// Texels farther than the width from every break edge of their face hold N: at each face's centre and halfway from it to each
    /// corner, taken in 3D and in uv alike, where the point is at least twice the width from the face's hard edges (the margin covers a
    /// face whose uvs are not an affine image of it).
    /// </summary>
    private static void CheckFlat(SceneMesh mesh, HashSet<(int, int)> hard, Texels png, List<string> tsv)
    {
        var checkedCount = 0;
        for (var f = 0; f < mesh.Faces.Length; f++)
        {
            if (!mesh.Uvs.TryGetValue(f, out var uv)) continue;
            var loop = mesh.Faces[f];
            var edges = Loop(loop).Where(hard.Contains).ToArray();
            var c3 = mesh.Centre(f);
            var c2 = uv.Aggregate(Vector2.Zero, (s, u) => s + u) / uv.Length;
            var points = new List<(Vector3 P, Vector2 Uv)> { (c3, c2) };
            for (var i = 0; i < loop.Length; i++) points.Add(((c3 + mesh.Vertices[loop[i]]) / 2, (c2 + uv[i]) / 2));
            foreach (var (p, u) in points)
            {
                if (edges.Any(e => ToSegment(p, mesh.Vertices[e.Item1], mesh.Vertices[e.Item2]) < 2 * Width)) continue;
                var (x, y) = ((int)MathF.Floor(u.X * Size), (int)MathF.Floor(u.Y * Size));
                if (x < 0 || y < 0 || x >= Size || y >= Size) continue;
                var at = (y * png.Width + x) * 4;
                var (r, g, b) = (png.Rgba[at], png.Rgba[at + 1], png.Rgba[at + 2]);
                checkedCount++;
                if (Math.Abs(r - 128) > FlatLevels || Math.Abs(g - 128) > FlatLevels || b < 255 - FlatLevels)
                    Note($"bevel: face {f}'s texel ({x}, {y}), clear of its hard edges, holds ({r}, {g}, {b}), not N (128, 128, 255) within {FlatLevels}");
            }
        }
        tsv.Add($"flat\ttexels\t{checkedCount}\tclear of every hard edge of their face by 2 x width");
        if (checkedCount < 50) Note($"bevel: only {checkedCount} texels clear of the hard edges were checked for N");
    }

    internal sealed record Edge(int Lo, int Hi, int FaceA, int FaceB, Vector3 Mid, Vector3 Axis, Vector3 NormalA, Vector3 NormalB, float Length);

    /// <summary>The longest convex hard edge between two jaw (<c>dark</c>) faces bending at least 60 degrees, the lower ids on a tie.</summary>
    private static Edge JawEdge(SceneMesh mesh, HashSet<(int, int)> hard, HashSet<int> jaw)
    {
        var faces = new Dictionary<(int, int), List<int>>();
        for (var f = 0; f < mesh.Faces.Length; f++)
            foreach (var e in Loop(mesh.Faces[f])) (faces.TryGetValue(e, out var l) ? l : faces[e] = []).Add(f);
        Edge? best = null;
        foreach (var ((lo, hi), around) in faces.OrderBy(k => k.Key))
        {
            if (around.Count != 2 || !hard.Contains((lo, hi)) || !jaw.Contains(around[0]) || !jaw.Contains(around[1])) continue;
            var (a, b) = (around.Min(), around.Max());
            var (na, nb) = (Vector3.Normalize(mesh.Normal(a)), Vector3.Normalize(mesh.Normal(b)));
            var mid = (mesh.Vertices[lo] + mesh.Vertices[hi]) / 2;
            if (Vector3.Dot(na, nb) > 0.5f || Vector3.Dot(na, mesh.Centre(b) - mid) >= 0) continue;
            var length = Vector3.Distance(mesh.Vertices[lo], mesh.Vertices[hi]);
            if (best is null || length > best.Length + 1e-7f)
                best = new Edge(lo, hi, a, b, mid, Vector3.Normalize(mesh.Vertices[hi] - mesh.Vertices[lo]), na, nb, length);
        }
        return best ?? throw new InvalidOperationException("the wrench's jaw has no convex hard edge");
    }

    /// <summary>The edge seen from its mean normal and leaning toward each face, all a little along the edge so it is not seen end-on or flat.</summary>
    private static Vector3[] Directions(Edge e) =>
    [
        Vector3.Normalize(e.NormalA + e.NormalB + 0.35f * e.Axis),
        Vector3.Normalize(2 * e.NormalA + e.NormalB + 0.35f * e.Axis),
        Vector3.Normalize(e.NormalA + 2 * e.NormalB + 0.35f * e.Axis),
    ];

    /// <summary>
    /// In a close frame: the mean luma of each face at three widths from the edge (its shade) and of the strip on each side at a
    /// quarter width, over the middle of the edge, each point in sight. With the map, each strip lies between the faces' shades.
    /// </summary>
    private static void CheckStrip(View view, string map, ScenarioFrame frame, Scene scene, SceneMesh mesh, Edge e, List<string> tsv, bool judge)
    {
        var project = CameraPixels.Perspective(scene.Min, scene.Max, view.Direction, view.Size, view.Zoom, view.Target);
        var eye = CameraPixels.Eye(scene.Min, scene.Max, view.Direction, view.Target);
        double? Shade(int face, Vector3 normal, float s)
        {
            var into = Vector3.Normalize(Vector3.Cross(normal, e.Axis));
            if (Vector3.Dot(into, mesh.Centre(face) - e.Mid) < 0) into = -into;
            var lumas = new List<double>();
            foreach (var t in new[] { -0.15f, -0.05f, 0.05f, 0.15f })
            {
                var p = e.Mid + e.Axis * (t * e.Length) + into * s;
                if (!Geo.Inside(mesh.Vertices, mesh.Faces[face], mesh.Normal(face), p) || !Geo.InSight(scene, mesh, face, eye, p)) continue;
                var (x, y) = CameraPixels.Holding(project(p));
                if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height) continue;
                var c = frame.Pixel(x, y);
                lumas.Add(Luma((c.R, c.G, c.B)));
            }
            return lumas.Count > 0 ? lumas.Average() : null;
        }

        var faceA = Shade(e.FaceA, e.NormalA, (float)(3 * Width));
        var faceB = Shade(e.FaceB, e.NormalB, (float)(3 * Width));
        var stripA = Shade(e.FaceA, e.NormalA, (float)(0.25 * Width));
        var stripB = Shade(e.FaceB, e.NormalB, (float)(0.25 * Width));
        string F(double? v) => v is { } d ? d.ToString("F2", CultureInfo.InvariantCulture) : "-";
        tsv.Add($"strip\t{view.Name} {map}\t{F(stripA)} {F(stripB)}\tfaces {e.FaceA} {F(faceA)}, {e.FaceB} {F(faceB)}; edge ({e.Lo}, {e.Hi})");
        if (!judge) return;
        if (faceA is not { } a || faceB is not { } b || stripA is not { } sa || stripB is not { } sb)
        {
            Note($"bevel: {view.Name}: the edge ({e.Lo}, {e.Hi})'s faces or strips are not in sight");
            return;
        }
        var (lo, hi) = (Math.Min(a, b) - BetweenSlack, Math.Max(a, b) + BetweenSlack);
        foreach (var (side, s) in new[] { ("A", sa), ("B", sb) })
            if (s < lo || s > hi) Note(string.Create(CultureInfo.InvariantCulture, $"bevel: {view.Name}: strip {side} reads {s:F2}, not between the faces' shades {a:F2} and {b:F2}"));
    }

    /// <summary>The bevel break's map: the PNG with its green inverted, written beside it.</summary>
    private static string InvertGreen(Texels png, string path)
    {
        var bytes = (byte[])png.Rgba.Clone();
        for (var i = 1; i < bytes.Length; i += 4) bytes[i] = (byte)(255 - bytes[i]);
        using var stream = File.Create(path);
        new ImageWriter().WritePng(bytes, png.Width, png.Height, ColorComponents.RedGreenBlueAlpha, stream);
        return path;
    }

    /// <summary>The wrench with the map exported, and <c>render_glb</c> against <c>render</c> front and three-quarter under D-6's rule.</summary>
    private static async Task CompareExportAsync(ScenarioDriver driver, Dictionary<string, ScenarioFrame> frames, View[] views, List<string> tsv)
    {
        var models = Path.Combine(driver.RunFolder, "game", "Content", "models");
        Directory.CreateDirectory(models);
        var glb = Path.Combine(models, "wrench-bevel.glb");
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "export-bevel-result.txt"), await CallTextAsync(driver, "export", new { path = glb }));
        Directory.CreateDirectory(Path.Combine(driver.RunFolder, "compare"));
        foreach (var name in new[] { "wrench-bevel-front", "wrench-bevel-three-quarter" })
        {
            var d = views.Single(v => v.Name == name).Direction;
            var frame = await driver.CaptureToolAsync("render_glb", name + "-glb", new { path = glb, direction = new[] { d.X, d.Y, d.Z }, size = Size });
            var row = Comparison.Measure(name, "good", frames[name], frame);
            Comparison.WriteTriptych(Path.Combine(driver.RunFolder, "compare", $"good-{name}.png"), frames[name], frame);
            tsv.Add($"glb\t{name}\t{row.Differing}\tof {row.Total}, patch {row.LargestPatch}, fails {row.Fails}");
            if (row.Fails) Note($"bevel: {name}: render_glb differs from render with the map, {row.Differing} pixels, patch {row.LargestPatch}");
        }
    }
}

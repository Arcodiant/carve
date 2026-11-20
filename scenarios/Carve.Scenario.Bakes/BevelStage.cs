using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Crate;
using Carve.Scenario.Uvs;
using StbImageWriteSharp;
using static Carve.Scenario.Bakes.BakesChecks;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Bakes;

/// <summary>
/// T-125: the bevel normal map. <c>bake</c> <c>normal</c> with <c>bevel.width</c> 0.0016 at 1024 (5 texels at the wrench's 3.1
/// texels per mm), padding 8, and add it to the material (<c>wrench-bevel</c>: stage 1's base colour and the map as
/// <c>normal_texture</c>). Frames: the <c>uvs</c> look with the map under it; zoom 8 on a convex grip (<c>steel</c>) edge from three
/// directions, each without and then with the map (the person's ruling, 2026-11-20: the lighter steel spreads the shades wider than
/// the dark jaw, where the map and its break read within 2 luma of each other); the wrench from the six sides and the three-quarter
/// view. Checks: texels farther than the width from every break edge of their face hold N itself, (128, 128, 255) or a level from it; in the close frames each strip by the edge
/// reads within 2 luma of the colour rule's shade for the mean normal (the person's ruling, 2026-11-20); <c>render_glb</c> of the export with the map agrees with <c>render</c> under D-6's rule, with the control that must fail it.
/// <c>CARVE_BAKES_BREAK</c>: <c>bevel-g</c> puts the map on with G inverted; <c>bevel-wide</c> bakes it 20 times as wide, which the flat-texel check catches. The thin-width break is the Core seam test's
/// (<c>WrenchBevelSeamTests</c>), so the stage has none (the person's ruling, 2026-11-20).
/// </summary>
internal sealed class BevelStage : IBakesStage
{
    private const int Size = 1024;

    /// <summary>fps-arms' 1.6 mm bevel.</summary>
    internal const double Width = 0.0016;

    /// <summary>The spec's own rule: a texel with no edge within reach is (128, 128, 255) or one level from it.</summary>
    private const int FlatLevels = 1;

    /// <summary>
    /// How far, in luma, a strip may lie from the mean normal's shade as the colour rule predicts it, in every close view: the
    /// person's ruling (2026-11-20), ColourRule's own slack. Probed in <c>bevel.tsv</c>'s <c>predicted</c> rows: the good map at
    /// most 1.00 off, <c>bevel-g</c> at least 5.21 off.
    /// </summary>
    private const double PredictedSlack = 2;

    /// <summary>The grip's (<c>steel</c>) colour, <see cref="SurfaceStage"/>'s.</summary>
    private const string Steel = SurfaceStage.SteelColour;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var hard = await HardEdgesAsync(driver);
        var scene = await Scene.ReadAsync(driver, "wrench");
        var mesh = scene["wrench"];
        var groups = GroupsOf(mesh.Raw);

        var textures = Path.Combine(driver.RunFolder, "textures");
        // bevel-wide: the map baked 20 times as wide, so it rounds texels the checks hold flat; CheckFlat's break.
        var width = Break == "bevel-wide" ? 20 * Width : Width;
        var bake = await CallJsonAsync(driver, "bake", new { mesh = "wrench", folder = textures, size = Size, padding = 8, maps = new[] { "normal" }, bevel = new { width } });
        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "bevel-bake.json"), bake.ToString());
        if (bake.GetProperty("skipped_faces").GetArrayLength() != 0 || bake.GetProperty("warnings").GetArrayLength() != 0) Note($"bevel bake: expected no skipped faces and no warnings, got {bake}");
        var normal = bake.GetProperty("files")[0].GetProperty("path").GetString()!;
        var png = Texels.Read(normal);
        var shown = Break == "bevel-g" ? InvertGreen(png, Path.Combine(textures, "wrench.normal.g-inverted.png")) : normal;

        var tsv = new List<string> { "check\twhat\tvalue\tdetail" };
        CheckFlat(mesh, hard, png, tsv);

        // Close on a convex grip edge, from three directions, without the map (stage 1's material) and then with it. A face is steel
        // when no later group of SurfaceStage's table (dark, red, black) takes it.
        var steel = groups["steel"].Except(groups["dark"]).Except(groups["red"]).Except(groups["black"]).ToHashSet();
        var edge = CloseEdge(scene, mesh, hard, steel);
        var close = Directions(edge).Select((d, k) => new View($"bevel-grip-{k + 1}", d, Size, 8, edge.Mid, Mirror: false)).ToArray();
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

        // T-121's block rule, with blend 1 read per channel, as for the occlusion frame (the person's ruling, RV-74).
        var uvs = await driver.CaptureAsync("wrench-uvs-normal", new { mode = "uvs", image = normal, size = Size });
        CheckUvImage("wrench-uvs-normal", uvs, mesh, png, blend: 1);

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

    /// <summary>
    /// The wrench's marked hard edges, as the server holds them: on a smooth mesh these are the breaks. get_mesh does not report
    /// them, and set_shading answers with them, so the wrench is set to the smooth shading it already has (the mesh is unchanged).
    /// </summary>
    private static async Task<HashSet<(int, int)>> HardEdgesAsync(ScenarioDriver driver)
    {
        var answer = await CallJsonAsync(driver, "set_shading", new { mesh = "wrench", shading = "smooth" });
        return answer.GetProperty("hard_edges").EnumerateArray()
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

    /// <summary>
    /// The longest convex hard edge between two of the given faces bending at least 60 degrees, the lower ids on a tie, whose
    /// sample points (<see cref="Samples"/>) are all on their face, facing the eye and in sight from each of the three close
    /// directions (the rule T-125's agent 2 made for the jaw, whose longest edge, (112, 113), the ring hides from every direction).
    /// </summary>
    private static Edge CloseEdge(Scene scene, SceneMesh mesh, HashSet<(int, int)> hard, HashSet<int> group)
    {
        var candidates = new List<Edge>();
        var faces = new Dictionary<(int, int), List<int>>();
        for (var f = 0; f < mesh.Faces.Length; f++)
            foreach (var e in Loop(mesh.Faces[f])) (faces.TryGetValue(e, out var l) ? l : faces[e] = []).Add(f);
        foreach (var ((lo, hi), around) in faces.OrderBy(k => k.Key))
        {
            if (around.Count != 2 || !hard.Contains((lo, hi)) || !group.Contains(around[0]) || !group.Contains(around[1])) continue;
            var (a, b) = (around.Min(), around.Max());
            var (na, nb) = (Vector3.Normalize(mesh.Normal(a)), Vector3.Normalize(mesh.Normal(b)));
            var mid = (mesh.Vertices[lo] + mesh.Vertices[hi]) / 2;
            if (Vector3.Dot(na, nb) > 0.5f || Vector3.Dot(na, mesh.Centre(b) - mid) >= 0) continue;
            var length = Vector3.Distance(mesh.Vertices[lo], mesh.Vertices[hi]);
            candidates.Add(new Edge(lo, hi, a, b, mid, Vector3.Normalize(mesh.Vertices[hi] - mesh.Vertices[lo]), na, nb, length));
        }
        // A stable sort: longest first, the lower ids (the dictionary's order) on a tie.
        foreach (var edge in candidates.OrderByDescending(c => MathF.Round(c.Length, 6)))
        {
            var seen = Directions(edge).All(d =>
            {
                var eye = CameraPixels.Eye(scene.Min, scene.Max, d, edge.Mid);
                return Samples(mesh, edge).All(q => Geo.Inside(mesh.Vertices, mesh.Faces[q.Face], mesh.Normal(q.Face), q.P)
                    && Vector3.Dot(mesh.Normal(q.Face), eye - q.P) > 0 && Geo.InSight(scene, mesh, q.Face, eye, q.P));
            });
            if (seen) return edge;
        }
        throw new InvalidOperationException($"none of the wrench's {candidates.Count} convex hard edges of the group is in sight from all three close directions");
    }

    /// <summary>
    /// The points a close frame reads, at -0.15, -0.05, 0.05 and 0.15 of the edge's length from its middle: on each face at a
    /// quarter width from the edge (its strip) and at its shade distance (<see cref="ShadeDistance"/>).
    /// </summary>
    private static IEnumerable<(int Face, bool Strip, Vector3 P)> Samples(SceneMesh mesh, Edge e)
    {
        foreach (var (face, normal) in new[] { (e.FaceA, e.NormalA), (e.FaceB, e.NormalB) })
        {
            var into = Into(mesh, e, face, normal);
            foreach (var strip in new[] { false, true })
                foreach (var t in new[] { -0.15f, -0.05f, 0.05f, 0.15f })
                    yield return (face, strip, e.Mid + e.Axis * (t * e.Length) + into * (float)(strip ? 0.25 * Width : ShadeDistance(mesh, e, face, normal)));
        }
    }

    private static Vector3 Into(SceneMesh mesh, Edge e, int face, Vector3 normal)
    {
        var into = Vector3.Normalize(Vector3.Cross(normal, e.Axis));
        return Vector3.Dot(into, mesh.Centre(face) - e.Mid) < 0 ? -into : into;
    }

    /// <summary>
    /// Where a face's own shade is read: three widths from the edge, or half the face's depth from it when the face is shallower
    /// than six widths, so the point is not in the strip of the face's far edge (the chamfer, face 66, is 5.5 mm deep).
    /// </summary>
    private static double ShadeDistance(SceneMesh mesh, Edge e, int face, Vector3 normal)
    {
        var into = Into(mesh, e, face, normal);
        var depth = mesh.Faces[face].Max(v => Vector3.Dot(mesh.Vertices[v] - e.Mid, into));
        return Math.Min(3 * Width, depth / 2);
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
    /// quarter width, over the middle of the edge, each point in sight. With the map, each strip reads within PredictedSlack of the
    /// mean normal's predicted shade.
    /// </summary>
    private static void CheckStrip(View view, string map, ScenarioFrame frame, Scene scene, SceneMesh mesh, Edge e, List<string> tsv, bool judge)
    {
        var project = CameraPixels.Perspective(scene.Min, scene.Max, view.Direction, view.Size, view.Zoom, view.Target);
        var misses = new List<string>();
        double? Shade(int face, bool strip)
        {
            var lumas = new List<double>();
            foreach (var (_, _, p) in Samples(mesh, e).Where(q => q.Face == face && q.Strip == strip))
            {
                var (x, y) = CameraPixels.Holding(project(p));
                if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height) { misses.Add($"{face} off frame"); continue; }
                var c = frame.Pixel(x, y);
                lumas.Add(Luma((c.R, c.G, c.B)));
            }
            return lumas.Count > 0 ? lumas.Average() : null;
        }

        // CloseEdge chose the edge so every point is on its face and in sight from this direction.
        var faceA = Shade(e.FaceA, strip: false);
        var faceB = Shade(e.FaceB, strip: false);
        var stripA = Shade(e.FaceA, strip: true);
        var stripB = Shade(e.FaceB, strip: true);
        string F(double? v) => v is { } d ? d.ToString("F2", CultureInfo.InvariantCulture) : "-";
        tsv.Add($"strip\t{view.Name} {map}\t{F(stripA)} {F(stripB)}\tfaces {e.FaceA} {F(faceA)}, {e.FaceB} {F(faceB)}; edge ({e.Lo}, {e.Hi}){(misses.Count > 0 ? "; missed " + string.Join(", ", misses) : "")}");
        if (!judge) return;
        if (faceA is not { } a || faceB is not { } b || stripA is not { } sa || stripB is not { } sb)
        {
            Note($"bevel: {view.Name}: the edge ({e.Lo}, {e.Hi})'s faces or strips are not in sight");
            return;
        }
        // The person's ruling (2026-11-20): every strip in every view is held to the colour rule's shade for the mean normal,
        // within PredictedSlack; the "between the two faces' shades" reading is dropped (it failed the good map on grip-3, where
        // the rule puts the mean exactly on face B's shade).
        double Predicted(Vector3 n)
        {
            var lit = ColourRule.Lit(Steel, n, view.Direction);
            return Luma((ColourRule.Encode(lit[0]), ColourRule.Encode(lit[1]), ColourRule.Encode(lit[2])));
        }
        var (pa, pb, pm) = (Predicted(e.NormalA), Predicted(e.NormalB), Predicted(Vector3.Normalize(e.NormalA + e.NormalB)));
        tsv.Add(string.Create(CultureInfo.InvariantCulture,
            $"predicted	{view.Name}	{pm:F2}	faces {pa:F2} {pb:F2} (measured {a:F2} {b:F2}); strips less predicted {sa - pm:F2} {sb - pm:F2}"));
        foreach (var (side, s) in new[] { ("A", sa), ("B", sb) })
        {
            if (Math.Abs(s - pm) > PredictedSlack)
                Note(string.Create(CultureInfo.InvariantCulture, $"bevel: {view.Name}: strip {side} reads {s:F2}, not within {PredictedSlack} of the mean normal's predicted shade {pm:F2} (faces predicted {pa:F2} and {pb:F2})"));
        }
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

    /// <summary>
    /// The wrench with the map exported, and <c>render_glb</c> against <c>render</c> front and three-quarter under D-6's rule, with the
    /// control that must fail it, as <see cref="BakesChecks.CompareExportAsync"/> has: the three-quarter <c>render_glb</c> against the front.
    /// </summary>
    private static async Task CompareExportAsync(ScenarioDriver driver, Dictionary<string, ScenarioFrame> frames, View[] views, List<string> tsv)
    {
        var (glbFrames, rows) = await RenderExportAsync(driver, frames, views, "wrench-bevel.glb", "export-bevel-result.txt", ["wrench-bevel-front", "wrench-bevel-three-quarter"]);
        foreach (var row in rows)
        {
            var name = row.Name;
            tsv.Add($"glb\t{name}\t{row.Differing}\tof {row.Total}, patch {row.LargestPatch}, fails {row.Fails}");
            if (row.Fails) Note($"bevel: {name}: render_glb differs from render with the map, {row.Differing} pixels, patch {row.LargestPatch}");
        }

        var control = Carve.Scenario.Orcward.Comparison.Measure("bevel-three-quarter-glb-against-front", "control", frames["wrench-bevel-front"], glbFrames["wrench-bevel-three-quarter"]);
        tsv.Add($"glb-control\t{control.Name}\t{control.Differing}\tof {control.Total}, patch {control.LargestPatch}, fails {control.Fails}");
        if (!control.Fails) Note($"the control {control.Name} passes the frame rule, so the rule cannot tell two views apart");
    }
}

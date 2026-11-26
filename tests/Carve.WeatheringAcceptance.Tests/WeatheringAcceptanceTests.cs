using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Carve.Core;
using Carve.Graphs;
using Carve.LoadChecks;
using Empyrean;
using Xunit;
using CarveModel = Carve.Core.Model;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.WeatheringAcceptance.Tests;

/// <summary>
/// T-168's checks on one round folder (<c>CARVE_WEATHERING_RUN</c>): the transcript (<c>transcript.jsonl</c>, stream-json), the saved
/// <c>wrench-weathered.carve</c>, the bake of its steel material's base colour slot (made here through <see cref="GraphSlotBake"/>), and the exported
/// <c>game/Content/models/wrench.glb</c>. The render against render_glb step is the Acceptance scenario's. Numbers go to <c>acceptance/numbers.tsv</c>.
/// </summary>
[Trait("Tier", "regression")]
public class WeatheringAcceptanceTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_WEATHERING_RUN");

    /// <summary>The model every round must report (the person's ruling of 2026-11-24).</summary>
    public const string Model = "claude-opus-5-5";

    /// <summary>
    /// The person's margins (ruling of 2026-11-24, on round 1's 93.8 and 22.3): texels within 2 of a steel hard edge are brighter than steel texels
    /// more than 12 from any by at least 30 luma, and the drive crevice is darker than the open steel by at least 10. The sets they were ruled on:
    /// both leave out drive faces 100 to 105 from the edge sets. The crevice set was redefined by the ruling of 2026-11-25 (CreviceReach): the drive's four visible side
    /// faces' texels within 0.004 m of the 8 edges of the drive's two ends (DriveSides), all texels of that set (the distance was chosen with round 2's number in view).
    /// </summary>
    public const double HardEdgeMargin = 30, CreviceMargin = 10;

    /// <summary>The grip may be no more spread (std of luma over its texels) than the starter's grip, plus float noise.</summary>
    private const double GripSlack = 0.01;

    private const int NearEdge = 2, FarEdge = 12;

    /// <summary>
    /// The junction (ruling of 2026-11-25), from samples/wrench.carve: the square drive is a separate box (vertices 168 to 175; side faces 100 to 103, end faces
    /// 104 at z -0.042 and 105 at z -0.020) standing between two 12-gon caps of the dark socket body, 118 (z -0.0425, the socket top) and 99 (z -0.020, the
    /// underside of the hub ring). The junction is the eight edges of the drive's two end rectangles; the sets are the texels of the drive's faces and of
    /// faces 118 and 99 whose 3D distance to those edges is within a reach. The rows for the other reaches, and for the socket, are notes that assert nothing; only CreviceReach with the drive's side faces' texels is checked. Caveat: faces 118 and 99 are
    /// group dark and do not wear the steel material in the file, so their texels in the steel slot's bake are not what is drawn there.
    /// </summary>
    private static readonly int[] SocketFaces = [118, 99];
    /// <summary>The crevice set's reach (ruled 2026-11-25), in metres on the mesh.</summary>
    public const double CreviceReach = 0.004;
    private static readonly double[] JunctionReaches = [0.004, 0.012, 0.035];
    private static readonly int[] DriveFaces = [100, 101, 102, 103, 104, 105];

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_WEATHERING_RUN is unset");

    private static CarveFileResult Read(string name)
    {
        var path = Path.Combine(RunFolder, name);
        var read = CarveFile.Read(File.ReadAllText(path), RunFolder);
        return read.Model is null ? throw new InvalidOperationException($"{path}: {string.Join("; ", read.Errors)}") : read;
    }

    // ---- the transcript ----

    private sealed record Call(int Index, string Name, JsonElement Input, string Id);

    private static (string? Model, List<Call> Calls, List<(int Index, string Text)> Results, HashSet<string> Failed) Transcript()
    {
        string? model = null;
        List<Call> calls = [];
        List<(int, string)> results = [];
        HashSet<string> failed = [];
        var i = 0;
        foreach (var line in File.ReadLines(Path.Combine(RunFolder, "transcript.jsonl")).Where(l => l.Trim().Length > 0))
        {
            i++;
            var root = JsonDocument.Parse(line).RootElement;
            var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "system" && root.TryGetProperty("model", out var m)) model ??= m.GetString();
            if (type is not ("assistant" or "user") || !root.TryGetProperty("message", out var msg) || !msg.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var block in content.EnumerateArray())
            {
                var bt = block.TryGetProperty("type", out var b) ? b.GetString() : null;
                if (bt == "tool_use") calls.Add(new Call(i, block.GetProperty("name").GetString()!, block.GetProperty("input"), block.GetProperty("id").GetString()!));
                else if (bt == "tool_result")
                {
                    results.Add((i, block.GetProperty("content").ToString()));
                    if (block.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True) failed.Add(block.GetProperty("tool_use_id").GetString()!);
                }
            }
        }

        return (model, calls, results, failed);
    }

    [SkippableOrRun]
    public void The_session_ran_on_the_pinned_model_with_no_file_tool()
    {
        var (model, calls, _, failed) = Transcript();
        Assert.Equal(Model, model);
        Assert.NotEmpty(calls);
        // Ruled 2026-11-24: only a call outside the two servers that succeeded fails the round; a refused attempt passes, counted and listed.
        var outside = calls.Where(c => !c.Name.StartsWith("mcp__carve__") && !c.Name.StartsWith("mcp__patina__")).ToArray();
        Note($"refused-attempts	{outside.Count(c => failed.Contains(c.Id))}	{string.Join(", ", outside.Where(c => failed.Contains(c.Id)).Select(c => $"{c.Name} at line {c.Index}"))}");
        Assert.All(outside, c => Assert.True(failed.Contains(c.Id), $"a tool that is neither carve's nor patina's succeeded: {c.Name} at line {c.Index}"));
    }

    [SkippableOrRun]
    public void The_missing_graph_message_came_and_the_next_steel_material_names_a_graph_that_exists()
    {
        var (_, calls, results, _) = Transcript();
        var message = results.FirstOrDefault(r => r.Text.Contains("no file at", StringComparison.Ordinal) && r.Text.Contains(".texgraph files in", StringComparison.Ordinal));
        Assert.True(message.Text is not null, "no tool result carried the missing-graph message (T-166: no file at ...; the .texgraph files in ...)");
        var next = calls.FirstOrDefault(c => c.Index > message.Index && c.Name == "mcp__carve__define_material"
            && c.Input.TryGetProperty("name", out var n) && n.GetString() == "steel");
        Assert.True(next is not null, "no define_material for steel after the message");
        var graphs = new[] { "base_colour_texture", "metallic_roughness_texture", "normal_texture", "occlusion_texture", "emissive_texture" }
            .Where(s => next!.Input.TryGetProperty(s, out var v) && v.ValueKind == JsonValueKind.Object && v.TryGetProperty("graph", out _))
            .Select(s => next!.Input.GetProperty(s).GetProperty("graph").GetString()!).ToArray();
        Assert.NotEmpty(graphs);
        Assert.All(graphs, g => Assert.True(File.Exists(Path.GetFullPath(g, RunFolder)), $"define_material names {g}, which is not in the round folder"));
    }

    // ---- the saved file and its bake ----

    private static (CarveModel Model, string Mesh, PolygonMesh Polygons, MaterialGraph Slot, int[] Steel, int[] Rubber) Saved()
    {
        var read = Read("wrench-weathered.carve");
        Assert.Equal(10, read.Version);
        Assert.Empty(read.Warnings);
        var model = read.Model!;
        var named = model.Meshes.Single();
        var style = named.Mesh.Style ?? throw new InvalidOperationException("the saved wrench has no groups or materials");
        var steel = style.FacesOf("steel")?.ToArray() ?? throw new InvalidOperationException("no group steel");
        var rubber = style.FacesOf("rubber")?.ToArray() ?? throw new InvalidOperationException("no group rubber");
        var slots = steel.Select(f => style.MaterialOf(f) is { } m ? model.FindMaterial(m)?.BaseColourTexture as MaterialGraph : null).ToArray();
        Assert.All(slots, s => Assert.NotNull(s));
        var slot = slots[0]! with { Path = Path.GetFullPath(slots[0]!.Path, RunFolder) };
        Assert.All(slots, s => Assert.Equal(slots[0]!.Path, s!.Path));
        Assert.True(GraphSlotBake.Read(slot.Path).ReadsMesh, "the steel's base colour graph does not read the mesh");
        return (model, named.Name, named.Mesh, slot, steel, rubber);
    }

    [SkippableOrRun]
    public void The_saved_file_is_version_10_and_its_steel_wears_a_mesh_reading_base_colour_graph() => Saved();

    private static double Luma(byte[] rgba, int n, int x, int y)
    {
        var i = (y * n + x) * 4;
        return 0.2126 * rgba[i] + 0.7152 * rgba[i + 1] + 0.0722 * rgba[i + 2];
    }

    /// <summary>The texels of a size-n bake whose centres lie in the UVs of the faces, with their distance (in texels) to the nearest of the segments.</summary>
    private static List<(int X, int Y, double Distance)> Texels(PolygonMesh mesh, IEnumerable<int> faces, int n, List<(Vector2 A, Vector2 B)> segments)
    {
        var found = new List<(int, int, double)>();
        foreach (var f in faces)
        {
            if (mesh.Uvs?.Of(f) is not { } uv) continue;
            for (var y = Math.Max(0, (int)(uv.Min(p => p.Y) * n)); y <= Math.Min(n - 1, (int)(uv.Max(p => p.Y) * n)); y++)
            for (var x = Math.Max(0, (int)(uv.Min(p => p.X) * n)); x <= Math.Min(n - 1, (int)(uv.Max(p => p.X) * n)); x++)
            {
                var c = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                if (!Inside(c, uv)) continue;
                found.Add((x, y, segments.Count == 0 ? double.MaxValue : segments.Min(s => Distance(c, s.A, s.B) * n)));
            }
        }

        return found;
    }

    private static bool Inside(Vector2 p, IReadOnlyList<Vector2> poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        return inside;
    }

    private static double Distance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-18f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Every UV segment of a hard edge on the given faces.</summary>
    private static List<(Vector2, Vector2)> HardSegments(PolygonMesh mesh, IEnumerable<int> faces)
    {
        var hard = mesh.HardEdges.ToHashSet();
        var segments = new List<(Vector2, Vector2)>();
        foreach (var f in faces)
        {
            if (mesh.Uvs?.Of(f) is not { } uv) continue;
            var loop = mesh.Faces[f].Loop;
            for (var k = 0; k < loop.Count; k++)
            {
                var (a, b) = (loop[k], loop[(k + 1) % loop.Count]);
                if (hard.Contains((Math.Min(a, b), Math.Max(a, b)))) segments.Add((uv[k], uv[(k + 1) % loop.Count]));
            }
        }

        return segments;
    }

    private static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

    private static void Note(string line)
    {
        Directory.CreateDirectory(Path.Combine(RunFolder, "acceptance"));
        File.AppendAllText(Path.Combine(RunFolder, "acceptance", "numbers.tsv"), line + "\n");
    }

    /// <summary>
    /// The drive's side faces, worked out from the geometry and not from a face number: of the drive box's six faces (DriveFaces), the four whose normal is
    /// perpendicular to the drive's axis (z, the axis the two end faces lie across). The other two are the ends, which sit flush against the socket's faces,
    /// are never seen and are dark under any occlusion; the person ruled (2026-11-25) that the crevice set is the sides' texels only.
    /// </summary>
    internal static int[] DriveSides(PolygonMesh mesh) => DriveFaces.Where(f =>
    {
        var p = mesh.Faces[f].Loop.Select(i => mesh.Vertices[i]).ToArray();
        return Math.Abs(Vector3.Normalize(Vector3.Cross(p[1] - p[0], p[2] - p[0])).Z) < 0.5f;
    }).ToArray();

    /// <summary>
    /// The texels of the drive's faces and of the socket's two faces, each with its face, its 3D distance to the 8 edges of the drive's two end rectangles,
    /// whether it lies within NearEdge of a hard edge, and its luma in the bake.
    /// </summary>
    internal static List<(int Face, double Metres, bool NearHard, double Luma, double Scale)> JunctionTexels(PolygonMesh mesh, int n, byte[] rgba)
    {
        var sides = DriveSides(mesh);
        var all = DriveFaces.Concat(SocketFaces).ToArray();
        var hard = HardSegments(mesh, all);
        var junction = new List<(Vector3 A, Vector3 B)>();
        foreach (var f in DriveFaces.Except(sides))
        {
            var loop = mesh.Faces[f].Loop;
            for (var k = 0; k < loop.Count; k++) junction.Add((mesh.Vertices[loop[k]], mesh.Vertices[loop[(k + 1) % loop.Count]]));
        }

        var texels = new List<(int, double, bool, double, double)>();
        var scale = 0.0;
        foreach (var f in all)
        {
            var uv = mesh.Uvs!.Of(f)!;
            var p = mesh.Faces[f].Loop.Select(i => mesh.Vertices[i]).ToArray();
            var (e1, e2) = (uv[1] - uv[0], uv[2] - uv[0]);
            var det = e1.X * e2.Y - e1.Y * e2.X;
            if (f == sides[0]) scale = (p[1] - p[0]).Length() / (e1.Length() * n);
            foreach (var t in Texels(mesh, [f], n, hard))
            {
                var d = new Vector2((t.X + 0.5f) / n, (t.Y + 0.5f) / n) - uv[0];
                var (a, b) = ((d.X * e2.Y - d.Y * e2.X) / det, (e1.X * d.Y - e1.Y * d.X) / det);
                var q = p[0] + (p[1] - p[0]) * a + (p[2] - p[0]) * b;
                texels.Add((f, junction.Min(s => Dist3(q, s.A, s.B)), t.Distance <= NearEdge, Luma(rgba, n, t.X, t.Y), 0));
            }
        }

        return texels.Select(t => (t.Item1, t.Item2, t.Item3, t.Item4, scale)).ToList();
    }

    /// <summary>The crevice set: the drive's side faces' texels within CreviceReach of the end edges, all of them.</summary>
    internal static List<(int Face, double Luma)> CreviceTexels(PolygonMesh mesh, int n, byte[] rgba)
    {
        var sides = DriveSides(mesh);
        return JunctionTexels(mesh, n, rgba).Where(t => sides.Contains(t.Face) && t.Metres <= CreviceReach).Select(t => (t.Face, t.Luma)).ToList();
    }

    /// <summary>Notes, per reach, the junction sets' mean luma against the open steel's, with and without the texels within NearEdge of a hard edge. The set "sides" is the checked one.</summary>
    private static double[] NoteJunction(PolygonMesh mesh, int n, byte[] rgba, double openMean, int openCount)
    {
        var sides = DriveSides(mesh);
        var texels = JunctionTexels(mesh, n, rgba);
        var scale = texels[0].Scale;
        var crevice = Array.Empty<double>();
        foreach (var reach in JunctionReaches)
        foreach (var withHard in new[] { true, false })
        foreach (var set in new[] { "sides", "drive (sides and ends)", "drive+socket" })
        {
            var inSet = texels.Where(t => t.Metres <= reach && (withHard || !t.NearHard)
                && (set == "drive+socket" || DriveFaces.Contains(t.Face)) && (set != "sides" || sides.Contains(t.Face))).ToArray();
            var mean = inSet.Select(t => t.Luma).DefaultIfEmpty().Average();
            if (reach == CreviceReach && set == "sides" && withHard) crevice = inSet.Select(t => t.Luma).ToArray();
            Note($"junction	{set}, reach {reach} m ({reach / scale:F1} texels at {n}), {(withHard ? "with" : "without")} texels within {NearEdge} of a hard edge: {inSet.Length} texels ({inSet.Count(t => sides.Contains(t.Face))} side, {inSet.Count(t => DriveFaces.Contains(t.Face) && !sides.Contains(t.Face))} end, {inSet.Count(t => !DriveFaces.Contains(t.Face))} socket), mean {F(mean)}; open steel: {openCount}, mean {F(openMean)}	{F(openMean - mean)}");
        }

        return crevice;
    }

    private static double Dist3(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        return Vector3.Distance(p, a + ab * Math.Clamp(Vector3.Dot(p - a, ab) / ab.LengthSquared(), 0, 1));
    }

    [SkippableOrRun]
    public void Hard_edge_texels_wear_brighter_and_the_drive_crevice_darker_than_the_open_steel()
    {
        var (model, mesh, polygons, slot, steel, _) = Saved();
        var n = slot.Size;
        var rgba = GraphSlotBake.Rgba(slot, GraphSlotBake.BaseColour, model, mesh, TestContext.Current.CancellationToken);
        var body = steel.Except(DriveFaces).ToArray();
        var segments = HardSegments(polygons, steel);
        var bodyTexels = Texels(polygons, body, n, segments);
        var near = bodyTexels.Where(t => t.Distance <= NearEdge).Select(t => Luma(rgba, n, t.X, t.Y)).ToArray();
        var open = bodyTexels.Where(t => t.Distance > FarEdge).Select(t => Luma(rgba, n, t.X, t.Y)).ToArray();
        Note($"hard-edge\twithin {NearEdge} texels: {near.Length} texels, mean {F(near.DefaultIfEmpty().Average())}; more than {FarEdge}: {open.Length} texels, mean {F(open.DefaultIfEmpty().Average())}\t{F(near.DefaultIfEmpty().Average() - open.DefaultIfEmpty().Average())}");
        var crevice = NoteJunction(polygons, n, rgba, open.DefaultIfEmpty().Average(), open.Length);
        Note($"crevice\tdrive side-face texels within {CreviceReach} m of the 8 end edges (all texels): {crevice.Length}, mean {F(crevice.DefaultIfEmpty().Average())}; open steel: {open.Length}, mean {F(open.DefaultIfEmpty().Average())}\t{F(crevice.DefaultIfEmpty().Average() - open.DefaultIfEmpty().Average())}");
        Assert.True(near.Length >= 20 && open.Length >= 20 && crevice.Length >= 20, $"too few texels: edge {near.Length}, open {open.Length}, crevice {crevice.Length}");
        Assert.True(near.Average() - open.Average() >= HardEdgeMargin, $"hard-edge mean luma {F(near.Average())} - open steel mean {F(open.Average())} = {F(near.Average() - open.Average())}, below {HardEdgeMargin}");
        Assert.True(open.Average() - crevice.Average() >= CreviceMargin, $"open steel mean luma {F(open.Average())} - drive-side crevice mean {F(crevice.Average())} = {F(open.Average() - crevice.Average())}, below {CreviceMargin}");
    }

    /// <summary>
    /// The luma of every texel of the grip (group rubber) as the file wears it (ruled 2026-11-24): a face's own material's base colour slot (a graph, baked
    /// here), or that material's flat base colour, or the face's own colour, or glTF's default white. Faces' texels are counted as the bake sees them.
    /// </summary>
    private static double[] GripLuma(string file)
    {
        var read = Read(file);
        var model = read.Model!;
        var named = model.Meshes.Single();
        var style = named.Mesh.Style ?? throw new InvalidOperationException("no groups or materials");
        var ct = TestContext.Current.CancellationToken;
        var baked = new Dictionary<string, (byte[] Rgba, int Size)>();
        var luma = new List<double>();
        foreach (var f in style.FacesOf("rubber") ?? throw new InvalidOperationException("no group rubber"))
        {
            var material = style.MaterialOf(f) is { } name ? model.FindMaterial(name) : null;
            if (material?.BaseColourTexture is MaterialGraph g)
            {
                var slot = g with { Path = Path.GetFullPath(g.Path, RunFolder) };
                var key = $"{slot.Path}|{slot.Output}|{slot.Size}";
                if (!baked.TryGetValue(key, out var b)) baked[key] = b = (GraphSlotBake.Rgba(slot, GraphSlotBake.BaseColour, model, named.Name, ct), slot.Size);
                luma.AddRange(Texels(named.Mesh, [f], b.Size, []).Select(t => Luma(b.Rgba, b.Size, t.X, t.Y)));
            }
            else if (material?.BaseColourTexture is not null) throw new InvalidOperationException($"the grip's material {material.Name} wears a PNG base colour; not measured");
            else
            {
                var (r, gg, bb) = FaceStyle.ToBytes(material?.BaseColour ?? style.ColourOf(f) ?? Carve.Core.Material.DefaultBaseColour);
                var l = 0.2126 * r + 0.7152 * gg + 0.0722 * bb;
                luma.AddRange(Texels(named.Mesh, [f], 1024, []).Select(_ => l));
            }
        }

        return [.. luma];
    }

    private static double Std(double[] v) => Math.Sqrt(v.Select(x => (x - v.Average()) * (x - v.Average())).Average());

    [SkippableOrRun]
    public void The_grip_wears_no_more_spread_than_the_starters_grip()
    {
        var (now, start) = (GripLuma("wrench-weathered.carve"), GripLuma("wrench-start.carve"));
        var (a, b) = (Std(now), Std(start));
        Note($"grip	std of luma over the grip's texels as worn: saved {now.Length} texels, mean {F(now.Average())}, std {F(a)}; starter {start.Length} texels, mean {F(start.Average())}, std {F(b)}	{F(a - b)}");
        Assert.True(now.Length >= 20 && start.Length >= 20, $"too few grip texels: {now.Length}, {start.Length}");
        Assert.True(a <= b + GripSlack, $"grip std of luma {F(a)} (saved file) - {F(b)} (starter file) = {F(a - b)}, above {GripSlack}");
    }

    // ---- the exported file ----

    [SkippableOrRun]
    public void The_glb_loads_with_no_warning()
    {
        var loaded = RunContent.Loader(RunFolder).Load(new ContentPath("models/wrench.glb"));
        Assert.Empty(loaded.Warnings);
        Assert.NotEmpty(loaded.SubmeshMaterials);
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_WEATHERING_RUN") is null)
        {
            Skip = "CARVE_WEATHERING_RUN is unset";
        }
    }
}

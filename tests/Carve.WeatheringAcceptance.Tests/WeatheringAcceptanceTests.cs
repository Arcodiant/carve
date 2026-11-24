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
    /// The margins of the two texel checks are the person's to rule after round 1's numbers are seen; until then each is 0 (strictly above / below,
    /// no margin), and the numbers are written out as <c>a - b</c> for the ruling.
    /// </summary>
    public const double HardEdgeMargin = 0, CreviceMargin = 0;

    private const int NearEdge = 2, FarEdge = 12;
    private static readonly int[] DriveFaces = [100, 101, 102, 103, 104, 105];

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_WEATHERING_RUN is unset");

    private static CarveFileResult Read(string name)
    {
        var path = Path.Combine(RunFolder, name);
        var read = CarveFile.Read(File.ReadAllText(path), RunFolder);
        return read.Model is null ? throw new InvalidOperationException($"{path}: {string.Join("; ", read.Errors)}") : read;
    }

    // ---- the transcript ----

    private sealed record Call(int Index, string Name, JsonElement Input);

    private static (string? Model, List<Call> Calls, List<(int Index, string Text)> Results) Transcript()
    {
        string? model = null;
        List<Call> calls = [];
        List<(int, string)> results = [];
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
                if (bt == "tool_use") calls.Add(new Call(i, block.GetProperty("name").GetString()!, block.GetProperty("input")));
                else if (bt == "tool_result") results.Add((i, block.GetProperty("content").ToString()));
            }
        }

        return (model, calls, results);
    }

    [SkippableOrRun]
    public void The_session_ran_on_the_pinned_model_with_no_file_tool()
    {
        var (model, calls, _) = Transcript();
        Assert.Equal(Model, model);
        Assert.NotEmpty(calls);
        Assert.All(calls, c => Assert.True(c.Name.StartsWith("mcp__carve__") || c.Name.StartsWith("mcp__patina__"), $"a tool that is neither carve's nor patina's: {c.Name}"));
    }

    [SkippableOrRun]
    public void The_missing_graph_message_came_and_the_next_steel_material_names_a_graph_that_exists()
    {
        var (_, calls, results) = Transcript();
        var message = results.FirstOrDefault(r => r.Text.Contains("is not there", StringComparison.Ordinal));
        Assert.True(message.Text is not null, "no tool result said a graph file is not there");
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
        var crevice = Texels(polygons, DriveFaces, n, segments).Where(t => t.Distance > NearEdge).Select(t => Luma(rgba, n, t.X, t.Y)).ToArray();
        Note($"hard-edge\twithin {NearEdge} texels: {near.Length} texels, mean {F(near.DefaultIfEmpty().Average())}; more than {FarEdge}: {open.Length} texels, mean {F(open.DefaultIfEmpty().Average())}\t{F(near.DefaultIfEmpty().Average() - open.DefaultIfEmpty().Average())}");
        Note($"crevice\tdrive texels (faces 100-105, over {NearEdge} from a hard edge): {crevice.Length}, mean {F(crevice.DefaultIfEmpty().Average())}; open steel: {open.Length}, mean {F(open.DefaultIfEmpty().Average())}\t{F(crevice.DefaultIfEmpty().Average() - open.DefaultIfEmpty().Average())}");
        Assert.True(near.Length >= 20 && open.Length >= 20 && crevice.Length >= 20, $"too few texels: edge {near.Length}, open {open.Length}, crevice {crevice.Length}");
        Assert.True(near.Average() - open.Average() > HardEdgeMargin, $"hard-edge mean luma {F(near.Average())} - open steel mean {F(open.Average())} = {F(near.Average() - open.Average())}, not above {HardEdgeMargin}");
        Assert.True(open.Average() - crevice.Average() > CreviceMargin, $"open steel mean luma {F(open.Average())} - drive crevice mean {F(crevice.Average())} = {F(open.Average() - crevice.Average())}, not above {CreviceMargin}");
    }

    [SkippableOrRun]
    public void The_grip_is_no_more_varied_than_the_clean_stand_in()
    {
        var (model, mesh, polygons, slot, _, rubber) = Saved();
        var n = slot.Size;
        var ct = TestContext.Current.CancellationToken;
        var baked = GraphSlotBake.Rgba(slot, GraphSlotBake.BaseColour, model, mesh, ct);
        var clean = GraphSlotBake.Rgba(slot, GraphSlotBake.BaseColour, model, null, ct);
        var texels = Texels(polygons, rubber, n, []);
        double Std(byte[] image)
        {
            var v = texels.Select(t => Luma(image, n, t.X, t.Y)).ToArray();
            var mean = v.Average();
            return Math.Sqrt(v.Select(x => (x - mean) * (x - mean)).Average());
        }

        var (a, b) = (Std(baked), Std(clean));
        Note($"rubber\tstd of luma over {texels.Count} texels: baked {F(a)}; clean stand-in {F(b)}\t{F(a - b)}");
        Assert.True(texels.Count >= 20, $"too few rubber texels: {texels.Count}");
        Assert.True(a <= b + 0.01, $"rubber std of luma {F(a)} (the bake) - {F(b)} (the clean stand-in) = {F(a - b)}, above 0.01");
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

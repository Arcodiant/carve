using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// The not-planar grouping (T-197, the person's ruling of 2026-11-24, which replaced T-72's "name 10, fold the rest") in each
/// tool's result through the real server: define_mesh, get_mesh, check, move, open and export on a row of lifted quads, and the leaky shed left as it was.
/// </summary>
[Trait("Tier", "unit")]
public sealed class NotPlanarFoldToolTests() : FileToolTests("carve-fold")
{
    private const string Fold = "select faces where non_planar names them";

    /// <summary>A row of <paramref name="quads"/> separate quads, each with one corner lifted off its plane.</summary>
    private static string Row(int quads)
    {
        var vertices = Enumerable.Range(0, quads).SelectMany(i => new[] { [i * 2, 0, 0], [i * 2 + 1, 0, 0], [i * 2 + 1, 1, 0], new float[] { i * 2, 1, 0.5f } }).ToArray();
        var faces = Enumerable.Range(0, quads).Select(i => new[] { i * 4, i * 4 + 1, i * 4 + 2, i * 4 + 3 });
        return JsonSerializer.Serialize(new { vertices, faces });
    }

    private static string[] Warnings(string json) =>
        JsonDocument.Parse(json).RootElement.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToArray();

    /// <summary>The one grouped not-planar line for a row of <paramref name="quads"/>, and nothing else about planarity.</summary>
    private static void ExpectGrouped(IEnumerable<string> warnings, int quads)
    {
        var line = Assert.Single(warnings, w => w.Contains("planar"));
        Assert.StartsWith($"{quads} faces are not planar: worst faces[", line);
        Assert.Contains(" off its plane (limit ", line);
        Assert.EndsWith($"; e.g. faces[0], faces[1], faces[2]; {Fold}", line);
    }

    [Fact]
    public async Task Define_mesh_get_mesh_check_and_move_group_the_faces_in_one_line()
    {
        var (carve, _) = await StartAsync(Row(12));
        using var _ = carve;

        ExpectGrouped(Warnings(Ok(await carve.CallToolAsync("get_mesh", "{}"))), 12);
        ExpectGrouped(Warnings(Ok(await carve.CallToolAsync("check", "{}"))), 12);
        ExpectGrouped(Warnings(Ok(await carve.CallToolAsync("move", """{"vertices":[0],"by":[0,0,0.01]}"""))), 12);

        // Every face is still named by select.
        var selected = JsonDocument.Parse(Ok(await carve.CallToolAsync("select", """{"kind":"faces","where":"non_planar"}"""))).RootElement;
        Assert.Equal(12, selected.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Three_faces_are_each_named_and_four_are_one_line()
    {
        var (carve, _) = await StartAsync(Row(3));
        using var _ = carve;
        var three = Warnings(Ok(await carve.CallToolAsync("check", "{}")));
        Assert.Equal(["faces[0]: ", "faces[1]: ", "faces[2]: "], three.Where(w => w.Contains("planar")).Select(w => w[..(w.IndexOf(':') + 2)]));

        Ok(await carve.CallToolAsync("define_mesh", Row(4)));
        ExpectGrouped(Warnings(Ok(await carve.CallToolAsync("check", "{}"))), 4);
    }

    [Fact]
    public async Task Open_and_export_group_the_same_way()
    {
        var (carve, _) = await StartAsync(Row(12));
        using var _ = carve;
        Ok(await carve.CallToolAsync("save", """{"path":"out/row.carve"}"""));

        ExpectGrouped(Warnings(Ok(await carve.CallToolAsync("open", """{"path":"out/row.carve"}"""))), 12);

        var exported = Ok(await carve.CallToolAsync("export", """{"path":"out/row.glb"}"""));
        var at = exported.IndexOf("warning(s): ", StringComparison.Ordinal);
        Assert.True(at >= 0, exported);
        // The grouped line holds "; " of its own, so it is cut out of the text whole (the row's open edges are a line before it).
        var text = exported[(at + "warning(s): ".Length)..];
        var from = text.IndexOf("12 faces are not planar", StringComparison.Ordinal);
        Assert.True(from >= 0, text);
        ExpectGrouped([text[from..(text.IndexOf(Fold, from, StringComparison.Ordinal) + Fold.Length)]], 12);
        Assert.Single(text.Split("not planar").Skip(1));
    }

    [Fact]
    public async Task The_leaky_sheds_warnings_are_grouped_by_kind()
    {
        var shed = Core.Tests.TestMeshes.LeakyShed();
        var (carve, _) = await StartAsync(JsonSerializer.Serialize(new { vertices = shed.Vertices.Select(v => new[] { v.X, v.Y, v.Z }), faces = shed.Faces.Select(f => f.Loop) }));
        using var _ = carve;
        var warnings = Warnings(Ok(await carve.CallToolAsync("check", "{}")));
        Assert.Equal(Core.Tests.TestMeshes.LeakyShedLines, warnings);
    }
}

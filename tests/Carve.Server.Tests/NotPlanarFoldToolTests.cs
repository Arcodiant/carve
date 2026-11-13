using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// The not-planar fold (the person's rule, 2026-11-13, "name 10, fold the rest") in each tool's result through the real
/// server: define_mesh, get_mesh, check, move, open and export on a row of lifted quads, and the leaky shed left as it was.
/// </summary>
[Trait("Tier", "regression")]
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

    private static void ExpectFolded(IEnumerable<string> warnings, int quads, string prefix = "")
    {
        var planar = warnings.Where(w => w.Contains("face is not planar:")).ToArray();
        Assert.Equal(Enumerable.Range(0, 10).Select(i => prefix + $"faces[{i}]: "), planar.Select(w => w[..(w.IndexOf(':') + 2)]));
        var more = quads - 10;
        Assert.Single(warnings, w => w == prefix + $"and {more} more {(more == 1 ? "face is" : "faces are")} not planar; {Fold}");
        // Nothing else about planarity: the named lines and the one fold.
        Assert.Equal(11, warnings.Count(w => w.Contains("planar")));
    }

    [Fact]
    public async Task Define_mesh_get_mesh_check_and_move_name_ten_faces_and_fold_the_rest()
    {
        var (carve, _) = await StartAsync(Row(12));
        using var _ = carve;

        ExpectFolded(Warnings(Ok(await carve.CallToolAsync("get_mesh", "{}"))), 12);
        ExpectFolded(Warnings(Ok(await carve.CallToolAsync("check", "{}"))), 12);
        ExpectFolded(Warnings(Ok(await carve.CallToolAsync("move", """{"vertices":[0],"by":[0,0,0.01]}"""))), 12);

        // Every face is still named by select.
        var selected = JsonDocument.Parse(Ok(await carve.CallToolAsync("select", """{"kind":"faces","where":"non_planar"}"""))).RootElement;
        Assert.Equal(12, selected.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Ten_faces_fold_nothing_and_eleven_fold_one()
    {
        var (carve, _) = await StartAsync(Row(10));
        using var _ = carve;
        var ten = Warnings(Ok(await carve.CallToolAsync("check", "{}")));
        Assert.Equal(10, ten.Count(w => w.Contains("face is not planar:")));
        Assert.DoesNotContain(ten, w => w.StartsWith("and "));

        Ok(await carve.CallToolAsync("define_mesh", Row(11)));
        var eleven = Warnings(Ok(await carve.CallToolAsync("check", "{}")));
        ExpectFolded(eleven, 11);
    }

    [Fact]
    public async Task Open_and_export_fold_the_same_way()
    {
        var (carve, _) = await StartAsync(Row(12));
        using var _ = carve;
        Ok(await carve.CallToolAsync("save", """{"path":"out/row.carve"}"""));

        ExpectFolded(Warnings(Ok(await carve.CallToolAsync("open", """{"path":"out/row.carve"}"""))), 12);

        var exported = Ok(await carve.CallToolAsync("export", """{"path":"out/row.glb"}"""));
        var at = exported.IndexOf("warning(s): ", StringComparison.Ordinal);
        Assert.True(at >= 0, exported);
        // The fold line holds a "; " of its own, so it is cut out before the warnings are split.
        var text = exported[(at + "warning(s): ".Length)..];
        var foldLine = $"and 2 more faces are not planar; {Fold}";
        Assert.Contains(foldLine, text);
        ExpectFolded(text.Replace(foldLine, "FOLD").Split("; ").Select(w => w == "FOLD" ? foldLine : w), 12);
    }

    [Fact]
    public async Task The_leaky_sheds_planted_faults_stay_named()
    {
        var shed = Core.Tests.TestMeshes.LeakyShed();
        var (carve, _) = await StartAsync(JsonSerializer.Serialize(new { vertices = shed.Vertices.Select(v => new[] { v.X, v.Y, v.Z }), faces = shed.Faces }));
        using var _ = carve;
        var warnings = Warnings(Ok(await carve.CallToolAsync("check", "{}")));
        Assert.Equal(Core.Tests.TestMeshes.LeakyShedWarnings, warnings);
    }
}

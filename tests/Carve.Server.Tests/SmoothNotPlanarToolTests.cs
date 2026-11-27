using System.Text.Json;
using static Carve.Server.Tests.VerbosityCalls;

namespace Carve.Server.Tests;

/// <summary>
/// A not-planar face on a smooth mesh is a note in check and no warning anywhere (T-198, the person's ruling of 2026-11-24), and a
/// flat mesh warns as before: the same lifted quads flat and smooth through check, get_mesh, export, move and subdivide.
/// </summary>
[Trait("Tier", "unit")]
public sealed class SmoothNotPlanarToolTests() : FileToolTests("carve-smooth")
{
    private const string Note = "expected on a smooth mesh, which export triangulates";

    private static string[] Strings(string json, string property) =>
        JsonDocument.Parse(json).RootElement.GetProperty(property).EnumerateArray().Select(w => w.GetString()!).ToArray();

    private static bool Planar(IEnumerable<string> lines) => lines.Any(w => w.Contains("planar"));

    private async Task<(ToolHarness Carve, JsonElement Tools)> Start(int quads, bool smooth)
    {
        var (carve, result) = await StartAsync(Row(quads));
        if (smooth) Ok(await carve.CallToolAsync("set_shading", """{"shading":"smooth"}"""));
        return (carve, result);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(2)]
    public async Task Flat_warns_and_smooth_notes_the_same_faces(int quads)
    {
        var (flat, _) = await Start(quads, false);
        using var _f = flat;
        var (smooth, _) = await Start(quads, true);
        using var _s = smooth;

        var flatCheck = Ok(await flat.CallToolAsync("check", "{}"));
        Assert.True(Planar(Strings(flatCheck, "warnings")));
        Assert.DoesNotContain(Strings(flatCheck, "notes"), n => n.EndsWith(Note));

        var check = Ok(await smooth.CallToolAsync("check", "{}"));
        Assert.False(Planar(Strings(check, "warnings")), check);
        var note = Assert.Single(Strings(check, "notes"), n => n.EndsWith(Note));
        Assert.StartsWith($"{quads} faces are not planar: worst faces[", note);
        Assert.EndsWith($"; select faces where non_planar names them; {Note}", note);
        var selected = JsonDocument.Parse(Ok(await smooth.CallToolAsync("select", """{"kind":"faces","where":"non_planar"}"""))).RootElement;
        Assert.Equal(quads, selected.GetProperty("count").GetInt32());

        // The other tools say nothing of it on the smooth mesh and as before on the flat one.
        Assert.False(Planar(Strings(Ok(await smooth.CallToolAsync("get_mesh", "{}")), "warnings")));
        Assert.False(Planar(Strings(Ok(await smooth.CallToolAsync("move", """{"vertices":[0],"by":[0,0,0.01]}""")), "warnings")));
        Assert.True(Planar(Strings(Ok(await flat.CallToolAsync("get_mesh", "{}")), "warnings")));
        Assert.True(Planar(Strings(Ok(await flat.CallToolAsync("move", """{"vertices":[0],"by":[0,0,0.01]}""")), "warnings")));
        Assert.DoesNotContain("not planar", Ok(await smooth.CallToolAsync("export", """{"path":"out/s.glb"}""")));
        Assert.Contains("not planar", Ok(await flat.CallToolAsync("export", """{"path":"out/f.glb"}""")));
    }

    [Fact]
    public async Task Set_shading_changes_the_answer_either_way_and_a_subdivided_mesh_reads_ok()
    {
        var (carve, _) = await Start(1, false);
        using var _ = carve;
        Assert.True(Planar(Strings(Ok(await carve.CallToolAsync("check", "{}")), "warnings")));
        Ok(await carve.CallToolAsync("set_shading", """{"shading":"smooth"}"""));
        var smooth = Ok(await carve.CallToolAsync("check", "{}"));
        Assert.False(Planar(Strings(smooth, "warnings")));
        Ok(await carve.CallToolAsync("set_shading", """{"shading":"flat"}"""));
        Assert.True(Planar(Strings(Ok(await carve.CallToolAsync("check", "{}")), "warnings")));

        // A closed cube, subdivided, smooth, with nothing else wrong: ok, and no subdivide warning of planarity.
        var cube = JsonSerializer.Serialize(new
        {
            vertices = new[] { [0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], new[] { 0, 1, 1 } },
            faces = new[] { [0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [2, 3, 7, 6], [1, 2, 6, 5], new[] { 3, 0, 4, 7 } },
        });
        Ok(await carve.CallToolAsync("define_mesh", cube));
        Ok(await carve.CallToolAsync("set_shading", """{"shading":"smooth"}"""));
        Assert.False(Planar(Strings(Ok(await carve.CallToolAsync("subdivide", """{"levels":2}""")), "warnings")));
        Assert.True(JsonDocument.Parse(Ok(await carve.CallToolAsync("check", "{}"))).RootElement.GetProperty("ok").GetBoolean());
    }
}

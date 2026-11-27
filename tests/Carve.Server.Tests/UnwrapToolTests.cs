using System.Text.Json;
using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>unwrap through the real carve (T-56): the barrel's worked example, the refusals, undo and the schema.</summary>
[Trait("Tier", "unit")]
public sealed class UnwrapToolTests
{
    private static string BarrelArguments()
    {
        var barrel = TestMeshes.Barrel();
        return JsonSerializer.Serialize(new { vertices = barrel.Vertices.Select(v => new[] { v.X, v.Y, v.Z }), faces = barrel.Faces.Select(f => f.Loop) });
    }

    private static readonly string Seams = JsonSerializer.Serialize(new { edges = TestMeshes.BarrelSeams.Select(p => new[] { p.A, p.B }), seam = true });

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static async Task<ToolHarness> StartAsync(bool seams = true)
    {
        var carve = new ToolHarness();
        try
        {
            await carve.InitializeAsync();
            Ok(await carve.CallToolAsync("define_mesh", BarrelArguments()));
            if (seams) Ok(await carve.CallToolAsync("mark_edges", Seams));
            return carve;
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    private static async Task<string> RefusedUnchanged(ToolHarness carve, string arguments)
    {
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));
        var result = await carve.CallToolAsync("unwrap", arguments);
        Assert.True(IsError(result), TextOf(result));
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        return TextOf(result);
    }

    [Fact]
    public async Task The_barrel_unwraps_into_three_islands_that_get_mesh_reads_back_and_undo_removes()
    {
        using var carve = await StartAsync();
        var tools = await carve.ListToolsAsync();
        var example = ToolList.Examples(tools, "unwrap").Single();
        var made = Ok(await carve.CallToolAsync("unwrap", example));

        Assert.Equal("mesh", made.GetProperty("mesh").GetString());
        Assert.Equal(3, made.GetProperty("islands").GetInt32());
        var islands = made.GetProperty("largest").EnumerateArray().ToArray();
        Assert.Equal([(32, 0), (1, 32), (1, 33)], islands.Select(i => (i.GetProperty("faces").GetInt32(), i.GetProperty("lowest_face").GetInt32())));
        Assert.All(islands, i => Assert.Equal(0, i.GetProperty("flipped").GetInt32()));
        Assert.All(islands, i => Assert.Equal(4, i.GetProperty("rect").GetArrayLength()));
        Assert.InRange(islands[0].GetProperty("stretch").GetDouble(), 1, 1.5);
        Assert.InRange(made.GetProperty("fill").GetDouble(), 0.3, 1);
        Assert.True(made.GetProperty("scale").GetDouble() > 0);

        // The same UVs as the Core makes, read back through get_mesh; the seams are untouched.
        var mesh = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        var uvs = mesh.GetProperty("uvs").EnumerateArray().ToArray();
        Assert.Equal(34, uvs.Length);
        var core = Carve.Core.Unwrap.Run(TestMeshes.SeamedBarrel()).Result!.Mesh;
        foreach (var entry in uvs)
        {
            var face = entry.GetProperty("face").GetInt32();
            Assert.Equal(core.Uvs!.Of(face)!.Select(p => (p.X, p.Y)), entry.GetProperty("uv").EnumerateArray().Select(p => (p[0].GetSingle(), p[1].GetSingle())));
        }
        Assert.Equal(TestMeshes.BarrelSeams.Length, mesh.GetProperty("seams").GetArrayLength());

        Ok(await carve.CallToolAsync("undo", "{}"));
        var undone = Ok(await carve.CallToolAsync("get_mesh", "{}"));
        Assert.False(undone.TryGetProperty("uvs", out _));
        Assert.Equal(TestMeshes.BarrelSeams.Length, undone.GetProperty("seams").GetArrayLength());
    }

    [Fact]
    public async Task Summary_counts_the_islands_and_lists_the_five_largest_and_full_adds_every_one()
    {
        // 8 islands: separate strips of 1 to 4 quads, a gap between each, so no seams are needed.
        using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var vertices = new List<int[]>();
        var faces = new List<int[]>();
        int[] sizes = [1, 3, 2, 3, 1, 4, 2, 1];
        var x = 0;
        foreach (var n in sizes)
        {
            var first = vertices.Count;
            for (var k = 0; k <= n; k++) { vertices.Add([x + k, 0, 0]); vertices.Add([x + k, 1, 0]); }
            for (var k = 0; k < n; k++) faces.Add([first + 2 * k, first + 2 * k + 2, first + 2 * k + 3, first + 2 * k + 1]);
            x += n + 2;
        }
        Ok(await carve.CallToolAsync("define_mesh", JsonSerializer.Serialize(new { vertices, faces })));
        var summary = Ok(await carve.CallToolAsync("unwrap", "{}"));
        Assert.Equal((8, 3), (summary.GetProperty("islands").GetInt32(), summary.GetProperty("single_face_islands").GetInt32()));
        Assert.False(summary.TryGetProperty("all", out _));
        // By face count, ties to the lowest face: 4 faces, then the 3s, then the 2s.
        var largest = summary.GetProperty("largest").EnumerateArray().Select(i => (i.GetProperty("faces").GetInt32(), i.GetProperty("lowest_face").GetInt32())).ToArray();
        Assert.Equal([(4, 10), (3, 1), (3, 6), (2, 4), (2, 14)], largest);
        var full = Ok(await carve.CallToolAsync("unwrap", """{"verbosity":"full"}"""));
        var all = full.GetProperty("all").EnumerateArray().ToArray();
        Assert.Equal(8, all.Length);
        // Each entry of all is an island in the same form as largest's, with every field, and largest's are the same entries.
        string[] fields = ["faces", "lowest_face", "rect", "stretch", "flipped"];
        Assert.All(all, i => Assert.All(fields, f => Assert.True(i.TryGetProperty(f, out _), $"{f} missing from {i}")));
        Assert.All(all, i => Assert.Equal(full.GetProperty("largest")[0].EnumerateObject().Select(p => p.Name), i.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(sizes.OrderBy(n => n).Sum(), all.Sum(i => i.GetProperty("faces").GetInt32()));
        foreach (var island in summary.GetProperty("largest").EnumerateArray())
            Assert.Equal(island.GetRawText(), all.Single(i => i.GetProperty("lowest_face").GetInt32() == island.GetProperty("lowest_face").GetInt32()).GetRawText());
        Assert.Equal(all.Select(i => i.GetProperty("lowest_face").GetInt32()).Order(), all.Select(i => i.GetProperty("lowest_face").GetInt32()));
        Assert.Equal(summary.GetProperty("flipped").GetInt32(), all.Sum(i => i.GetProperty("flipped").GetInt32()));
        var worst = all.OrderByDescending(i => i.GetProperty("stretch").GetDouble()).ThenBy(i => i.GetProperty("lowest_face").GetInt32()).First();
        Assert.Equal(worst.GetProperty("lowest_face").GetInt32(), summary.GetProperty("stretch").GetProperty("lowest_face").GetInt32());
        Assert.Equal(worst.GetProperty("stretch").GetDouble(), summary.GetProperty("stretch").GetProperty("worst").GetDouble());
        // full is the summary plus all.
        Assert.Equal(summary.GetProperty("largest").GetRawText(), full.GetProperty("largest").GetRawText());
        Assert.Equal("""{"ok":true}""", TextOf(await carve.CallToolAsync("unwrap", """{"verbosity":"silent"}""")));
    }

    [Fact]
    public async Task A_partial_set_into_and_margin_are_kept_and_the_rest_keeps_its_uvs()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("unwrap", """{"into":[0,0,0.5,0.5]}"""));
        var first = Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("uvs").EnumerateArray().Select(e => e.GetRawText()).ToArray();
        var made = Ok(await carve.CallToolAsync("unwrap", """{"faces":[32,33],"into":[0.5,0.5,1,1],"margin":0.01}"""));
        Assert.Equal([32, 33], made.GetProperty("largest").EnumerateArray().Select(i => i.GetProperty("lowest_face").GetInt32()));
        Assert.All(made.GetProperty("largest").EnumerateArray(), i => Assert.True(i.GetProperty("rect")[0].GetDouble() >= 0.51 - 1e-6));
        var second = Ok(await carve.CallToolAsync("get_mesh", "{}")).GetProperty("uvs").EnumerateArray().Select(e => e.GetRawText()).ToArray();
        Assert.Equal(first.Take(32), second.Take(32));
        Assert.NotEqual(first[32], second[32]);
    }

    [Theory]
    [InlineData("""{"faces":[40]}""", "faces[40]: face 40 is not a face of the mesh, which has 34")]
    [InlineData("""{"faces":[3,3]}""", "faces[3]: face 3 is listed more than once")]
    [InlineData("""{"faces":[]}""", "faces: the list is empty")]
    [InlineData("""{"faces":["a"]}""", "faces: the 1st listed entry: expected an integer id")]
    [InlineData("""{"into":[0,0,1]}""", "into: expected 4 numbers [u0, v0, u1, v1], got 3")]
    [InlineData("""{"into":[0,0,0,1]}""", "into: [0, 0, 0, 1] has no area")]
    [InlineData("""{"into":[0,0,1e39,1]}""", "into[2]: 1e39 is out of range: an into element must be a finite number within ±3.40282347E+38")]
    [InlineData("""{"margin":-1}""", "margin: -1 is not a margin into can take")]
    [InlineData("""{"margin":0.5}""", "margin: 0.5 is not a margin into can take")]
    [InlineData("""{"colour":1}""", "colour: not an argument of unwrap")]
    public async Task Each_refusal_names_its_element_and_changes_nothing(string arguments, string expected)
    {
        using var carve = await StartAsync();
        Assert.Contains(expected, await RefusedUnchanged(carve, arguments));
    }

    [Fact]
    public async Task A_barrel_with_no_seams_is_refused_naming_the_island()
    {
        using var carve = await StartAsync(seams: false);
        var text = await RefusedUnchanged(carve, "{}");
        Assert.Contains("faces[0]: an island of 34 faces with 0 boundary loops is not a disc; mark a seam across it with mark_edges", text);
    }
}

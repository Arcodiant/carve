extern alias carve;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>B-10: <c>get_mesh</c>'s <c>include</c> and <c>faces</c> read a large mesh in parts; with neither the answer is unchanged.</summary>
[Trait("Tier", "unit")]
public class GetMeshPartsTests(ITestOutputHelper output)
{
    private static string Sample(string name) => Path.Combine(Paths.RepoRoot, "samples", name);

    private static async Task<ToolHarness> OpenAsync(string sample)
    {
        var carve = new ToolHarness();
        await carve.InitializeAsync();
        var opened = await carve.CallToolAsync("open", JsonSerializer.Serialize(new { path = Sample(sample) }));
        Assert.False(CarveProcess.IsError(opened), CarveProcess.TextOf(opened));
        return carve;
    }

    private static async Task<string> TextAsync(ToolHarness carve, string arguments, bool refused = false)
    {
        var result = await carve.CallToolAsync("get_mesh", arguments);
        Assert.True(CarveProcess.IsError(result) == refused, CarveProcess.TextOf(result));
        return CarveProcess.TextOf(result);
    }

    /// <summary>The repo root the digests below were taken in (B-10's worktree checked out at c518cce).</summary>
    private const string TakenIn = @"D:\Repos\carve\branches\bug\B-10";

    private static string Escaped(string s) => JsonSerializer.Serialize(s)[1..^1];

    /// <summary>SHA-256 of <c>get_mesh</c>'s text on each sample with neither argument (a model of several meshes refuses <c>{}</c>, and that refusal is held too), taken on origin/milestone/M-29 (c518cce) before B-10.</summary>
    [Theory]
    [InlineData("wrench.carve", "{}", "8759792091DCB13090782AF3BE58BBFBC94BB2ABDCA607C751726D05166B28E0")]
    [InlineData("barrel-textured.carve", "{}", "CC6E5E26D4C96815B60D25E1C0B3AA3AA36DDC57B5AA7226DAF533ADFDC10D76")]
    [InlineData("gardener.carve", "{}", "38DB97E2FD7381DB66A8EEA897AA5CD7688B2CD29C998FA598ED888BCFD88E42")]
    [InlineData("gardener.carve", """{"mesh":"Body"}""", "BBE0E05730BBA99DC84032AF0BA9FDEF628E02DEDE2C74FF8D6C168C7312C99D")]
    [InlineData("rover.carve", """{"mesh":"Cab"}""", "428894068A6270C51A905874CC5B4938011E8417C7D3DB7C8424299581CD7799")]
    [InlineData("rover.carve", """{"mesh":"Cab","level":0}""", "67CCE472406D335D77530516B6D902EEFC33D3FF1DC763F6D46C0C748AC13DEB")]
    public async Task With_neither_argument_the_answer_is_byte_for_byte_as_before(string sample, string arguments, string before)
    {
        await using var carve = await OpenAsync(sample);
        // A texture's path is absolute, so the answer names the checkout it runs in; held as it read in the checkout the digests were taken in.
        var text = CarveProcess.TextOf(await carve.CallToolAsync("get_mesh", arguments))
            .Replace(Escaped(Paths.RepoRoot), Escaped(TakenIn), StringComparison.OrdinalIgnoreCase);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        output.WriteLine($"{sample} {arguments} {text.Length} {digest}");
        Assert.Equal(before, digest);
    }

    /// <summary>Claude Code's default maximum for a tool result is 25,000 tokens; held here in characters, which is stricter.</summary>
    private const int Limit = 25_000;

    private static readonly string[] WrenchGroups = ["rubber", "steel", "dark", "red", "black", "all"];

    [Fact]
    public async Task The_wrench_example_runs_as_written_and_each_answer_is_under_the_limit()
    {
        await using var carve = await OpenAsync("wrench.carve");
        var description = ToolList.Description(await carve.ListToolsAsync(), "get_mesh");
        output.WriteLine($"description {description.Length}");
        var examples = ToolList.Examples(description);
        Assert.Equal([carve::Carve.Server.GetMeshTool.WrenchGroups, carve::Carve.Server.GetMeshTool.WrenchSteel], examples);

        var groups = await TextAsync(carve, examples[0]);
        Assert.True(groups.Length < Limit, $"{groups.Length}");
        var names = JsonDocument.Parse(groups).RootElement.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("name").GetString());
        Assert.Equal(WrenchGroups, names);
        var steel = await TextAsync(carve, examples[1]);
        Assert.True(steel.Length < Limit, $"{steel.Length}");
        var body = JsonDocument.Parse(steel).RootElement;
        Assert.Equal(58, body.GetProperty("faces").GetArrayLength());
        Assert.Equal(["vertices", "faces", "closed", "bounds", "warnings", "parent", "world"], body.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task On_the_wrench_each_section_alone_and_each_group_s_faces_and_vertices_are_under_the_limit()
    {
        await using var carve = await OpenAsync("wrench.carve");
        var sizes = new List<(string Asked, int Length)>();
        foreach (var s in carve::Carve.Server.GetMeshTool.Sections)
        {
            var asked = JsonSerializer.Serialize(new { include = new[] { s } });
            sizes.Add((asked, (await TextAsync(carve, asked)).Length));
        }
        foreach (var g in WrenchGroups)
        {
            var asked = JsonSerializer.Serialize(new { faces = g, include = new[] { "faces", "vertices" } });
            sizes.Add((asked, (await TextAsync(carve, asked)).Length));
        }
        foreach (var (asked, length) in sizes) output.WriteLine($"{asked} {length}");
        var largest = sizes.MaxBy(s => s.Length);
        // The largest is the uvs alone, 16,312 characters, then group 'all' (every face) with faces and vertices, 22,533.
        Assert.True(largest.Length < Limit, $"largest: {largest.Asked} at {largest.Length} characters");
    }

    [Fact]
    public async Task Include_answers_the_named_sections_and_those_always_answered()
    {
        await using var carve = await OpenAsync("wrench.carve");
        var whole = JsonDocument.Parse(await TextAsync(carve, "{}")).RootElement;
        var body = JsonDocument.Parse(await TextAsync(carve, """{"include":["seams","faces"]}""")).RootElement;
        Assert.Equal(["faces", "seams", "closed", "bounds", "warnings", "parent", "world"], body.EnumerateObject().Select(p => p.Name));
        Assert.Equal(whole.GetProperty("faces").GetRawText(), body.GetProperty("faces").GetRawText());
        Assert.Equal(whole.GetProperty("seams").GetRawText(), body.GetProperty("seams").GetRawText());
        var none = JsonDocument.Parse(await TextAsync(carve, """{"include":[]}""")).RootElement;
        Assert.Equal(["closed", "bounds", "warnings", "parent", "world"], none.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Faces_narrows_the_per_face_sections_and_vertices_and_edges_follow()
    {
        await using var carve = await OpenAsync("wrench.carve");
        var whole = JsonDocument.Parse(await TextAsync(carve, "{}")).RootElement;
        var red = whole.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("name").GetString() == "red").GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()).ToArray();
        var byName = JsonDocument.Parse(await TextAsync(carve, """{"faces":"red"}""")).RootElement;
        var byIds = await TextAsync(carve, JsonSerializer.Serialize(new { faces = red.Reverse().Append(red[0]) }));
        Assert.Equal(byName.GetRawText(), JsonDocument.Parse(byIds).RootElement.GetRawText());

        Assert.Equal(red.Order(), byName.GetProperty("faces").EnumerateArray().Select(f => f.GetProperty("id").GetInt32()));
        var used = byName.GetProperty("faces").EnumerateArray().SelectMany(f => f.GetProperty("vertices").EnumerateArray().Select(v => v.GetInt32())).Distinct().Order();
        Assert.Equal(used, byName.GetProperty("vertices").EnumerateArray().Select(v => v.GetProperty("id").GetInt32()));
        var edges = byName.GetProperty("edges").EnumerateArray().ToArray();
        Assert.All(edges, e => Assert.Contains(e.GetProperty("faces").EnumerateArray(), f => red.Contains(f.GetInt32())));
        Assert.Equal(whole.GetProperty("edges").EnumerateArray().Count(e => e.GetProperty("faces").EnumerateArray().Any(f => red.Contains(f.GetInt32()))), edges.Length);
        var edgeKeys = edges.Select(e => e.GetProperty("vertices").GetRawText()).ToHashSet();
        Assert.All(byName.GetProperty("seams").EnumerateArray(), s => Assert.Contains(s.GetRawText(), edgeKeys));
        Assert.Equal(red.Order(), byName.GetProperty("uvs").EnumerateArray().Select(u => u.GetProperty("face").GetInt32()));
        foreach (var key in new[] { "groups", "colours" })
            Assert.All(byName.GetProperty(key).EnumerateArray(), g => Assert.All(g.GetProperty("faces").EnumerateArray(), f => Assert.Contains(f.GetInt32(), red)));
        Assert.Contains(byName.GetProperty("groups").EnumerateArray(), g => g.GetProperty("name").GetString() == "all");
        foreach (var key in new[] { "closed", "bounds", "warnings", "parent", "world" })
            Assert.Equal(whole.GetProperty(key).GetRawText(), byName.GetProperty(key).GetRawText());
    }

    [Fact]
    public async Task Faces_works_with_a_target_and_a_level()
    {
        await using var cube = new ToolHarness();
        await cube.InitializeAsync();
        var tools = await cube.ListToolsAsync();
        Assert.False(CarveProcess.IsError(await cube.CallToolAsync("define_mesh", ToolList.Cube(tools))));
        Assert.False(CarveProcess.IsError(await cube.CallToolAsync("define_target", ToolList.Examples(tools, "define_target")[0])));
        var body = JsonDocument.Parse(await TextAsync(cube, """{"target":"Bulge","faces":[0],"include":["vertices"]}""")).RootElement;
        var ids = body.GetProperty("vertices").EnumerateArray().Select(v => v.GetProperty("id").GetInt32().ToString()).ToHashSet();
        var moved = body.GetProperty("target").GetProperty("positions").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.All(moved, id => Assert.Contains(id, ids));
        Assert.True(moved.Length < 4);

        await using var rover = await OpenAsync("rover.carve");
        var level = JsonDocument.Parse(await TextAsync(rover, """{"mesh":"Cab","level":0,"faces":[0],"include":["faces"]}""")).RootElement;
        Assert.Equal(0, level.GetProperty("faces").EnumerateArray().Single().GetProperty("id").GetInt32());
        Assert.Equal(0, level.GetProperty("level").GetProperty("shown").GetInt32());
    }

    [Theory]
    [InlineData("""{"include":["vertex"]}""", "include: no section named 'vertex'; the sections are 'vertices', 'faces', 'edges'")]
    [InlineData("""{"include":"faces"}""", "include: expected a list of section names")]
    [InlineData("""{"faces":"blue"}""", "faces: no group named 'blue'; the mesh's groups are 'rubber', 'steel', 'dark', 'red', 'black', 'all'")]
    [InlineData("""{"faces":[9999]}""", "faces[9999]")]
    [InlineData("""{"faces":[]}""", "faces: name at least one face id, or a group's name")]
    [InlineData("""{"faces":3}""", "faces: expected a list of face ids or a group's name")]
    public async Task An_unknown_section_face_or_group_is_refused_with_what_exists(string arguments, string expected)
    {
        await using var carve = await OpenAsync("wrench.carve");
        Assert.Contains(expected, await TextAsync(carve, arguments, refused: true));
    }
}

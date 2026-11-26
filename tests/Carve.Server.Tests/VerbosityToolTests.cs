using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-200: <c>verbosity</c> on the fourteen tools that answer with a mesh report (M-23's ruling 3). <c>silent</c> is exactly
/// <c>{"ok":true}</c> with the change made (<c>check</c>: its own ok); a tool error is whole at every level; <c>summary</c>
/// is the call with no argument; <c>full</c> lists every warning; any other value is a tool error naming the three.
/// </summary>
[Trait("Tier", "unit")]
public sealed class VerbosityToolTests
{
    /// <summary>define_mesh's worked unit cube.</summary>
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>Two unit squares facing each other, as bridge's worked example has them.</summary>
    private const string Squares =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,1,2,3],[4,7,6,5]]}""";

    /// <summary>The tools with no <c>verbosity</c>, whose whole result is what was asked for (ruling 3).</summary>
    private static readonly string[] Readers =
        ["get_mesh", "get_parts", "get_skeleton", "get_weights", "get_clips", "select", "render", "render_glb", "help"];

    /// <summary>Each tool here: the calls that set it up, a call that succeeds, and a call that is a tool error.</summary>
    public static TheoryData<string, string[], string, string> Tools => new()
    {
        { "define_mesh", [], Cube, """{"vertices":[[0,0,0],[1,0,0]],"faces":[[0,1,9]]}""" },
        { "move", ["define_mesh " + Cube], """{"faces":[3],"by":[0,0.5,0]}""", """{"faces":[99],"by":[0,0.5,0]}""" },
        { "extrude", ["define_mesh " + Cube], """{"faces":[1],"distance":0.2}""", """{"faces":[99],"distance":0.2}""" },
        { "delete", ["define_mesh " + Cube], """{"faces":[3]}""", """{"faces":[99]}""" },
        { "merge_faces", ["define_mesh " + Cube, """loop_cut {"edge":[0,3],"at":[0.5]}"""], """{"faces":[1,7]}""", """{"faces":[1,99]}""" },
        { "bridge", ["define_mesh " + Squares], """{"loops":[[0,1,2,3],[4,5,6,7]]}""", """{"loops":[[0,1,2,3],[4,5,6,99]]}""" },
        { "plane_cut", ["define_mesh " + Cube], """{"point":[0,0.5,0],"normal":[0,1,0],"keep":"below"}""", """{"point":[0,5,0],"normal":[0,1,0],"keep":"below"}""" },
        { "loop_cut", ["define_mesh " + Cube], """{"edge":[0,3],"at":[0.5]}""", """{"edge":[0,6],"at":[0.5]}""" },
        { "knife", ["define_mesh " + Cube], """{"points":[[0.35,0,1],[0.35,0.7,1],[0.65,0.7,1],[0.65,0,1]]}""", """{"points":[[0.35,0,9],[0.35,0.7,9]]}""" },
        { "subdivide", ["define_mesh " + Cube], """{"levels":1}""", """{"levels":9}""" },
        { "undo", ["define_mesh " + Cube, """move {"faces":[3],"by":[0,0.5,0]}"""], "{}", """{"steps":2}""" },
        { "open", ["define_mesh " + Cube, """save {"path":"v.carve"}""", """move {"faces":[3],"by":[0,0.5,0]}"""], """{"path":"v.carve"}""", """{"path":"missing.carve"}""" },
        { "check", ["define_mesh " + Cube], "{}", """{"mesh":"nope"}""" },
        { "export", ["define_mesh " + Cube], """{"path":"v.glb"}""", """{"path":"v.txt"}""" },
        { "define_part", ["define_mesh " + Cube], """{"part":"p","translation":[0,1,0]}""", """{"part":"p","scale":[0,1,1]}""" },
        { "keep_level", ["define_mesh " + Cube], """{"level":0}""", """{"level":"x"}""" },
        { "define_group", ["define_mesh " + Cube], """{"name":"g","faces":[0,1]}""", """{"name":"g","faces":[99]}""" },
        { "set_colour", ["define_mesh " + Cube], """{"colour":"#C8503C","faces":[1]}""", """{"colour":"red","faces":[1]}""" },
        { "define_material", ["define_mesh " + Cube], """{"name":"m","roughness":0.25}""", """{"name":"m","roughness":2}""" },
        { "set_material", ["define_mesh " + Cube, """define_material {"name":"m","roughness":0.25}"""], """{"material":"m","faces":[2]}""", """{"material":"nope","faces":[2]}""" },
        { "set_shading", ["define_mesh " + Cube], """{"shading":"smooth"}""", """{"shading":"round"}""" },
        { "mark_edges", ["define_mesh " + Cube], """{"edges":[[0,3]]}""", """{"edges":[[0,6]]}""" },
        { "set_uvs", ["define_mesh " + Cube], """{"faces":[{"face":0,"uv":[[0,0],[1,0],[1,1],[0,1]]}]}""", """{"faces":[{"face":99,"uv":[[0,0],[1,0],[1,1],[0,1]]}]}""" },
        { "project_uvs", ["define_mesh " + Cube], """{"method":"box"}""", """{"method":"cube"}""" },
        { "define_skeleton", ["define_mesh " + Cube], Rig, """{"bones":[{"name":"a","parent":"zz","position":[0,0,0],"rotation":[0,0,0,1]}]}""" },
        { "set_weights", ["define_mesh " + Cube, "define_skeleton " + Rig], """{"vertices":[0,1,2,3,4,5,6,7],"weights":{"base":1}}""", """{"vertices":[0,1,2,3,4,5,6,7],"weights":{"nope":1}}""" },
        { "weigh", ["define_mesh " + Cube, "define_skeleton " + Rig], "{}", """{"bones":["nope"]}""" },
        { "define_clip", ["define_mesh " + Cube, "define_skeleton " + Rig], Wave, """{"name":"w","duration":-1,"channels":{}}""" },
        { "delete_clip", ["define_mesh " + Cube, "define_skeleton " + Rig, "define_clip " + Wave], """{"name":"wave"}""", """{"name":"zz"}""" },
        { "define_target", ["define_mesh " + Cube], Bulge, """{"name":"Bulge","positions":{"99":[0,0,0]}}""" },
        { "delete_target", ["define_mesh " + Cube, "define_target " + Bulge], """{"name":"Bulge"}""", """{"name":"zz"}""" },
        { "save", ["define_mesh " + Cube], """{"path":"v.carve"}""", """{"path":"v.txt"}""" },
        { "bake", ["define_mesh " + Cube, """project_uvs {"method":"box"}"""], """{"size":64,"padding":0,"maps":["normal"],"bevel":{"width":0.1}}""", """{"size":64,"maps":["nope"]}""" },
        { "displace", ["define_mesh " + Cube, """subdivide {"levels":2}"""], """{"source":{"noise":{"seed":7,"scale":0.35,"octaves":5}},"amount":0.6}""", """{"amount":"x"}""" },
    };

    private const string Rig = """{"bones":[{"name":"base","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]},{"name":"joint","parent":"base","position":[0,1,0],"rotation":[0,0,0,1]}]}""";

    private const string Wave =
        """{"name":"wave","duration":1,"loop":true,"channels":{"joint":{"rotation":{"interpolation":"LINEAR","keys":[{"time":0,"value":[0,0,0,1]},{"time":0.5,"value":[0,0,0.70710677,0.70710677]},{"time":1,"value":[0,0,0,1]}]}}}}""";

    private const string Bulge = """{"name":"Bulge","positions":{"2":[1.1,1.3,-0.1],"3":[-0.1,1.3,-0.1],"6":[1.1,1.3,1.1],"7":[-0.1,1.3,1.1]}}""";

    private static string With(string arguments, string verbosity) =>
        arguments.TrimEnd()[..^1] + (arguments.Trim() == "{}" ? "" : ",") + $"\"verbosity\":\"{verbosity}\"}}";

    private static string Text(JsonElement result) =>
        string.Concat(result.GetProperty("content").EnumerateArray().Select(c => c.GetProperty("text").GetString()));

    private static bool IsError(JsonElement result) => result.TryGetProperty("isError", out var e) && e.GetBoolean();

    /// <summary>A fresh carve in its own folder, set up by <paramref name="setup"/> ("tool {arguments}" each).</summary>
    private static async Task<ToolHarness> Start(string folder, string[] setup)
    {
        var carve = new ToolHarness(folder);
        await carve.InitializeAsync();
        foreach (var step in setup)
        {
            var at = step.IndexOf(' ');
            var result = await carve.CallToolAsync(step[..at], step[(at + 1)..]);
            Assert.False(IsError(result), $"{step}: {Text(result)}");
        }
        return carve;
    }

    /// <summary>What the call left: the model as the get_* tools read it, and the files in the folder.</summary>
    private static async Task<string> State(ToolHarness carve, string folder) =>
        string.Join("|", await Task.WhenAll(new[] { "get_mesh", "get_parts", "get_skeleton", "get_weights", "get_clips" }.Select(async t => Text(await carve.CallToolAsync(t, "{}"))))) + "|" + await Saved(carve, folder) + "|" + string.Join(",", Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(folder, f)).Order());

    /// <summary>The model as a file holds it, which shows what no get_* does (shading, hard edges); the file is removed again.</summary>
    private static async Task<string> Saved(ToolHarness carve, string folder)
    {
        var path = Path.Combine(folder, "state.carve");
        await carve.CallToolAsync("save", $"{{\"path\":{JsonSerializer.Serialize(path)}}}");
        if (!File.Exists(path)) return "no model";
        var text = await File.ReadAllTextAsync(path);
        File.Delete(path);
        return text;
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Each_level_answers_as_ruled(string tool, string[] setup, string arguments, string bad)
    {
        var root = Directory.CreateTempSubdirectory("carve-verbosity-").FullName;
        try
        {
            async Task<(string Text, bool Error, string Before, string After)> Run(string level, string args)
            {
                // One folder each run, emptied, so the paths in two results are the same.
                var folder = Path.Combine(root, "run");
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                Directory.CreateDirectory(folder);
                await using var carve = await Start(folder, setup);
                var before = await State(carve, folder);
                var result = await carve.CallToolAsync(tool, level == "" ? args : With(args, level));
                // bake times each map ("ms"), which differs from run to run.
                return (System.Text.RegularExpressions.Regex.Replace(Text(result), "\"ms\":[0-9]+", "\"ms\":0"), IsError(result), before, await State(carve, folder));
            }

            var plain = await Run("", arguments);
            Assert.False(plain.Error, plain.Text);

            var silent = await Run("silent", arguments);
            if (tool == "check")
            {
                Assert.Equal($"{{\"ok\":{(JsonDocument.Parse(plain.Text).RootElement.GetProperty("ok").GetBoolean() ? "true" : "false")}}}", silent.Text);
            }
            else
            {
                Assert.Equal("""{"ok":true}""", silent.Text);
                Assert.NotEqual(silent.Before, silent.After); // the change is made
            }
            Assert.Equal(plain.After, silent.After); // and is the same change

            Assert.Equal(plain.Text, (await Run("summary", arguments)).Text);
            var full = await Run("full", arguments);
            Assert.Equal(plain.After, full.After);
            if (full.Text != plain.Text)
            {
                // A kind of 4 or more (delete's open edges, subdivide's faces) is one line at summary and every one at full; nothing else differs.
                var (a, b) = (JsonDocument.Parse(plain.Text).RootElement, JsonDocument.Parse(full.Text).RootElement);
                Assert.Equal(a.EnumerateObject().Where(x => x.Name != "warnings").Select(x => x.ToString()), b.EnumerateObject().Where(x => x.Name != "warnings").Select(x => x.ToString()));
                Assert.True(b.GetProperty("warnings").GetArrayLength() > a.GetProperty("warnings").GetArrayLength());
                Assert.All(b.GetProperty("warnings").EnumerateArray(), w => Assert.Matches(@"^[a-z]+\[[0-9-]+\]: ", w.GetString()!));
            }

            var refused = await Run("", bad);
            Assert.True(refused.Error, $"{tool} {bad} was not refused: {refused.Text}");
            var refusedSilent = await Run("silent", bad);
            Assert.True(refusedSilent.Error);
            Assert.Equal(refused.Text, refusedSilent.Text);

            var loud = await Run("loud", arguments);
            Assert.True(loud.Error, loud.Text);
            Assert.Contains("verbosity: expected \"silent\", \"summary\" or \"full\", got \"loud\"", loud.Text);
            Assert.Equal(loud.Before, loud.After);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>displace shortens its list of warnings at summary, so full is the one that lists every one (T-201).</summary>
    [Fact]
    public async Task Full_on_displace_lists_every_open_edge()
    {
        await using var carve = await Start(Path.GetTempPath(), ["define_mesh " + Cube, """delete {"faces":[3]}"""]);
        const string Arguments = """{"source":{"noise":{"seed":7,"scale":0.35,"octaves":5}},"amount":0.01,"verbosity":"VERB"}""";
        var summary = JsonDocument.Parse(Text(await carve.CallToolAsync("displace", Arguments.Replace("VERB", "summary")))).RootElement.GetProperty("warnings");
        await carve.CallToolAsync("undo", "{}");
        var full = JsonDocument.Parse(Text(await carve.CallToolAsync("displace", Arguments.Replace("VERB", "full")))).RootElement.GetProperty("warnings");
        Assert.True(full.GetArrayLength() > summary.GetArrayLength(), full.ToString());
        Assert.All(full.EnumerateArray(), w => Assert.DoesNotContain("e.g.", w.GetString()));
    }

    [Fact]
    public async Task Check_at_silent_answers_its_own_ok_on_a_broken_mesh()
    {
        await using var carve = await Start(Path.GetTempPath(), ["define_mesh " + Cube, """delete {"faces":[3]}"""]);
        Assert.Equal("""{"ok":false}""", Text(await carve.CallToolAsync("check", """{"verbosity":"silent"}""")));
    }

    [Fact]
    public async Task A_value_that_is_not_a_string_is_a_tool_error()
    {
        await using var carve = await Start(Path.GetTempPath(), ["define_mesh " + Cube]);
        var result = await carve.CallToolAsync("move", """{"faces":[3],"by":[0,1,0],"verbosity":2}""");
        Assert.True(IsError(result));
        Assert.Contains("verbosity: expected \"silent\", \"summary\" or \"full\", got 2", Text(result));
    }

    [Fact]
    public async Task Full_on_the_holed_cage_lists_every_open_edge()
    {
        await using var carve = await Start(Path.GetTempPath(), []);
        var cage = Path.Combine(Paths.RepoRoot, "samples", "arms-cage.carve");
        foreach (var (tool, args) in new[] { ("open", JsonSerializer.Serialize(new { path = cage })), ("subdivide", """{"levels":1}"""), ("subdivide", """{"levels":1}""") })
            Assert.False(IsError(await carve.CallToolAsync(tool, args)));
        var faces = JsonSerializer.Serialize(Enumerable.Range(0, 200).Select(i => i * 2));

        var deleted = JsonDocument.Parse(Text(await carve.CallToolAsync("delete", $$"""{"faces":{{faces}},"verbosity":"full"}"""))).RootElement;
        var check = JsonDocument.Parse(Text(await carve.CallToolAsync("check", """{"verbosity":"full"}"""))).RootElement;
        foreach (var body in new[] { deleted, check })
        {
            var lines = body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToList();
            Assert.Equal(798, lines.Count(w => System.Text.RegularExpressions.Regex.IsMatch(w, @"^edges\[\d+-\d+\]: edge belongs to only one face$")));
            Assert.DoesNotContain(lines, w => w.Contains(" edges belong to only one face"));
        }
    }

    [Fact]
    public async Task Every_tool_has_verbosity_or_is_a_reader()
    {
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var tools = (await carve.ListToolsAsync()).EnumerateArray().ToList();
        var wrong = new List<string>();
        foreach (var tool in tools)
        {
            var name = tool.GetProperty("name").GetString()!;
            var has = tool.GetProperty("inputSchema").TryGetProperty("properties", out var p) && p.TryGetProperty("verbosity", out _);
            if (has == (Readers.Contains(name)))
                wrong.Add(has ? $"{name} has verbosity but is listed as without it" : $"{name} has no verbosity");
        }
        Assert.Empty(wrong);
    }
}

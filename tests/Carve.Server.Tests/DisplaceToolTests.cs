using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;
using static Carve.Server.Tests.ToolList;

namespace Carve.Server.Tests;

/// <summary>T-223's displace through the tools: the worked example (the boulder sample), undo, the graph source and every refusal.</summary>
[Trait("Tier", "unit")]
public sealed class DisplaceToolTests() : FileToolTests("carve-displace")
{
    /// <summary>A 32-texel graph: a u ramp as height and a red base colour.</summary>
    private const string RampGraph = """
        {
          "version": 1,
          "size": 32,
          "seed": 0,
          "nodes": {
            "c": { "type": "output", "inputs": { "in": "tint" }, "params": { "role": "base_colour" } },
            "h": { "type": "output", "inputs": { "in": "ramp" }, "params": { "role": "height" } },
            "ramp": { "type": "gradient", "params": { "axis": "u", "repeat": 1 } },
            "tint": { "type": "colour", "params": { "colour": "#ff0000" } }
          }
        }
        """;

    private static new JsonElement Ok(JsonElement result) => JsonDocument.Parse(FileToolTests.Ok(result)).RootElement;

    private static string SamplePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "gatehouse.carve"))) dir = dir.Parent!;
        return Path.Combine(dir.FullName, "samples", "boulder.carve");
    }

    private static string Positions(JsonElement mesh) => mesh.GetProperty("vertices").GetRawText();

    /// <summary>
    /// samples/boulder.carve is the worked example as written: the cube subdivided 3 levels, displaced, set smooth and
    /// saved. Set <c>CARVE_WRITE_BOULDER=1</c> to write it again.
    /// </summary>
    [Fact]
    public async Task The_example_makes_the_boulder_sample_and_one_undo_restores_every_position()
    {
        var (carve, tools) = await StartAsync();
        using var _ = carve;
        Ok(await carve.CallToolAsync("subdivide", """{"levels":3}"""));
        var smooth = Positions(Ok(await carve.CallToolAsync("get_mesh", "{}")));

        var body = Ok(await carve.CallToolAsync("displace", Examples(tools, "displace").Single()));
        Assert.Equal(386, body.GetProperty("vertices").GetInt32());
        Assert.Equal(384, body.GetProperty("faces").GetInt32());
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(386, body.GetProperty("moved").GetProperty("count").GetInt32());
        Assert.Equal(Enumerable.Range(0, 10), body.GetProperty("moved").GetProperty("first").EnumerateArray().Select(x => x.GetInt32()));
        Assert.InRange(body.GetProperty("largest").GetDouble(), 0.08, 0.3);
        Assert.DoesNotContain(body.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("too coarse"));
        var nonPlanar = Ok(await carve.CallToolAsync("select", """{"kind":"faces","where":"non_planar"}""")).GetProperty("count").GetInt32();
        Assert.Contains(body.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith($"{nonPlanar} faces are not planar: worst faces["));
        var rough = Positions(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        Assert.NotEqual(smooth, rough);

        Assert.Equal("displace", Ok(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
        Assert.Equal(smooth, Positions(Ok(await carve.CallToolAsync("get_mesh", "{}"))));

        Ok(await carve.CallToolAsync("displace", Examples(tools, "displace").Single()));
        Assert.Equal(rough, Positions(Ok(await carve.CallToolAsync("get_mesh", "{}"))));
        FileToolTests.Ok(await carve.CallToolAsync("set_shading", """{"shading":"smooth"}"""));
        FileToolTests.Ok(await carve.CallToolAsync("save", """{"path":"boulder.carve"}"""));
        var made = File.ReadAllText(Path.Combine(Root, "boulder.carve")).ReplaceLineEndings("\n");
        if (Environment.GetEnvironmentVariable("CARVE_WRITE_BOULDER") == "1") File.WriteAllText(SamplePath(), made);
        Assert.Equal(File.ReadAllText(SamplePath()).ReplaceLineEndings("\n"), made);
    }

    [Fact]
    public async Task A_coarse_cube_is_warned_of()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        var body = Ok(await carve.CallToolAsync("displace", """{"source":{"noise":{"scale":0.5}},"amount":0.2}"""));
        Assert.Contains(body.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.StartsWith("the mesh is too coarse for the noise"));
    }

    [Fact]
    public async Task A_graph_s_greyscale_output_displaces_a_mapped_mesh()
    {
        File.WriteAllText(Path.Combine(Root, "ramp.texgraph"), RampGraph);
        var (carve, _) = await StartAsync();
        using var __ = carve;
        Ok(await carve.CallToolAsync("subdivide", """{"levels":2}"""));

        Assert.Contains("project_uvs", Refused(await carve.CallToolAsync("displace", """{"source":{"graph":"ramp.texgraph"},"amount":0.1}""")));

        Ok(await carve.CallToolAsync("project_uvs", """{"method":"box"}"""));
        var body = Ok(await carve.CallToolAsync("displace", """{"source":{"graph":"ramp.texgraph","output":"height"},"amount":0.1}"""));
        Assert.True(body.GetProperty("closed").GetBoolean());
        Assert.Equal(98, body.GetProperty("vertices").GetInt32());
        Assert.InRange(body.GetProperty("largest").GetDouble(), 0.01, 0.05);
    }

    [Fact]
    public async Task Each_refusal_names_what_is_wrong_and_changes_nothing()
    {
        File.WriteAllText(Path.Combine(Root, "ramp.texgraph"), RampGraph);
        var (carve, _) = await StartAsync();
        using var __ = carve;
        Ok(await carve.CallToolAsync("project_uvs", """{"method":"box"}"""));
        var before = Positions(Ok(await carve.CallToolAsync("get_mesh", "{}")));

        var missing = Refused(await carve.CallToolAsync("displace", """{"source":{"graph":"rock.texgraph"},"amount":0.1}"""));
        Assert.Contains("the .texgraph files in", missing);
        Assert.Contains("ramp.texgraph", missing);
        var colour = Refused(await carve.CallToolAsync("displace", """{"source":{"graph":"ramp.texgraph","output":"base_colour"},"amount":0.1}"""));
        Assert.Contains("base_colour is a colour output", colour);
        Assert.Contains("greyscale outputs are height", colour);
        Assert.Contains("has no roughness output", Refused(await carve.CallToolAsync("displace", """{"source":{"graph":"ramp.texgraph","output":"roughness"},"amount":0.1}""")));
        Assert.Contains("not both", Refused(await carve.CallToolAsync("displace", """{"source":{"graph":"ramp.texgraph","noise":{}},"amount":0.1}""")));
        Assert.Contains("source: required", Refused(await carve.CallToolAsync("displace", """{"amount":0.1}""")));
        Assert.Contains("amount: required", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{}}}""")));
        Assert.Contains("amount: is zero", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{}},"amount":0}""")));
        Assert.Contains("from -2147483648 to 2147483647, got 4294967296", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{"seed":4294967296}},"amount":0.1}""")));
        Assert.Contains("expected a finite number of metres, got 1e400", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{"scale":1e400}},"amount":0.1}""")));
        Assert.Contains("output, default height", Refused(await carve.CallToolAsync("displace", """{"source":{},"amount":0.1}""")));
        Assert.Contains("octaves", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{"octaves":12}},"amount":0.1}""")));
        Assert.Contains("source.noise.size", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{"size":2}},"amount":0.1}""")));
        Assert.Contains("selection: matches no vertex", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{}},"amount":0.1,"vertices":[]}""")));
        Assert.Contains("faces[9]", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{}},"amount":0.1,"faces":[9]}""")));
        Assert.Contains("group", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{}},"amount":0.1,"group":"lid"}""")));
        Assert.Contains("spin: not an argument of displace", Refused(await carve.CallToolAsync("displace", """{"source":{"noise":{}},"amount":0.1,"spin":1}""")));

        Assert.Equal(before, Positions(Ok(await carve.CallToolAsync("get_mesh", "{}"))));
    }

    [Fact]
    /// <summary>
    /// The rebase onto T-223: displace reads a graph through Patina.Graphs' built-in registry, not M-29's Carve.Graphs, so a
    /// graph with a mesh-input node is refused as a fault and the mesh left as it was (the wording is a finding for the person).
    /// </summary>
    public async Task A_mesh_reading_graph_is_refused_and_changes_nothing()
    {
        var weathered = Path.Combine(Path.GetDirectoryName(SamplePath())!, "graphs", "weathered-iron.texgraph");
        var (carve, _) = await StartAsync();
        using var __ = carve;
        Ok(await carve.CallToolAsync("subdivide", """{"levels":2}"""));
        Ok(await carve.CallToolAsync("project_uvs", """{"method":"box"}"""));
        var before = Positions(Ok(await carve.CallToolAsync("get_mesh", "{}")));
        var refused = Refused(await carve.CallToolAsync("displace", $$"""{"source":{"graph":{{JsonSerializer.Serialize(weathered)}},"output":"wear"},"amount":0.1}"""));
        Assert.Contains("unknown type 'mesh_curvature'", refused);
        Assert.Equal(before, Positions(Ok(await carve.CallToolAsync("get_mesh", "{}"))));
    }

    [Fact]
    public async Task A_group_or_faces_displace_only_their_corners()
    {
        var (carve, _) = await StartAsync();
        using var __ = carve;
        Ok(await carve.CallToolAsync("define_group", """{"name":"top","faces":[3]}"""));
        var body = Ok(await carve.CallToolAsync("displace", """{"source":{"noise":{"seed":3}},"amount":0.2,"group":"top","axis":[0,1,0]}"""));
        Assert.Equal("""{"count":4,"first":[2,3,6,7]}""", body.GetProperty("moved").GetRawText());
    }
}

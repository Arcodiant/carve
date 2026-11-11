using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-38: <c>get_weights</c> reads back what <c>weigh</c> and <c>set_weights</c> left, and warns of the gaps.</summary>
[Trait("Tier", "regression")]
public sealed class GetWeightsToolTests
{
    private static readonly string Cube = JsonSerializer.Serialize(new
    {
        name = "cube",
        vertices = TestMeshes.CubeVertices.Select(v => new[] { v.X, v.Y, v.Z }),
        faces = TestMeshes.CubeFaces,
    });

    private const string Chain =
        """{"bones":[{"name":"Hips","parent":null,"position":[0,-0.5,0],"rotation":[0,0,0,1]},{"name":"Head","parent":"Hips","position":[0,1,0],"rotation":[0,0,0,1]},{"name":"Tail","parent":"Hips","position":[0,0,5],"rotation":[0,0,0,1]}]}""";

    private static void Ok(JsonElement r) => Assert.False(IsError(r), TextOf(r));

    private static async Task<CarveProcess> StartAsync()
    {
        var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Ok(await carve.CallToolAsync("define_skeleton", Chain));
        return carve;
    }

    private static JsonElement Body(JsonElement r) { Ok(r); return JsonDocument.Parse(TextOf(r)).RootElement; }

    [Fact]
    public async Task The_worked_example_runs_as_written_and_gives_the_result_its_description_shows()
    {
        using var carve = await StartAsync();
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "get_weights").GetProperty("description").GetString()!;
        var lines = description.Split('\n');
        var example = lines.Single(l => l.StartsWith("Example: ", StringComparison.Ordinal))["Example: ".Length..];
        Assert.Equal("""{"vertices":[0,7]}""", example);

        // The description's premise: set_weights binds vertices 0 and 1 of the cube wholly to Head.
        Ok(await carve.CallToolAsync("set_weights", """{"vertices":[0,1],"weights":{"Head":1}}"""));
        var result = JsonNode.Parse(TextOf(await carve.CallToolAsync("get_weights", example)));
        var shown = JsonNode.Parse(lines.Single(l => l.StartsWith("Result: ", StringComparison.Ordinal))["Result: ".Length..]);
        Assert.True(JsonNode.DeepEquals(shown, result), result!.ToJsonString());

        // What the result says, named, so a regression reads as one: a weighted vertex by name, an unweighted one empty, the
        // unweighted count and ids, and the idle bones, Head now followed.
        Assert.Equal(1, result["vertices"]![0]!["weights"]!["Head"]!.GetValue<double>());
        Assert.Empty(result["vertices"]![1]!["weights"]!.AsObject());
        Assert.Equal(6, result["summary"]!["unweighted"]!.GetValue<int>());
        Assert.StartsWith("6 vertices have no weight: 2, 3, 4, 5, 6, 7", result["warnings"]![0]!.GetValue<string>());
        Assert.Equal("note: no vertex follows 'Hips', 'Tail'", result["warnings"]![1]!.GetValue<string>());
    }

    [Fact]
    public async Task A_group_override_after_weigh_reads_back_as_set_and_the_rest_as_weighed_with_no_warnings()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("weigh", "{}"));
        var top = Body(await carve.CallToolAsync("get_weights", "{}"));
        Assert.DoesNotContain(top.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("no weight"));

        Ok(await carve.CallToolAsync("define_group", """{"name":"lid","faces":[0]}"""));
        Ok(await carve.CallToolAsync("set_weights", """{"group":"lid","weights":{"Head":3,"Tail":1}}"""));
        var lid = Body(await carve.CallToolAsync("get_weights", "{}"));
        var lidVertices = TestMeshes.CubeFaces[0].ToHashSet();
        var set = lid.GetProperty("vertices").EnumerateArray().Where(v => lidVertices.Contains(v.GetProperty("vertex").GetInt32())).ToArray();
        Assert.Equal(lidVertices.Count, set.Length);
        foreach (var v in set)
        {
            Assert.Equal(0.75, v.GetProperty("weights").GetProperty("Head").GetDouble(), 12);
            Assert.Equal(0.25, v.GetProperty("weights").GetProperty("Tail").GetDouble(), 12);
        }
        Assert.DoesNotContain(lid.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("no weight"));

        // By vertex and by bone agree with the full read.
        var one = Body(await carve.CallToolAsync("get_weights", $$"""{"vertices":[{{set[0].GetProperty("vertex").GetInt32()}}]}"""));
        Assert.Equal(0.75, one.GetProperty("vertices")[0].GetProperty("weights").GetProperty("Head").GetDouble(), 12);
        var byBone = Body(await carve.CallToolAsync("get_weights", """{"bone":"Tail"}"""));
        Assert.Equal("Tail", byBone.GetProperty("bone").GetString());
        var followers = byBone.GetProperty("followers").EnumerateArray().ToArray();
        Assert.All(set, v => Assert.Contains(followers, f => f.GetProperty("vertex").GetInt32() == v.GetProperty("vertex").GetInt32() && Math.Abs(f.GetProperty("weight").GetDouble() - 0.25) < 1e-12));
    }

    [Fact]
    public async Task Partial_weights_list_the_unweighted_vertices_and_a_bone_nobody_follows_is_a_note()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("set_weights", """{"vertices":[0,1,2],"weights":{"Head":1}}"""));
        var body = Body(await carve.CallToolAsync("get_weights", """{"bone":"Head"}"""));
        Assert.Equal(3, body.GetProperty("followers").GetArrayLength());
        var summary = body.GetProperty("summary");
        Assert.Equal(5, summary.GetProperty("unweighted").GetInt32());
        Assert.Equal([3, 4, 5, 6, 7], summary.GetProperty("unweightedVertices").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(3, summary.GetProperty("boneVertices").GetProperty("Head").GetInt32());
        var warnings = body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToArray();
        Assert.Contains("5 vertices have no weight: 3, 4, 5, 6, 7", warnings[0]);
        Assert.Contains("no vertex follows 'Hips', 'Tail'", warnings[1]);
    }

    [Theory]
    [InlineData("""{"vertices":[0],"bone":"Head"}""", "not both")]
    [InlineData("""{"bone":"Nose"}""", "no bone named 'Nose'")]
    [InlineData("""{"vertices":[99]}""", "99")]
    [InlineData("""{"vertices":[]}""", "vertices")]
    [InlineData("""{"extra":1}""", "extra")]
    public async Task Bad_requests_are_tool_errors(string arguments, string expected)
    {
        using var carve = await StartAsync();
        var r = await carve.CallToolAsync("get_weights", arguments);
        Assert.True(IsError(r));
        Assert.Contains(expected, TextOf(r));
    }

    [Fact]
    public async Task Weigh_with_bones_weighs_only_the_vertices_nearest_a_named_bone_wholly_to_it()
    {
        // On the unit cube, Head (0, 0.5, 0) to (0, 1, 0) is the nearest bone of the whole skeleton to vertices 2, 3, 6 and 7;
        // Hips is nearest to 0, 1, 4 and 5 (D-6's bones scope), which stay unweighted.
        using var carve = await StartAsync();
        var weighed = Body(await carve.CallToolAsync("weigh", """{"bones":["Head"]}"""));
        Assert.Equal(4, weighed.GetProperty("weighed").GetInt32());
        Assert.Equal(0, weighed.GetProperty("kept").GetInt32());
        Assert.Equal(4, weighed.GetProperty("unweighted").GetInt32());

        var read = Body(await carve.CallToolAsync("get_weights", "{}"));
        var byVertex = read.GetProperty("vertices").EnumerateArray().ToDictionary(
            v => v.GetProperty("vertex").GetInt32(),
            v => v.GetProperty("weights").EnumerateObject().Select(p => (p.Name, p.Value.GetDouble())).ToArray());
        foreach (var v in new[] { 2, 3, 6, 7 }) Assert.Equal([("Head", 1.0)], byVertex[v]);
        foreach (var v in new[] { 0, 1, 4, 5 }) Assert.Empty(byVertex[v]);
        Assert.Equal([0, 1, 4, 5], read.GetProperty("summary").GetProperty("unweightedVertices").EnumerateArray().Select(e => e.GetInt32()));
    }

    [Fact]
    public async Task Weigh_with_no_skeleton_or_an_empty_bones_list_is_a_tool_error_and_no_undo_step()
    {
        using var bare = new CarveProcess([]);
        await bare.InitializeAsync();
        Ok(await bare.CallToolAsync("define_mesh", Cube));
        var noSkeleton = await bare.CallToolAsync("weigh", "{}");
        Assert.True(IsError(noSkeleton));
        Assert.Contains("no skeleton yet", TextOf(noSkeleton));
        Assert.Contains("nothing to undo", TextOf(await bare.CallToolAsync("undo", "{}"))); // define_mesh is still the only step

        using var carve = await StartAsync();
        var empty = await carve.CallToolAsync("weigh", """{"bones":[]}""");
        Assert.True(IsError(empty));
        Assert.Contains("bones: an empty list weighs nothing", TextOf(empty));
        Assert.Equal("define_skeleton", Body(await carve.CallToolAsync("undo", "{}")).GetProperty("undone").GetString());
    }

    [Fact]
    public async Task Without_a_skeleton_or_a_mesh_it_is_a_tool_error()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        Assert.Contains("no mesh", TextOf(await carve.CallToolAsync("get_weights", "{}")));
        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Assert.Contains("no skeleton", TextOf(await carve.CallToolAsync("get_weights", "{}")));
    }
}

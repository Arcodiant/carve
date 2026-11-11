using System.Text.Json;
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
    public async Task The_worked_example_runs_and_reads_unweighted_vertices_as_empty_with_a_warning()
    {
        using var carve = await StartAsync();
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "get_weights").GetProperty("description").GetString()!;
        var example = description.Split('\n').Single(l => l.StartsWith("Example: ", StringComparison.Ordinal))["Example: ".Length..];
        Assert.Equal("""{"vertices":[0,7]}""", example);

        var body = Body(await carve.CallToolAsync("get_weights", example));
        Assert.Equal(2, body.GetProperty("vertices").GetArrayLength());
        Assert.Empty(body.GetProperty("vertices")[0].GetProperty("weights").EnumerateObject());
        var warnings = body.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()!).ToArray();
        Assert.Contains("8 vertices have no weight: 0, 1, 2, 3, 4, 5, 6, 7", warnings[0]);
        Assert.Contains("no vertex follows 'Hips', 'Head', 'Tail'", warnings[1]);
        Assert.Equal(8, body.GetProperty("summary").GetProperty("unweighted").GetInt32());
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
        var set = lid.GetProperty("vertices").EnumerateArray().Where(v => v.GetProperty("weights").TryGetProperty("Tail", out _)).ToArray();
        Assert.NotEmpty(set);
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
        Assert.Equal(set.Length, followers.Length);
        Assert.All(followers, f => Assert.Equal(0.25, f.GetProperty("weight").GetDouble(), 12));
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
    public async Task Without_a_skeleton_or_a_mesh_it_is_a_tool_error()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        Assert.Contains("no mesh", TextOf(await carve.CallToolAsync("get_weights", "{}")));
        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Assert.Contains("no skeleton", TextOf(await carve.CallToolAsync("get_weights", "{}")));
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Core.Tests;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-38: <c>get_weights</c> reads back what <c>weigh</c> and <c>set_weights</c> left, and warns of the gaps.</summary>
[Trait("Tier", "unit")]
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

    private static async Task<ToolHarness> StartAsync()
    {
        var carve = new ToolHarness();
        try
        {
            await carve.InitializeAsync();
            Ok(await carve.CallToolAsync("define_mesh", Cube));
            Ok(await carve.CallToolAsync("define_skeleton", Chain));
            return carve;
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    private static JsonElement Body(JsonElement r) { Ok(r); return JsonDocument.Parse(TextOf(r)).RootElement; }

    [Fact]
    public async Task A_deleted_vertex_is_left_out_of_weigh_and_get_weights_and_named_as_deleted()
    {
        // M-5's delete drops a vertex's weights and leaves a tombstone: weigh must not weigh it again, nor anything count it.
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("weigh", "{}"));
        Ok(await carve.CallToolAsync("delete", """{"vertices":[6]}"""));

        var weighed = Body(await carve.CallToolAsync("weigh", "{}"));
        Assert.Equal((0, 7, 0), (weighed.GetProperty("weighed").GetInt32(), weighed.GetProperty("kept").GetInt32(), weighed.GetProperty("unweighted").GetInt32()));

        var read = Body(await carve.CallToolAsync("get_weights", "{}"));
        Assert.Equal(7, read.GetProperty("summary").GetProperty("vertices").GetInt32());
        Assert.Equal(0, read.GetProperty("summary").GetProperty("unweighted").GetInt32());
        Assert.DoesNotContain(6, read.GetProperty("vertices").EnumerateArray().Select(v => v.GetProperty("vertex").GetInt32()));

        var named = await carve.CallToolAsync("get_weights", """{"vertices":[6]}""");
        Assert.True(IsError(named));
        Assert.Contains("vertices[6]: deleted", TextOf(named));
    }

    [Fact]
    public async Task A_cuts_blended_weights_read_back_and_a_plain_weigh_keeps_them()
    {
        // M-5's cuts blend new vertices' weights from their edge's ends (T-67); M-17's get_weights must read them back and a
        // weigh without replace must keep them, as it keeps any existing weight (D-6).
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("set_weights", """{"vertices":[0,1,4,5],"weights":{"Hips":1}}"""));
        Ok(await carve.CallToolAsync("set_weights", """{"vertices":[2,3,6,7],"weights":{"Head":1}}"""));
        var cut = Body(await carve.CallToolAsync("loop_cut", """{"edge":[0,3],"at":[0.25]}"""));
        var created = cut.GetProperty("created").GetProperty("vertices").EnumerateArray().Select(v => v.GetInt32()).ToArray();
        Assert.Equal([8, 9, 10, 11], created);

        void Blended(JsonElement read)
        {
            Assert.Equal(12, read.GetProperty("summary").GetProperty("vertices").GetInt32());
            Assert.Equal(0, read.GetProperty("summary").GetProperty("unweighted").GetInt32());
            var byId = read.GetProperty("vertices").EnumerateArray().ToDictionary(v => v.GetProperty("vertex").GetInt32(), v => v.GetProperty("weights"));
            foreach (var v in created)
            {
                Assert.Equal(0.75, byId[v].GetProperty("Hips").GetDouble(), 12);
                Assert.Equal(0.25, byId[v].GetProperty("Head").GetDouble(), 12);
            }
        }

        Blended(Body(await carve.CallToolAsync("get_weights", "{}")));
        var weighed = Body(await carve.CallToolAsync("weigh", "{}"));
        Assert.Equal((0, 12, 0), (weighed.GetProperty("weighed").GetInt32(), weighed.GetProperty("kept").GetInt32(), weighed.GetProperty("unweighted").GetInt32()));
        Blended(Body(await carve.CallToolAsync("get_weights", "{}")));

        var named = Body(await carve.CallToolAsync("get_weights", """{"vertices":[8]}"""));
        Assert.Equal(0.75, named.GetProperty("vertices")[0].GetProperty("weights").GetProperty("Hips").GetDouble(), 12);
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
        using var bare = new ToolHarness();
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
        using var carve = new ToolHarness();
        await carve.InitializeAsync();
        Assert.Contains("no mesh", TextOf(await carve.CallToolAsync("get_weights", "{}")));
        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Assert.Contains("no skeleton", TextOf(await carve.CallToolAsync("get_weights", "{}")));
    }
}

using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using static Carve.Server.Tests.CarveProcess;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>T-40: the heat colours and the weights look's refusals; where the colours land is the crate scenario's.</summary>
[Trait("Tier", "regression")]
public sealed class WeightsLookTests() : TempFolderTests("carve-weights-look")
{
    private const string Tri = """{"vertices":[[0,0,0],[1,0,0],[0,1,0]],"faces":[[0,1,2]]}""";
    private const string Rig = """{"bones":[{"name":"Hips","parent":null,"position":[0,0,0],"rotation":[0,0,0,1]},{"name":"Spine","parent":"Hips","position":[0,1,0],"rotation":[0,0,0,1]}]}""";

    [Theory]
    [InlineData(0f, 0f, 0f, 0.5f)]
    [InlineData(0.5f, 0f, 1f, 0f)]
    [InlineData(1f, 1f, 0f, 0f)]
    [InlineData(0.25f, 0f, 0.5f, 0.25f)]
    [InlineData(float.NaN, 1f, 0f, 1f)]
    public void Heat_runs_dark_blue_through_green_to_red_and_magenta_for_none(float weight, float r, float g, float b) =>
        Assert.Equal(new Vector3(r, g, b), LookMeshes.HeatColour(weight));

    [Fact]
    public void The_weights_look_colours_each_corner_from_its_vertex_and_is_unlit()
    {
        var mesh = TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(0, 1, 0)], [[0, 1, 2]]);
        var scene = LookMeshes.Compose(mesh, DrawnSpace.Of(Vector3.Zero, Vector3.One), Look.Weights, Highlight.None, [1f, 0f, float.NaN]);
        Assert.Equal(Vector4.One, scene.FlatColour);
        // A, then C, then B.
        Assert.Equal([Rgba8.FromVector(LookMeshes.HeatColour(1)), Rgba8.FromVector(LookMeshes.Unweighted), Rgba8.FromVector(LookMeshes.HeatColour(0))], scene.VertexColours!);
    }

    [Fact]
    public async Task An_unknown_bone_lists_the_names_and_the_caption_names_the_bone()
    {
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Assert.False(IsError(await carve.CallToolAsync("define_mesh", Tri)));

        var none = await carve.CallToolAsync("render", """{"weights":"Hips"}""");
        Assert.True(IsError(none), TextOf(none));
        Assert.Contains("no skeleton", TextOf(none));

        Assert.False(IsError(await carve.CallToolAsync("define_skeleton", Rig)));
        var wrong = await carve.CallToolAsync("render", """{"weights":"Elbow"}""");
        Assert.True(IsError(wrong), TextOf(wrong));
        Assert.Contains("'Elbow'", TextOf(wrong));
        Assert.Contains("'Hips', 'Spine'", TextOf(wrong));

        var both = await carve.CallToolAsync("render", """{"weights":"Hips","mode":"normals"}""");
        Assert.True(IsError(both), TextOf(both));

        var drawn = await carve.CallToolAsync("render", """{"weights":"Hips","size":256}""");
        Assert.False(IsError(drawn), "render failed");
        Assert.Equal("256x256, view front, weights Hips, 3 vertices, 1 faces", TextOf(drawn, 1));
    }
}

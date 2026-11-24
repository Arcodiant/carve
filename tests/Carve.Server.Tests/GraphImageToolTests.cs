using System.Text.Json;
using Carve.Core;

namespace Carve.Server.Tests;

/// <summary>
/// T-165: render's <c>image</c> as <c>{graph, output, size}</c> under mode <c>uvs</c>. On <see cref="ToolHarness"/> (no engine) every
/// refusal that comes before the engine names image, and <see cref="GraphBakes.Image"/> bakes on first use and not on the second;
/// the regression test draws the description's worked example through the real process.
/// </summary>
[Trait("Tier", "unit")]
public sealed class GraphImageToolTests() : FileToolTests("carve-graph-image")
{
    private const string Graph = """{ "version": 2, "size": 64, "seed": 1, "nodes": { "m": { "type": "mesh_curvature" }, "o": { "type": "output", "inputs": { "in": "m" }, "params": { "name": "wear" } } } }""";

    private const string Example = """{"mode":"uvs","image":{"graph":"weathered-iron.texgraph","output":"wear","size":256}}""";

    private void Files()
    {
        File.WriteAllText(Path.Combine(Root, "weathered-iron.texgraph"), Graph);
        File.WriteAllText(Path.Combine(Root, "notgraph.texgraph"), "nope");
    }

    [Theory]
    [InlineData("""{"mode":"uvs","image":{"graph":"weathered-iron.texgraph","output":"wear","extra":1}}""", "image.extra: an image graph takes only graph, output and size")]
    [InlineData("""{"mode":"uvs","image":{"graph":"weathered-iron.texgraph"}}""", "image: a graph image needs output")]
    [InlineData("""{"mode":"uvs","image":{"output":"wear"}}""", "image: a graph image needs graph")]
    [InlineData("""{"mode":"uvs","image":{"graph":"weathered-iron.texgraph","output":"wear","size":"big"}}""", "image.size: expected an integer")]
    [InlineData("""{"mode":"uvs","image":{"graph":"weathered-iron.texgraph","output":"wear","size":100}}""", "image: size is 100")]
    [InlineData("""{"mode":"uvs","image":{"graph":"nope.texgraph","output":"wear"}}""", "image.graph: no file at ")]
    [InlineData("""{"mode":"uvs","image":{"graph":"notgraph.texgraph","output":"wear"}}""", "image.graph: ")]
    [InlineData("""{"mode":"uvs","image":7}""", "image: expected a PNG path or {graph, output, size}")]
    [InlineData("""{"mode":"checker","image":{"graph":"weathered-iron.texgraph","output":"wear"}}""", "image: not with mode 'checker'")]
    public async Task A_bad_graph_image_is_refused_naming_image(string arguments, string expected)
    {
        Files();
        var (carve, _) = await StartAsync(mesh: null);
        using var _ = carve;
        var result = await carve.CallToolAsync("render", arguments);
        Assert.True(IsError(result));
        Assert.Contains(expected, TextOf(result));
    }

    [Fact]
    public async Task The_description_carries_the_worked_example_and_the_output_bakes_once()
    {
        Files();
        var (carve, tools) = await StartAsync(mesh: null);
        using var _ = carve;
        var description = tools.EnumerateArray().First(t => t.GetProperty("name").GetString() == "render").GetProperty("description").GetString()!;
        Assert.Contains(Example["{\"mode\":\"uvs\",".Length..^1], description);
        Ok(await carve.CallToolAsync("define_mesh", BakeToolTests.Strip));
        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
        var bakes = new GraphBakes();
        var graph = new MaterialGraph(Path.Combine(Root, "weathered-iron.texgraph"), "wear", 256);
        var model = carve.Store.Current.Model;
        var (texture, first) = bakes.Image(model, "strip", graph, TestContext.Current.CancellationToken);
        Assert.Equal(256, texture.Width);
        Assert.Contains("baked: [{\"material\":\"weathered-iron.texgraph\",\"mesh\":\"strip\",\"slot\":\"image\",\"size\":256,\"ms\":", GraphBakes.Text([first!]));
        Assert.Null(bakes.Image(model, "strip", graph, TestContext.Current.CancellationToken).Baked);

        var missing = Assert.Throws<GraphSlotProblemException>(() => bakes.Image(model, "strip", graph with { Output = "nothing" }, TestContext.Current.CancellationToken));
        Assert.Contains("nothing", missing.Message);
    }

    [Fact]
    [Trait("Tier", "regression")]
    public async Task The_worked_example_draws_through_the_process_and_names_the_output()
    {
        Files();
        var (process, _) = await StartProcessAsync(mesh: null);
        using var carve = process;
        Ok(await carve.CallToolAsync("define_mesh", BakeToolTests.Strip));
        Ok(await carve.CallToolAsync("set_uvs", BakeToolTests.StripUvs()));
        string Text(JsonElement r) { Assert.False(IsError(r), IsError(r) ? TextOf(r) : ""); return r.GetProperty("content")[1].GetProperty("text").GetString()!; }
        var first = Text(await carve.CallToolAsync("render", Example));
        Assert.Contains("image weathered-iron.texgraph wear 256 px", first);
        Assert.Contains("baked: [{\"material\":\"weathered-iron.texgraph\"", first);
        var second = Text(await carve.CallToolAsync("render", Example));
        Assert.Contains("image weathered-iron.texgraph wear 256 px", second);
        Assert.DoesNotContain("baked:", second);
        var gone = await carve.CallToolAsync("render", Example.Replace("\"wear\"", "\"nothing\""));
        Assert.True(IsError(gone));
        Assert.Contains("nothing", TextOf(gone));
    }
}

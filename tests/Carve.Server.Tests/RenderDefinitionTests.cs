using System.Numerics;
using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public class RenderDefinitionTests
{
    [Fact]
    public async Task The_tool_lists_its_views_and_says_what_missing_and_empty_mean()
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();

        var tools = await carve.ListToolsAsync();
        var render = tools.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "render");

        var description = render.GetProperty("description").GetString()!;
        Assert.Contains("front", description);
        Assert.Contains("back", description);
        Assert.Contains("MISSING", description);
        Assert.Contains("EMPTY", description);
        Assert.Contains("'labels'", description);
        Assert.Contains("Only what faces the camera is labelled", description);
        Assert.Contains("name an edge by its two vertex ids", description);
        Assert.Contains("a label may sit on a face hidden", description);
        Assert.Contains("'views'", description);
        Assert.Contains("'columns'", description);
        Assert.Contains("the six axis views, orthographic, at 256", description);
        Assert.Contains("'3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, 8 vertices, 6 faces'", description);
        Assert.Contains("fewer than 2 views or more than 6", description);
        Assert.Contains("columns other than 1, 2 or 3", description);
        Assert.Contains("views together with view, direction, zoom or target", description);

        var view = render.GetProperty("inputSchema").GetProperty("properties").GetProperty("view");
        Assert.Equal(["front", "back", "+x", "-x", "+y", "-y", "+z", "-z"], view.GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Contains("direction", description);
        Assert.Contains("zoom", description);
        Assert.Contains("target", description);
    }

    [Fact]
    public async Task The_description_says_what_the_new_arguments_do()
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "render")
            .GetProperty("description").GetString()!;

        foreach (var words in new[]
        {
            "'+x'", "'-x'", "'+y'", "'-y'", "'+z'", "'-z'", "'projection'", "keeps parallel edges parallel", "what a game shows",
            "reading dimensions", "'size'", "a sheet tile", "dense mesh", "not one of the eight names", "a size other than",
            "a projection other than", "'front' (the default) looks from",
        })
        {
            Assert.Contains(words, description);
        }
    }

    [Fact]
    public async Task The_tool_describes_its_looks()
    {
        using var carve = new ToolHarness();
        await carve.InitializeAsync();

        var tools = await carve.ListToolsAsync();
        var render = tools.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "render");

        var description = render.GetProperty("description").GetString()!;
        foreach (var part in new[] { "wireframe", "normals", "highlight", "hidden-line", "(normal + 1) / 2", "spike", "a mode other than", "a highlight that is not an object" })
        {
            Assert.Contains(part, description);
        }

        var schema = render.GetProperty("inputSchema").GetProperty("properties");
        Assert.Equal(["shaded", "wireframe", "normals", "checker", "uvs"], schema.GetProperty("mode").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("object", schema.GetProperty("highlight").GetProperty("type").GetString());
        Assert.Equal(["vertices", "edges", "faces"], schema.GetProperty("highlight").GetProperty("properties").EnumerateObject().Select(p => p.Name));
    }
}

using System.Numerics;
using System.Text.Json;
using StbImageSharp;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>The tool-definition checks moved out of the regression class of the same family (T-88): description, schema and worked-example checks on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class NamedMeshDefinitionTests() : TempFolderTests("carve-named")
{
    private const string Names = "'crate', 'lid'";

    /// <summary>The define_mesh, define_mesh, move calls of define_mesh's two-mesh worked example.</summary>
    private static (string Tool, string Arguments)[] TwoMeshExample(JsonElement tools)
    {
        var text = ToolList.Description(tools, "define_mesh");
        var at = text.IndexOf("Worked example (two meshes):", StringComparison.Ordinal);
        Assert.True(at >= 0, "define_mesh's description has no two-mesh worked example");
        return text[at..].Split('\n').Skip(1)
            .TakeWhile(line => line.Length > 2 && char.IsDigit(line[0]) && line[1] == '.')
            .Select(line => line.Split(' ', 3))
            .Select(p => (p[1], p[2].Split(" (", 2)[0]))
            .ToArray();
    }

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static string Refused(JsonElement result)
    {
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static float[] Point(JsonElement mesh, int vertex) =>
        [.. mesh.GetProperty("vertices")[vertex].GetProperty("at").EnumerateArray().Select(x => x.GetSingle())];

    [Fact]
    public async Task The_worked_example_moves_the_lid_alone_and_leaving_mesh_out_is_refused_with_the_names()
    {
        var (carve, tools) = await NamedMeshTests.StartWithCrateAndLidOnHarnessAsync();
        using var _ = carve;
        var move = TwoMeshExample(tools)[2].Arguments;
        var unnamed = move.Replace("\"mesh\":\"lid\",", "", StringComparison.Ordinal);

        Assert.Contains(Names, Refused(await carve.CallToolAsync("move", unnamed)));
        var moved = Ok(await carve.CallToolAsync("move", move));

        Assert.True(moved.GetProperty("closed").GetBoolean());
        Assert.Equal(0, moved.GetProperty("warnings").GetArrayLength());
        Assert.Equal(new float[] { -0.1f, 1.1f, -0.1f }, Point(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"lid"}""")), 0));
        Assert.Equal(new float[] { 0f, 0f, 0f }, Point(Ok(await carve.CallToolAsync("get_mesh", """{"mesh":"crate"}""")), 0));
    }

    [Fact]
    public async Task Every_tool_refuses_an_unknown_argument_and_lists_exactly_the_arguments_its_schema_has()
    {
        // Each tool keeps its own list of known arguments beside its schema; a name in one and not the other shows here.
        using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var tools = (await carve.ListToolsAsync()).EnumerateArray().ToList();
        Assert.True(tools.Count >= 18, $"{tools.Count} tools");
        foreach (var tool in tools)
        {
            var name = tool.GetProperty("name").GetString()!;
            var schema = tool.GetProperty("inputSchema");
            var properties = schema.TryGetProperty("properties", out var p) ? p.EnumerateObject().Select(x => x.Name).Order().ToList() : [];
            var result = await carve.CallToolAsync(name, """{"zz_unknown":1}""");
            Assert.True(IsError(result), name);
            var text = TextOf(result);
            var match = System.Text.RegularExpressions.Regex.Match(text,
                $"zz_unknown: not an argument of {name}(?:; its arguments are ([a-z_, ]+)|, which takes none)");
            Assert.True(match.Success, $"{name}: {text}");
            var listed = match.Groups[1].Success ? match.Groups[1].Value.Split(", ").Order().ToList() : [];
            Assert.True(properties.SequenceEqual(listed), $"{name}: schema has {string.Join(", ", properties)}; the refusal lists {string.Join(", ", listed)}");
        }
    }
}

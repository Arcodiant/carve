using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-68: <c>merge_faces</c> and <c>bridge</c> through the client, each worked example then <c>check</c>, and one undo step each.</summary>
[Trait("Tier", "regression")]
public sealed class MergeBridgeToolTests() : TempFolderTests("carve-merge-bridge")
{
    private const string DefineMarker = "define_mesh input: ";

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static void Refused(JsonElement result, string mention)
    {
        Assert.True(IsError(result), TextOf(result));
        Assert.True(TextOf(result).Contains(mention, StringComparison.Ordinal), TextOf(result));
    }

    private static int[] Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();

    private static string Input(JsonElement tools, string name) =>
        ToolList.Description(tools, name).Split('\n').Single(l => l.StartsWith(DefineMarker, StringComparison.Ordinal))[DefineMarker.Length..];

    private async Task<(CarveProcess Carve, JsonElement Tools)> StartAsync(string tool)
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        var tools = await carve.ListToolsAsync();
        Ok(await carve.CallToolAsync("define_mesh", Input(tools, tool)));
        return (carve, tools);
    }

    private static async Task CheckedClean(CarveProcess carve, bool closed, params string[] problems)
    {
        var check = Ok(await carve.CallToolAsync("check", "{}"));
        Assert.Equal(problems, check.GetProperty("problems").EnumerateArray()
            .Select(p => p.GetProperty("check").GetString() + " " + p.GetProperty("element").GetString()).ToArray());
        Assert.Equal(closed, check.GetProperty("stats").GetProperty("closed").GetBoolean());
    }

    [Fact]
    public async Task A_merge_of_another_colour_is_a_warning_naming_it()
    {
        var (carve, _) = await StartAsync("merge_faces");
        using var __ = carve;
        Ok(await carve.CallToolAsync("set_colour", """{"faces":[7],"colour":"#3C78C8"}"""));
        var body = Ok(await carve.CallToolAsync("merge_faces", """{"faces":[7,1]}"""));
        var warning = Assert.Single(body.GetProperty("warnings").EnumerateArray()).GetString()!;
        Assert.StartsWith("faces[7]: ", warning, StringComparison.Ordinal);
    }

    /// <summary>The README's <c>Cutting</c> example for <paramref name="tool"/>: the last inline JSON on its bullet.</summary>
    private static string ReadmeExample(string tool)
    {
        var lines = File.ReadAllLines(Path.Combine(Paths.RepoRoot, "README.md"));
        var section = lines.SkipWhile(l => l != "## Cutting").Skip(1).TakeWhile(l => !l.StartsWith("## ", StringComparison.Ordinal));
        var bullet = section.Single(l => l.StartsWith($"- `{tool}` ", StringComparison.Ordinal));
        var end = bullet.LastIndexOf("}`", StringComparison.Ordinal);
        var start = bullet.LastIndexOf("`{", end, StringComparison.Ordinal);
        return bullet[(start + 1)..(end + 1)];
    }

    /// <summary>define_mesh's unit cube, from its description's worked example line.</summary>
    private static string CubeInput(JsonElement tools)
    {
        const string marker = "Worked example (a unit cube): ";
        var description = ToolList.Description(tools, "define_mesh");
        return description[(description.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..].Split('\n')[0];
    }
}

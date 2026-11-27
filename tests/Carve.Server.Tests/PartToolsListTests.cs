extern alias carve;
using System.Text.Json;

namespace Carve.Server.Tests;

/// <summary>
/// T-93: where each agreed sentence of the part tools is. The SHA-256 digest of tools/list beside it was retired by the
/// person (2026-11-24, M-23) and deleted by T-197 (Opus 5.5).
/// </summary>
[Trait("Tier", "unit")]
public class PartToolsListTests
{
    /// <summary>Pins where each agreed sentence is, once each.</summary>
    [Fact]
    public async Task Each_agreed_sentence_is_in_its_tools_description_once()
    {
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var texts = (await carve.ListToolsAsync()).EnumerateArray()
            .ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("description").GetString()!);
        string[] partSpace = ["bridge", "define_target", "delete", "extrude", "knife", "loop_cut", "merge_faces", "move", "plane_cut", "project_uvs", "select", "subdivide", "displace", "tessellate"];
        string[] partTarget = ["define_group", "delete_target", "get_weights", "mark_edges", "set_colour", "set_material", "set_shading", "set_uvs", "set_weights", "unwrap", "weigh"];
        foreach (var (name, text) in texts)
        {
            Assert.True(Count(text, carve::Carve.Server.PartTexts.PartSpace) == (partSpace.Contains(name) ? 1 : 0), $"{name}: PartSpace sentence count");
            Assert.True(Count(text, carve::Carve.Server.PartTexts.PartTarget) == (partTarget.Contains(name) ? 1 : 0), $"{name}: PartTarget sentence count");
            Assert.True(Count(text, carve::Carve.Server.PartTexts.DefineMesh) == (name == "define_mesh" ? 1 : 0), $"{name}: DefineMesh sentence count");
            Assert.True(Count(name == "check" ? HelpToolTests.WithReference(text, name) : text, carve::Carve.Server.PartTexts.Check) == (name == "check" ? 1 : 0), $"{name}: Check sentence count");
        }
    }

    /// <summary>RV-71: pins render's and get_mesh's T-94 paragraphs, once each.</summary>
    [Fact]
    public async Task Each_T94_paragraph_is_in_its_tools_description_once()
    {
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        foreach (var tool in (await carve.ListToolsAsync()).EnumerateArray())
        {
            var (name, text) = (tool.GetProperty("name").GetString()!, tool.GetProperty("description").GetString()!);
            // T-207: render's paragraph moved to its help entry, so it is in no description and once in that entry.
            Assert.True(Count(text, carve::Carve.Server.RenderPartTexts.Parts) == 0, $"{name}: render's part and level paragraph count");
            Assert.True(Count(text, carve::Carve.Server.GetMeshTool.PlacementText) == (name == "get_mesh" ? 1 : 0), $"{name}: get_mesh's placement paragraph count");
        }
    }

    [Fact]
    public void Renders_part_and_level_paragraph_is_in_its_help_entry_once()
    {
        Assert.Equal(1, Count(carve::Carve.Server.HelpTool.Entry("render")!, carve::Carve.Server.RenderPartTexts.Parts));
    }

    private static int Count(string text, string sentence)
    {
        var n = 0;
        for (var at = text.IndexOf(sentence, StringComparison.Ordinal); at >= 0; at = text.IndexOf(sentence, at + sentence.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}

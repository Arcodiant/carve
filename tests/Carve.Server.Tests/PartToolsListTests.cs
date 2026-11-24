extern alias carve;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carve.Server.Tests;

/// <summary>
/// T-93: tools/list is unchanged for every tool that was there before M-9's part tools, apart from the agreed sentences.
/// The digest is of every other tool's whole entry (name, description, schema) with the agreed sentences taken out, taken
/// on the code before T-93: origin/milestone/M-9 at 6e2626f (Merge task/T-92), whose save and open texts T-92 changed;
/// taken again for T-132 (Opus 5.5), whose save and open texts name version 9 and drop T-92's refusal of a part tree beside targets.
/// Taken again for T-95 (Opus 5.5), whose export text says how a part tree becomes nodes: on T-95 with ExportTool.cs as at 431c75c
/// (the milestone tip), the digest was the one before (EBD17198...), and export's description was the only field of tools/list that changed.
/// Taken again for RV-71 (Opus 5.5), whose project_uvs text says planar sees the mesh's own axes and whose export text says a piece of an
/// instance or an empty is refused as save's is: with those two changes stripped the digest was the one before (DCE3D7BA...).
/// T-124 (Opus 5.5) adds render_glb's sentence on texture sampling (mip maps, as render's); it is taken out with T-94's additions.
/// Taken again for T-160 (Opus 5.5), whose define_material text and schema take graph slots and whose get_mesh, save and open
/// texts name them; the diff of src/Carve.Server against origin/milestone/M-29 changes no other tool's text or schema (was 1697322B...).
/// Taken again for T-164 (Sonnet 5.5), whose export text gains the paragraph on graph materials and whose define_material sentence
/// says export writes them; only those two descriptions moved it (was D5334566...).
/// </summary>
[Trait("Tier", "unit")]
public class PartToolsListTests
{
    /// <summary>SHA-256 of the existing tools' entries before T-93, in registration order, as <see cref="Digest"/> takes it.</summary>
    // T-207 retakes the digest as each of its agents changes descriptions (render by agent A; the last retake is the one that counts):
    // render, and the new help tool, are the descriptions it has changed so far.
    private const string Before = "B7B7554D5F6F2DB66633316AEE142ABABFC494BD55734F3757B6F31123872646";

    private static readonly string[] NewTools = ["define_part", "get_parts", "keep_level", "bake", "displace", "help"];

    /// <summary>The sentences T-93 adds to existing descriptions, each removed before the digest.</summary>
    private static readonly string[] Agreed = [carve::Carve.Server.PartTexts.PartSpace, carve::Carve.Server.PartTexts.DefineMesh, carve::Carve.Server.PartTexts.Check,
        carve::Carve.Server.PartTexts.PartTarget];

    private static readonly string[] T94Added = [carve::Carve.Server.RenderPartTexts.Parts, carve::Carve.Server.GetMeshTool.PlacementText, carve::Carve.Server.RenderPartTexts.Image,
        carve::Carve.Server.RenderGlbTexts.Sampling];

    private static readonly (string Now, string Was)[] T94Reworded =
    [
        ("in model space, every part where it is placed, and inside the bounds of what is drawn", "in the mesh's own coordinates and inside its bounds"),
        ("in model space's axes, any length", "in the mesh's own axes, any length"),
        ("[x, y, z] in model space, inside the bounds of what is drawn: the point to look at.", "[x, y, z] in the mesh's coordinates, inside its bounds: the point to look at."),
    ];

    internal static string Digest(JsonElement tools)
    {
        var kept = new JsonArray();
        foreach (var tool in tools.EnumerateArray())
        {
            if (NewTools.Contains(tool.GetProperty("name").GetString())) continue;
            var node = JsonNode.Parse(tool.GetRawText())!.AsObject();
            var text = node["description"]!.GetValue<string>();
            foreach (var sentence in Agreed) text = text.Replace(sentence, "", StringComparison.Ordinal);
            // T-94's agreed changes to render and get_mesh, taken out again: its paragraphs, its new arguments, and the
            // texts that said "the mesh's own coordinates" put back as they were.
            foreach (var sentence in T94Added) text = text.Replace(sentence, "", StringComparison.Ordinal);
            foreach (var (now, was) in T94Reworded) text = text.Replace(now, was, StringComparison.Ordinal);
            node["description"] = text;
            if (node["inputSchema"]?["properties"] is JsonObject properties)
            {
                var name = tool.GetProperty("name").GetString();
                foreach (var added in name == "render" ? ["part", "level", "image"] : name == "get_mesh" ? new[] { "level" } : []) properties.Remove(added);
                if (name == "render" && properties["target"] is JsonObject targetArgument)
                    targetArgument["description"] = T94Reworded.Aggregate(targetArgument["description"]!.GetValue<string>(), (t, r) => t.Replace(r.Now, r.Was, StringComparison.Ordinal));
            }
            kept.Add(node);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kept.ToJsonString())));
    }

    [Fact]
    public async Task Existing_tools_are_unchanged_apart_from_the_agreed_sentences()
    {
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var digest = Digest(await carve.ListToolsAsync());
        Assert.True(digest == Before, $"tools/list digest {digest}");
    }

    /// <summary>The digest takes the agreed sentences out, so it cannot see one go missing; this pins where each one is.</summary>
    [Fact]
    public async Task Each_agreed_sentence_is_in_its_tools_description_once()
    {
        await using var carve = new ToolHarness();
        await carve.InitializeAsync();
        var texts = (await carve.ListToolsAsync()).EnumerateArray()
            .ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("description").GetString()!);
        string[] partSpace = ["bridge", "define_target", "delete", "extrude", "knife", "loop_cut", "merge_faces", "move", "plane_cut", "project_uvs", "select", "subdivide", "displace"];
        string[] partTarget = ["define_group", "delete_target", "get_weights", "mark_edges", "set_colour", "set_material", "set_shading", "set_uvs", "set_weights", "unwrap", "weigh"];
        foreach (var (name, text) in texts)
        {
            Assert.True(Count(text, carve::Carve.Server.PartTexts.PartSpace) == (partSpace.Contains(name) ? 1 : 0), $"{name}: PartSpace sentence count");
            Assert.True(Count(text, carve::Carve.Server.PartTexts.PartTarget) == (partTarget.Contains(name) ? 1 : 0), $"{name}: PartTarget sentence count");
            Assert.True(Count(text, carve::Carve.Server.PartTexts.DefineMesh) == (name == "define_mesh" ? 1 : 0), $"{name}: DefineMesh sentence count");
            Assert.True(Count(name == "check" ? HelpToolTests.WithReference(text, name) : text, carve::Carve.Server.PartTexts.Check) == (name == "check" ? 1 : 0), $"{name}: Check sentence count");
        }
    }

    /// <summary>RV-71: T-94's paragraphs are taken out before the digest too; this pins render's and get_mesh's, once each.</summary>
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

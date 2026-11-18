extern alias carve;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carve.Server.Tests;

/// <summary>
/// T-93: tools/list is unchanged for every tool that was there before M-9's part tools, apart from the agreed sentences.
/// The digest is of every other tool's whole entry (name, description, schema) with the agreed sentences taken out, taken
/// on the code before T-93 (commit 98beeca, the same product code as a2b8878).
/// </summary>
[Trait("Tier", "unit")]
public class PartToolsListTests
{
    /// <summary>SHA-256 of the existing tools' entries before T-93, in registration order, as <see cref="Digest"/> takes it.</summary>
    private const string Before = "EF74CB6CD79C71BEC5C5C84F22B620E3AE2DC523E607248109E6C7DA9F31D0C7";

    private static readonly string[] NewTools = ["define_part", "get_parts", "keep_level"];

    /// <summary>The sentences T-93 adds to existing descriptions, each removed before the digest.</summary>
    private static readonly string[] Agreed = [carve::Carve.Server.PartTexts.PartSpace, carve::Carve.Server.PartTexts.DefineMesh, carve::Carve.Server.PartTexts.Check];

    internal static string Digest(JsonElement tools)
    {
        var kept = new JsonArray();
        foreach (var tool in tools.EnumerateArray())
        {
            if (NewTools.Contains(tool.GetProperty("name").GetString())) continue;
            var node = JsonNode.Parse(tool.GetRawText())!.AsObject();
            var text = node["description"]!.GetValue<string>();
            foreach (var sentence in Agreed) text = text.Replace(sentence, "", StringComparison.Ordinal);
            node["description"] = text;
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
}

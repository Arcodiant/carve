using System.Text.Json;
using Xunit;

namespace Patina.Server.Tests;

/// <summary>
/// T-143: reads a round folder of the node-library acceptance run (PATINA_ACCEPTANCE_RUN names it, for example
/// C:\Users\andre\artifacts\carve\M-28\T-143\round-1) and holds each session's saved graph to the pass line. The folder's
/// round.json names each session's graph, export folders and the parameters its variant was exported with. Skipped unless
/// PATINA_ACCEPTANCE_RUN is set.
/// </summary>
[Trait("Tier", "unit")]
public class AcceptanceRoundTests
{
    private static string Folder = "";

    private static JsonElement Round()
    {
        var folder = Environment.GetEnvironmentVariable("PATINA_ACCEPTANCE_RUN");
        if (string.IsNullOrWhiteSpace(folder)) Assert.Skip("PATINA_ACCEPTANCE_RUN names no round folder");
        Folder = folder!;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(folder!, "round.json"))).RootElement.Clone();
    }

    private static string In(string session, string file) => Path.Combine(Folder, session, file.Replace('/', Path.DirectorySeparatorChar));

    private static JsonElement Graph(string session, string file) =>
        JsonDocument.Parse(File.ReadAllText(In(session, file))).RootElement.Clone();

    private static async Task<string> OpenedCleanAsync(PatinaHarness patina, string session, string graph)
    {
        var opened = await patina.Ok("open", JsonSerializer.Serialize(new { path = In(session, graph) }));
        Assert.Contains("no faults: it evaluates", opened);
        var check = await patina.Ok("check", "{}");
        Assert.True(JsonDocument.Parse(check).RootElement.GetProperty("ok").GetBoolean(), $"{session}: check: {check}");
        return await patina.Ok("get_graph", "{}");
    }

    private static async Task ExportEqualsAsync(PatinaHarness patina, string session, string folder, string? parameters)
    {
        using var temp = new TempFolder();
        var path = JsonSerializer.Serialize(temp.Path);
        await patina.Ok("export", parameters is null
            ? "{\"folder\":" + path + ",\"size\":512}"
            : "{\"folder\":" + path + ",\"size\":512,\"parameters\":" + parameters + "}");
        var kept = Directory.GetFiles(In(session, folder), "*.png").Select(f => Path.GetFileName(f)).Order().ToArray();
        Assert.Equal(kept, Directory.GetFiles(temp.Path, "*.png").Select(f => Path.GetFileName(f)).Order().ToArray());
        foreach (var name in kept)
            Assert.True(File.ReadAllBytes(Path.Combine(temp.Path, name)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(In(session, folder), name))),
                $"{session}: {folder}/{name} differs from a fresh evaluation");
    }

    [Theory]
    [InlineData("s1-rug")]
    [InlineData("s2-iron")]
    [InlineData("s3-path")]
    public async Task A_material_checks_clean_has_a_parameter_with_a_variant_and_exports_as_a_fresh_evaluation(string session)
    {
        var entry = Round().GetProperty(session);
        await using var patina = await PatinaHarness.StartAsync(Path.Combine(Folder, session));
        var text = await OpenedCleanAsync(patina, session, entry.GetProperty("graph").GetString()!);

        Assert.Contains("parameters (", text);
        var plain = entry.GetProperty("export").GetString()!;
        var variant = entry.GetProperty("variant").GetString()!;
        var parameters = entry.GetProperty("parameters").GetRawText();
        await ExportEqualsAsync(patina, session, plain, null);
        await ExportEqualsAsync(patina, session, variant, parameters);

        // The variant is a different material: some image of it differs from the default's.
        Assert.Contains(Directory.GetFiles(In(session, plain), "*.png"), file =>
            !File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(In(session, variant), Path.GetFileName(file)))));
        // check is clean on the variant too.
        var checkVariant = await patina.Ok("check", "{\"parameters\":" + parameters + "}");
        Assert.True(JsonDocument.Parse(checkVariant).RootElement.GetProperty("ok").GetBoolean(), checkVariant);
    }

    [Fact]
    public void The_iron_has_a_metallic_output()
    {
        var entry = Round().GetProperty("s2-iron");
        var nodes = Graph("s2-iron", entry.GetProperty("graph").GetString()!).GetProperty("nodes");
        Assert.Contains(nodes.EnumerateObject(), n =>
            n.Value.GetProperty("type").GetString() == "output" && n.Value.TryGetProperty("params", out var p)
            && p.TryGetProperty("role", out var role) && role.GetString() == "metallic");
    }

    [Fact]
    public void The_path_uses_the_moss_graph_through_a_graph_node_with_an_input()
    {
        var entry = Round().GetProperty("s3-path");
        var nodes = Graph("s3-path", entry.GetProperty("graph").GetString()!).GetProperty("nodes");
        var uses = nodes.EnumerateObject().Where(n => n.Value.GetProperty("type").GetString() == "graph").ToList();
        var moss = Assert.Single(uses, n => n.Value.GetProperty("params").GetProperty("path").GetString()!.StartsWith("moss", StringComparison.Ordinal));
        Assert.True(moss.Value.TryGetProperty("inputs", out var inputs) && inputs.EnumerateObject().Any(), "the moss graph node has no input connected");
        var mossNodes = Graph("s3-path", "moss.texgraph").GetProperty("nodes");
        Assert.Contains(mossNodes.EnumerateObject(), n => n.Value.GetProperty("type").GetString() == "input");
    }

    [Fact]
    public async Task The_fixed_graph_checks_clean_and_keeps_the_planted_ids()
    {
        var entry = Round().GetProperty("s4-fix");
        var fixedFile = entry.GetProperty("graph").GetString()!;
        var draftFile = entry.GetProperty("draft").GetString()!;
        await using var patina = await PatinaHarness.StartAsync(Path.Combine(Folder, "s4-fix"));
        await OpenedCleanAsync(patina, "s4-fix", fixedFile);

        var draft = Graph("s4-fix", draftFile).GetProperty("nodes").EnumerateObject().Select(n => n.Name).ToHashSet();
        var fixedIds = Graph("s4-fix", fixedFile).GetProperty("nodes").EnumerateObject().Select(n => n.Name).ToHashSet();
        Assert.Equal(14, draft.Count);
        // Each of the three faults is cured by changing a node's parameter, so every planted id is kept.
        Assert.True(draft.IsSubsetOf(fixedIds), "missing planted ids: " + string.Join(", ", draft.Except(fixedIds)));

        // The draft is the plant, untouched: it still has its three faults.
        await patina.Ok("open", JsonSerializer.Serialize(new { path = In("s4-fix", draftFile) }));
        var problems = JsonDocument.Parse(await patina.Ok("check", "{}")).RootElement.GetProperty("problems")
            .EnumerateArray().Select(p => p.GetProperty("check").GetString()).Order().ToArray();
        Assert.Equal(new[] { "base-colour-range", "does-not-tile", "one-value" }, problems);
    }
}

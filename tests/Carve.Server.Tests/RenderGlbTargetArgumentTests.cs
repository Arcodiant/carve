using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>
/// T-108's <c>render_glb</c> <c>targets</c> refusals, on the in-process harness (RV-64): the refusals that need no file, worded as
/// <c>render</c> and <c>check</c> word them, and the order the file's names and the weights are told in, on the engine's CPU parser.
/// What needs the engine to draw stays in <see cref="RenderGlbTargetsTests"/>.
/// </summary>
[Trait("Tier", "unit")]
public sealed class RenderGlbTargetArgumentTests() : TempFolderTests("carve-render-glb-target-argument")
{
    private const string Cube = """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[2,3,7,6],[0,4,7,3],[1,2,6,5]]}""";

    private async Task<ToolHarness> StartWithBulgeAsync()
    {
        var carve = new ToolHarness(workingDirectory: Root);
        try
        {
            await carve.InitializeAsync();
            Assert.False(IsError(await carve.CallToolAsync("define_mesh", Cube)), "define_mesh");
            Assert.False(IsError(await carve.CallToolAsync("define_target", """{"name":"Bulge","positions":{"2":[1,1.5,0],"3":[0,1.5,0],"6":[1,1.5,1],"7":[0,1.5,1]}}""")), "define_target");
            return carve;
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    [Theory]
    [InlineData("""{"Bulge":1.5}""", "targets.Bulge: weight 1.5 is outside 0 to 1")]
    [InlineData("""{"Bulge":-0.25}""", "targets.Bulge: weight -0.25 is outside 0 to 1")]
    [InlineData("""{"Bulge":"half"}""", "targets.Bulge: expected a number")]
    [InlineData("""{"Bulge":1e999}""", "targets.Bulge: the weight is not a finite number")]
    [InlineData("""{"Bulge":1e39}""", "targets.Bulge: the weight is not a finite number")]
    [InlineData("""[0.5]""", "targets: expected an object of target name to weight")]
    public async Task Render_glb_refuses_a_weight_or_a_type_as_check_does_naming_the_target(string targets, string expected)
    {
        using var carve = await StartWithBulgeAsync();
        var glb = await carve.CallToolAsync("render_glb", $$"""{"path":"nowhere.glb","targets":{{targets}}}""");
        Assert.True(IsError(glb), TextOf(glb));
        Assert.Contains(expected, TextOf(glb));

        // The same words from check, which reads the same argument.
        var check = await carve.CallToolAsync("check", $$"""{"targets":{{targets}}}""");
        Assert.True(IsError(check), TextOf(check));
        Assert.Contains(expected, TextOf(check));
    }

    [Fact]
    public async Task Render_glb_tells_every_bad_entry_in_one_call_as_render_does_and_before_a_path_error()
    {
        using var carve = await StartWithBulgeAsync();
        var glb = TextOf(await carve.CallToolAsync("render_glb", """{"path":"nowhere.glb","targets":{"Bulge":"half","Other":2,"Third":1e999}}"""));
        Assert.Contains("targets.Bulge: expected a number", glb);
        Assert.Contains("targets.Other: weight 2 is outside 0 to 1", glb);
        Assert.Contains("targets.Third: the weight is not a finite number", glb);
        Assert.DoesNotContain("no file at", glb);
    }

    [Fact]
    public async Task A_refused_weight_is_never_prepared_as_a_number_even_without_its_words()
    {
        var errors = new List<string>();
        var glb = await PrepareAsync(errors, [new("Bulge", float.NaN)], new Dictionary<string, string>());
        Assert.Null(glb);
        Assert.Equal("targets.Bulge: the weight is not a finite number", Assert.Single(errors));
    }

    [Fact]
    public async Task Render_glb_with_a_good_weight_goes_on_to_the_engine()
    {
        using var carve = await StartWithBulgeAsync();
        var result = await carve.CallToolAsync("render_glb", """{"path":"nowhere.glb","targets":{"Bulge":1}}""");
        Assert.True(IsError(result));
        Assert.Equal(ToolHarness.NoEngine, TextOf(result));
    }

    /// <summary>The cube with its Bulge exported by the tool, read by the engine's CPU parser, as <c>render_glb</c> reads a file.</summary>
    private async Task<GlbScene?> PrepareAsync(List<string> errors, IReadOnlyList<KeyValuePair<string, float>> targets, IReadOnlyDictionary<string, string> refusals)
    {
        using var carve = await StartWithBulgeAsync();
        var file = Path.Combine(Root, "bulge.glb");
        Assert.False(IsError(await carve.CallToolAsync("export", $$"""{"path":"bulge.glb"}""")), "export");
        return GlbScene.Prepare(GlbExportTests.Parser, await File.ReadAllBytesAsync(file), null, 0, errors, targets, refusals);
    }

    [Fact]
    public async Task An_unknown_name_is_told_before_its_weight_and_a_known_name_s_weight_after_its_name()
    {
        var errors = new List<string>();
        var glb = await PrepareAsync(errors,
            [new("Nope", 0), new("Bulge", 0)],
            new Dictionary<string, string> { ["Nope"] = "targets.Nope: weight 2 is outside 0 to 1", ["Bulge"] = "targets.Bulge: the weight is not a finite number" });

        Assert.Null(glb);
        Assert.Equal(2, errors.Count);
        Assert.Equal("targets.Nope: the file has no target named 'Nope'; the targets are 'Bulge'", errors[0]);
        Assert.Equal("targets.Bulge: the weight is not a finite number", errors[1]);
    }

    [Fact]
    public async Task A_name_the_file_has_with_a_good_weight_is_prepared()
    {
        var errors = new List<string>();
        var glb = await PrepareAsync(errors, [new("Bulge", 0.5f)], new Dictionary<string, string>());
        Assert.NotNull(glb);
        Assert.Empty(errors);
        Assert.Equal(0.5f, Assert.Single(glb!.TargetWeights).Value);
    }
}

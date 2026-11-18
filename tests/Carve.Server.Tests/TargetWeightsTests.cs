using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>T-103: <c>check</c>'s three target rules and the <c>targets</c> argument's refusals, on the in-process harness.</summary>
[Trait("Tier", "unit")]
public sealed class TargetWeightsTests() : TempFolderTests("carve-target-weights")
{
    private const string Cube = """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[2,3,7,6],[0,4,7,3],[1,2,6,5]]}""";

    // Two quads side by side whose shared edge is two pairs of coincident vertices.
    private const string Seam = """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[1,0,0],[2,0,0],[2,1,0],[1,1,0]],"faces":[[0,1,2,3],[4,5,6,7]]}""";

    private async Task<ToolHarness> StartAsync(string mesh)
    {
        var carve = new ToolHarness(workingDirectory: Root);
        try
        {
            await carve.InitializeAsync();
            Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh)), "define_mesh");
            return carve;
        }
        catch
        {
            await carve.DisposeAsync();
            throw;
        }
    }

    private static async Task Target(ToolHarness carve, string name, string positions) =>
        Assert.False(IsError(await carve.CallToolAsync("define_target", $$"""{"name":"{{name}}","positions":{{positions}}}""")), name);

    private static JsonElement Body(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static string[] Problems(JsonElement body, string check) =>
        body.GetProperty("problems").EnumerateArray().Where(p => p.GetProperty("check").GetString() == check)
            .Select(p => p.GetProperty("element").GetString() + " " + p.GetProperty("message").GetString()).ToArray();

    private const string Inside = """{"2":[1,-0.5,0],"3":[0,-0.5,0],"6":[1,-0.5,1],"7":[0,-0.5,1]}""";
    private const string Tall = """{"2":[1,1.5,0],"3":[0,1.5,0],"6":[1,1.5,1],"7":[0,1.5,1]}""";

    [Fact]
    public async Task A_target_that_turns_faces_over_is_reported_by_name_and_id_and_a_good_one_is_not()
    {
        using var carve = await StartAsync(Cube);
        await Target(carve, "Inside", Inside);
        await Target(carve, "Tall", Tall);

        var body = Body(await carve.CallToolAsync("check", "{}"));
        var flips = Problems(body, "target-flips-face");
        Assert.Single(flips);
        Assert.StartsWith("targets['Inside'] ", flips[0]);
        // The four sides (faces 0, 1, 4 and 5) turn; the top and bottom are moved but keep their sides.
        Assert.Contains("4 faces turn against the rest normal: faces[0], faces[1], faces[4], faces[5];", flips[0]);
        Assert.DoesNotContain("Tall", string.Join(' ', body.GetProperty("problems").EnumerateArray().Select(p => p.GetProperty("element").GetString())));
        Assert.False(body.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task A_target_that_flattens_faces_collapses_them_and_does_not_also_flip_them()
    {
        using var carve = await StartAsync(Cube);
        await Target(carve, "Flat", """{"2":[1,0,0],"3":[0,0,0],"6":[1,0,1],"7":[0,0,1]}""");

        var body = Body(await carve.CallToolAsync("check", "{}"));
        var collapsed = Problems(body, "target-collapses-face");
        Assert.Single(collapsed);
        Assert.StartsWith("targets['Flat'] ", collapsed[0]);
        Assert.Contains("4 faces", collapsed[0]);
        Assert.Contains("faces[0], faces[1], faces[4], faces[5]", collapsed[0]);
        Assert.Empty(Problems(body, "target-flips-face"));
    }

    [Fact]
    public async Task A_target_that_moves_one_of_two_coincident_vertices_opens_the_mesh_and_names_both()
    {
        using var carve = await StartAsync(Seam);
        await Target(carve, "Crack", """{"4":[1.2,0,0],"7":[1.2,1,0]}""");

        var body = Body(await carve.CallToolAsync("check", "{}"));
        var opens = Problems(body, "target-opens-mesh");
        Assert.Single(opens);
        Assert.StartsWith("targets['Crack'] ", opens[0]);
        Assert.Contains("2 vertices", opens[0]);
        Assert.Contains("vertices[4] from vertices[1]", opens[0]);
        Assert.Contains("vertices[7] from vertices[2]", opens[0]);
        Assert.Empty(Problems(body, "target-flips-face"));

        // Moving both halves together keeps the seam shut.
        await Target(carve, "Crack", """{"1":[1.2,0,0],"2":[1.2,1,0],"4":[1.2,0,0],"7":[1.2,1,0]}""");
        Assert.Empty(Problems(Body(await carve.CallToolAsync("check", "{}")), "target-opens-mesh"));
    }

    [Fact]
    public async Task Ids_past_ten_are_a_count_and_the_lowest_ten_are_named()
    {
        var vertices = new List<string>();
        var faces = new List<string>();
        var moved = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            vertices.AddRange([$"[{i},0,0]", $"[{i}.5,0,0]", $"[{i},1,0]"]);
            faces.Add($"[{3 * i},{3 * i + 1},{3 * i + 2}]");
            moved.Add($"\"{3 * i + 2}\":[{i},-1,0]");
        }
        using var carve = await StartAsync($$"""{"vertices":[{{string.Join(',', vertices)}}],"faces":[{{string.Join(',', faces)}}]}""");
        await Target(carve, "Under", "{" + string.Join(',', moved) + "}");

        var flips = Problems(Body(await carve.CallToolAsync("check", "{}")), "target-flips-face").Single();
        Assert.Contains("12 faces", flips);
        Assert.Contains("faces[9]", flips);
        Assert.DoesNotContain("faces[10]", flips);
        Assert.Contains("and 2 more", flips);
    }

    [Fact]
    public async Task Check_at_weights_measures_the_morphed_mesh_and_still_compares_weight_1_with_rest()
    {
        using var carve = await StartAsync(Cube);
        await Target(carve, "Tall", Tall);
        await Target(carve, "Inside", Inside);

        var rest = Body(await carve.CallToolAsync("check", "{}")).GetProperty("stats");
        var half = Body(await carve.CallToolAsync("check", """{"targets":{"Tall":0.5}}"""));
        Assert.Equal(1.25f, half.GetProperty("stats").GetProperty("size")[1].GetSingle(), 1e-6f);
        Assert.Equal(1f, rest.GetProperty("size")[1].GetSingle());
        Assert.Single(Problems(half, "target-flips-face"));

        // Weight 0 is the rest mesh.
        var zero = Body(await carve.CallToolAsync("check", """{"targets":{"Tall":0}}"""));
        Assert.Equal(rest.GetRawText(), zero.GetProperty("stats").GetRawText());
    }

    [Fact]
    public async Task Check_at_a_weight_still_judges_a_target_against_the_rest_mesh_not_the_mesh_at_that_weight()
    {
        using var carve = await StartAsync(Cube);
        await Target(carve, "Inside", Inside);

        // Inside at weight 1 lowers the top to y 0.5, so the sides are still faces; judged from that mesh, weight 1 would take the top to y 0 and
        // collapse them. Judged from rest, as it must be, the same four sides turn over and none collapses.
        var body = Body(await carve.CallToolAsync("check", """{"targets":{"Inside":1}}"""));
        var flips = Problems(body, "target-flips-face");
        Assert.Single(flips);
        Assert.Contains("4 faces turn against the rest normal: faces[0], faces[1], faces[4], faces[5];", flips[0]);
        Assert.Empty(Problems(body, "target-collapses-face"));
    }

    [Fact]
    public async Task A_face_with_no_area_at_rest_is_not_reported_as_a_target_collapsing_it()
    {
        // Face 0 is a quad; face 1 a triangle whose corners lie on a line, so it has no area at rest and none at weight 1.
        // define_mesh and move refuse a face with no area, so the triangle is drawn with some and its corner then set onto its line in the store.
        using var carve = await StartAsync("""{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[5,0,0],[6,0,0],[7,1,0]],"faces":[[0,1,2,3],[4,5,6]]}""");
        var drawn = carve.Store.Current.Mesh!;
        carve.Store.Replace(drawn.WithVertices([.. drawn.Vertices.Take(6), new System.Numerics.Vector3(7, 0, 0)]));
        await Target(carve, "Flat", """{"2":[1,0,0],"3":[0,0,0]}""");

        var collapsed = Problems(Body(await carve.CallToolAsync("check", "{}")), "target-collapses-face");
        Assert.Single(collapsed);
        Assert.Contains("1 face has no area but had some at rest: faces[0];", collapsed[0]);
        Assert.DoesNotContain("faces[1]", collapsed[0]);
    }

    [Fact]
    public async Task A_mesh_with_no_targets_checks_as_it_always_did()
    {
        using var carve = await StartAsync(Cube);
        var body = Body(await carve.CallToolAsync("check", "{}"));
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Empty(body.GetProperty("problems").EnumerateArray());
    }

    [Theory]
    [InlineData("""{"targets":{"Nope":1}}""", "targets.Nope: no mesh has a target named 'Nope'; the targets are 'Tall'")]
    [InlineData("""{"targets":{"Tall":1.5}}""", "targets.Tall: weight 1.5 is outside 0 to 1")]
    [InlineData("""{"targets":{"Tall":-0.25}}""", "targets.Tall: weight -0.25 is outside 0 to 1")]
    [InlineData("""{"targets":{"Tall":"half"}}""", "targets.Tall: expected a number")]
    [InlineData("""{"targets":{"Tall":1e999}}""", "targets.Tall: the weight is not a finite number")]
    [InlineData("""{"targets":[1]}""", "targets: expected an object of target name to weight")]
    public async Task Check_and_render_refuse_unknown_names_and_weights_outside_0_to_1(string arguments, string expected)
    {
        using var carve = await StartAsync(Cube);
        await Target(carve, "Tall", """{"2":[1,1.5,0]}""");
        foreach (var tool in new[] { "check", "render" })
        {
            var result = await carve.CallToolAsync(tool, arguments);
            Assert.True(IsError(result), tool + ": " + TextOf(result));
            Assert.Contains(expected, TextOf(result));
        }
    }

    [Fact]
    public async Task A_model_with_no_targets_says_so_and_the_uvs_look_refuses_targets()
    {
        using var carve = await StartAsync(Cube);
        var none = await carve.CallToolAsync("render", """{"targets":{"Tall":1}}""");
        Assert.True(IsError(none));
        Assert.Contains("the model has no targets", TextOf(none));

        var uvs = await carve.CallToolAsync("render", """{"mode":"uvs","targets":{"Tall":1}}""");
        Assert.True(IsError(uvs));
        Assert.Contains("targets", TextOf(uvs));
    }
}

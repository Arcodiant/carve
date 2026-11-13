using System.Text.Json;
using static Carve.Server.Tests.CarveProcess;

namespace Carve.Server.Tests;

/// <summary>project_uvs through the real tools (T-55).</summary>
[Trait("Tier", "regression")]
public sealed class ProjectUvsToolTests
{
    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private const string Holed =
        """{"vertices":[[0,0,0],[4,0,0],[4,4,0],[0,4,0],[1,1,0],[1,3,0],[3,3,0],[3,1,0]],"faces":[{"loop":[0,3,2,1],"holes":[[4,7,6,5]]}]}""";

    private const string SetFace0 = """{"faces":[{"face":0,"uv":[[0,0],[0,1],[1,1],[1,0]]}]}""";

    private static JsonElement Ok(JsonElement result)
    {
        Assert.False(IsError(result), TextOf(result));
        return JsonDocument.Parse(TextOf(result)).RootElement;
    }

    private static async Task<CarveProcess> StartAsync(string mesh = Cube)
    {
        var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", mesh));
        return carve;
    }

    private static async Task<string> Refused(CarveProcess carve, string arguments)
    {
        var result = await carve.CallToolAsync("project_uvs", arguments);
        Assert.True(IsError(result), TextOf(result));
        return TextOf(result);
    }

    private static async Task<JsonElement> Mesh(CarveProcess carve) => Ok(await carve.CallToolAsync("get_mesh", "{}"));

    private static float[] Floats(JsonElement list) => [.. list.EnumerateArray().Select(x => x.GetSingle())];

    private static float[][] UvOf(JsonElement mesh, int face) =>
        [.. mesh.GetProperty("uvs").EnumerateArray().Single(e => e.GetProperty("face").GetInt32() == face)
            .GetProperty("uv").EnumerateArray().Select(p => Floats(p))];

    [Fact]
    public async Task The_described_examples_run_and_read_back()
    {
        using var carve = await StartAsync();
        var examples = ToolList.Examples(await carve.ListToolsAsync(), "project_uvs");
        Assert.Equal(2, examples.Length);
        var box = Ok(await carve.CallToolAsync("project_uvs", examples[0]));
        Assert.Equal(6, box.GetProperty("mapped").GetArrayLength());
        Assert.Equal([5], box.GetProperty("axes").GetProperty("+x").EnumerateArray().Select(x => x.GetInt32()));
        var planar = Ok(await carve.CallToolAsync("project_uvs", examples[1]));
        Assert.Equal([3], planar.GetProperty("mapped").EnumerateArray().Select(x => x.GetInt32()));
        Assert.False(planar.TryGetProperty("axes", out _));
        Assert.Equal([0f, 0.25f, 0.5f, 0.75f], Floats(planar.GetProperty("bounds")));
    }

    [Fact]
    public async Task Planar_default_into_reports_mapped_and_bounds_and_leaves_other_faces()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("set_uvs", SetFace0));
        var result = Ok(await carve.CallToolAsync("project_uvs", """{"method":"planar","view":"+z","faces":[1]}"""));
        Assert.Equal("planar", result.GetProperty("method").GetString());
        Assert.Equal([0f, 0f, 1f, 1f], Floats(result.GetProperty("bounds")));
        var mesh = await Mesh(carve);
        Assert.Equal([[0f, 1f], [1f, 1f], [1f, 0f], [0f, 0f]], UvOf(mesh, 1));
        Assert.Equal([[0f, 0f], [0f, 1f], [1f, 1f], [1f, 0f]], UvOf(mesh, 0));
        Assert.Equal(2, mesh.GetProperty("uvs").GetArrayLength());
    }

    [Fact]
    public async Task Planar_takes_a_direction_and_is_one_undo_step()
    {
        using var carve = await StartAsync();
        var result = Ok(await carve.CallToolAsync("project_uvs", """{"method":"planar","direction":[0,0,3]}"""));
        Assert.Equal(6, result.GetProperty("mapped").GetArrayLength());
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.False((await Mesh(carve)).TryGetProperty("uvs", out _));
    }

    [Fact]
    public async Task A_holed_face_gets_one_uv_per_corner_holes_included()
    {
        using var carve = await StartAsync(Holed);
        Ok(await carve.CallToolAsync("project_uvs", """{"method":"planar","view":"+z"}"""));
        var uv = UvOf(await Mesh(carve), 0);
        Assert.Equal(8, uv.Length);
        Assert.Equal([0.25f, 0.75f], uv[4]);
    }

    [Fact]
    public async Task Cylindrical_runs_with_defaults_and_with_every_option()
    {
        using var carve = await StartAsync();
        var result = Ok(await carve.CallToolAsync("project_uvs", """{"method":"cylindrical","faces":[0,2,3,4,5],"axis":[0,1,0],"centre":[0.5,0,0.5],"seam":[0,0,-1],"into":[0,0,2,1]}"""));
        Assert.Equal(5, result.GetProperty("mapped").GetArrayLength());
        Ok(await carve.CallToolAsync("project_uvs", """{"method":"cylindrical"}"""));
    }

    [Theory]
    [InlineData("""{}""", "method: required but missing")]
    [InlineData("""{"method":"spherical"}""", "method: 'spherical' is not a method")]
    [InlineData("""{"method":"box","faces":[9]}""", "faces[9]: face 9 is not a face of the mesh, which has 6")]
    [InlineData("""{"method":"box","faces":[1,1]}""", "faces[1]: face 1 is listed more than once")]
    [InlineData("""{"method":"box","faces":[]}""", "faces: no faces to map")]
    [InlineData("""{"method":"box","view":"+z"}""", "view: not an option of box; box takes no options")]
    [InlineData("""{"method":"planar","axis":[0,1,0]}""", "axis: not an option of planar; planar takes view, direction")]
    [InlineData("""{"method":"cylindrical","direction":[0,0,1]}""", "direction: not an option of cylindrical; cylindrical takes axis, centre, seam")]
    [InlineData("""{"method":"planar","view":"+z","direction":[0,0,1]}""", "view and direction: give one, not both")]
    [InlineData("""{"method":"planar","view":"up"}""", "view: 'up' is not a view")]
    [InlineData("""{"method":"planar","direction":[0,0,0]}""", "direction: must not be zero")]
    [InlineData("""{"method":"cylindrical","axis":[0,0,0]}""", "axis: must not be zero")]
    [InlineData("""{"method":"cylindrical","seam":[0,0,0]}""", "seam: must not be zero")]
    [InlineData("""{"method":"cylindrical","seam":[0,2,0]}""", "seam: lies along the axis")]
    [InlineData("""{"method":"box","into":[0,0,0,1]}""", "into: [0, 0, 0, 1] has no area")]
    [InlineData("""{"method":"box","into":[1,0,0,1]}""", "has no area")]
    [InlineData("""{"method":"box","into":[0,0,1]}""", "into: expected 4 numbers")]
    [InlineData("""{"method":"box","into":[0,0,"a",1]}""", "into[2]: expected a number")]
    [InlineData("""{"method":"box","into":[0,0,1e99,1]}""", "into[2]: 1e99 is out of range")]
    [InlineData("""{"method":"box","colour":1}""", "colour: not an argument of project_uvs")]
    [InlineData("""{"method":"box","mesh":"nope"}""", "nope")]
    public async Task Every_refusal_names_its_element_and_changes_nothing(string arguments, string expected)
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("set_uvs", SetFace0));
        var before = TextOf(await carve.CallToolAsync("get_mesh", "{}"));
        var text = await Refused(carve, arguments);
        Assert.Contains(expected, text);
        Assert.Equal(before, TextOf(await carve.CallToolAsync("get_mesh", "{}")));
        // The earlier set is the last undo step still.
        Ok(await carve.CallToolAsync("undo", "{}"));
        Assert.False((await Mesh(carve)).TryGetProperty("uvs", out _));
    }

    [Fact]
    public async Task A_deleted_face_and_an_empty_model_are_refused()
    {
        using var carve = await StartAsync();
        Ok(await carve.CallToolAsync("delete", """{"faces":[5]}"""));
        Assert.Contains("faces[5]: face 5 is deleted", await Refused(carve, """{"method":"box","faces":[5]}"""));
        using var bare = new CarveProcess([]);
        await bare.InitializeAsync();
        Assert.Contains("define_mesh", await Refused(bare, """{"method":"box"}"""));
    }
}

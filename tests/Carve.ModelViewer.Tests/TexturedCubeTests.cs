using System.Numerics;
using System.Text.Json;
using Xunit;

namespace Carve.ModelViewer.Tests;

/// <summary>The textured fixture cube viewed from the front without and with <c>--sky</c>, made once for the class.</summary>
public sealed class TexturedRuns : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("carve-viewer-tex-").FullName;

    public TexturedRuns()
    {
        var content = Path.Combine(_temp, "Content");
        Directory.CreateDirectory(Path.Combine(content, "models"));
        File.WriteAllBytes(Path.Combine(content, "models/metal.glb"), TexturedCubeFixture.Write());
        File.WriteAllBytes(Path.Combine(content, "models/cube.glb"), CubeFixture.Write());

        var keep = Environment.GetEnvironmentVariable("CAIRN_ARTIFACTS");
        var root = keep is null ? _temp : Path.Combine(keep, "viewer-textured");
        Plain = ViewerProcess.Run(content, "models/metal.glb", Path.Combine(root, "plain"), CubeRuns.Front);
        Sky = ViewerProcess.Run(content, "models/metal.glb", Path.Combine(root, "sky"), CubeRuns.Front, "--sky");
        Untextured = ViewerProcess.Run(content, "models/cube.glb", Path.Combine(root, "untextured"), CubeRuns.Front);
    }

    internal ViewerResult Plain { get; }

    internal ViewerResult Sky { get; }

    internal ViewerResult Untextured { get; }

    public void Dispose() => Directory.Delete(_temp, recursive: true);
}

[Trait("Tier", "regression")]
public class TexturedCubeTests(TexturedRuns runs) : IClassFixture<TexturedRuns>
{
    private static readonly Vector3 PlusZ = new(0.5f, 0.5f, 1f);
    private static readonly Vector3 Top = new(0.5f, 1f, 0.5f);

    private static JsonElement ViewJson(ViewerResult run) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(run.Out, "view.json"))).RootElement;

    private static ViewerFrame Frame(ViewerResult run)
    {
        var eye = ViewJson(run).GetProperty("views")[0].GetProperty("eye").EnumerateArray().Select(n => n.GetSingle()).ToArray();
        return new ViewerFrame(Path.Combine(run.Out, "front.png"), new Vector3(eye[0], eye[1], eye[2]), new Vector3(0.5f));
    }

    private static int Max((int R, int G, int B) p) => Math.Max(p.R, Math.Max(p.G, p.B));

    [Fact]
    public void Both_runs_pass_and_view_json_says_whether_the_sky_was_on()
    {
        Assert.True(runs.Plain.ExitCode == 0, runs.Plain.Output);
        Assert.True(runs.Sky.ExitCode == 0, runs.Sky.Output);
        Assert.False(ViewJson(runs.Plain).GetProperty("sky").GetBoolean());
        Assert.True(ViewJson(runs.Sky).GetProperty("sky").GetBoolean());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void View_json_reports_every_field_and_texture_of_the_material(bool sky)
    {
        var material = Assert.Single(ViewJson(sky ? runs.Sky : runs.Plain).GetProperty("materials").EnumerateArray());

        Assert.Equal([0.8f, 0.8f, 0.8f, 1f], material.GetProperty("baseColor").EnumerateArray().Select(n => n.GetSingle()));
        Assert.Equal(TexturedCubeFixture.Metallic, material.GetProperty("metallic").GetSingle());
        Assert.Equal(TexturedCubeFixture.Roughness, material.GetProperty("roughness").GetSingle());
        Assert.Equal(
            [TexturedCubeFixture.Emissive.X, TexturedCubeFixture.Emissive.Y, TexturedCubeFixture.Emissive.Z],
            material.GetProperty("emissive").EnumerateArray().Select(n => n.GetSingle()));
        Assert.Equal(TexturedCubeFixture.Occlusion, material.GetProperty("occlusion").GetSingle());
        Assert.True(material.GetProperty("doubleSided").GetBoolean());
        Assert.True(material.GetProperty("pbr").GetBoolean());
        Assert.Contains("Pbr", material.GetProperty("flags").GetString());

        var textures = material.GetProperty("textures");
        foreach (var (slot, width, height) in TexturedCubeFixture.Sizes)
        {
            var texture = textures.GetProperty(slot);
            Assert.True(texture.GetProperty("set").GetBoolean(), slot);
            Assert.Equal(width, texture.GetProperty("width").GetInt32());
            Assert.Equal(height, texture.GetProperty("height").GetInt32());
            Assert.False(string.IsNullOrEmpty(texture.GetProperty("format").GetString()), slot);
        }
    }

    [Fact]
    public void A_cube_with_no_textures_reports_every_slot_unset()
    {
        var view = ViewJson(runs.Untextured);
        var material = Assert.Single(view.GetProperty("materials").EnumerateArray());

        Assert.False(material.GetProperty("doubleSided").GetBoolean());
        Assert.Equal([0f, 0f, 0f], material.GetProperty("emissive").EnumerateArray().Select(n => n.GetSingle()));
        Assert.Equal(1f, material.GetProperty("occlusion").GetSingle());
        Assert.Equal(5, material.GetProperty("textures").EnumerateObject().Count());
        foreach (var slot in material.GetProperty("textures").EnumerateObject())
        {
            Assert.False(slot.Value.GetProperty("set").GetBoolean(), slot.Name);
        }

        Assert.False(view.GetProperty("sky").GetBoolean());
    }

    /// <summary>A metal face reflects what is around it: with no sky it is near-black, with the sky it is lit (T-78's probe: 11 against 150).</summary>
    [Fact]
    public void A_metallic_face_is_lighter_with_the_sky_than_without()
    {
        var plain = Frame(runs.Plain);
        var sky = Frame(runs.Sky);

        foreach (var (name, point) in new[] { ("+Z", PlusZ), ("top", Top) })
        {
            var without = plain.At(point);
            var with = sky.At(point);
            Assert.True(Max(with) >= Max(without) + 60, $"the {name} face is {without} without the sky and {with} with it");
        }

        // The sky lights the model and draws nothing behind it: the corners stay the one dark background.
        Assert.Equal(plain.Pixel(4, 4), sky.Pixel(4, 4));
    }
}

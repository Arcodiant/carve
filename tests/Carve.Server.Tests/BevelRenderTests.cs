extern alias carve;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using carve::Carve.Server;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>
/// T-125's reconciliation against render, T-120's probe made permanent: <c>bake</c> a <c>normal</c> map with width 0.1 on a unit box
/// whose +Z face is laid as <see cref="MaterialRenderTests.FrontUvs"/> lays it, put it on that face as <c>normal_texture</c> over
/// #808080, and render from the front. There (T-120's probe) the top face is the brightest and +X is darker than +Z, so the strip of
/// +Z within 0.05 of its top edge reads brighter than the face's middle, leaning toward the top face's shade, and the strip by its +X
/// edge darker. Not "leaning toward +X's": the edge's mean normal reads darker than both faces in this view, so the person dropped it
/// (2026-11-20). The same map with G inverted, and then with R inverted, each fails.
/// </summary>
[Trait("Tier", "regression")]
public sealed class BevelRenderTests() : FileToolTests("carve-bevel-render")
{
    /// <summary>
    /// The luma the top strip must gain over the middle, and the +X strip lose: the person's rulings (2026-11-20), at least 4.7
    /// brighter and at least 1.6 darker. T-120's probe puts the edge's own normal (the mean, (0, 0.707, 0.707)) at about +9.5 luma
    /// over flat for the top and the +x lean at about −3.2; these are half of each. Probed here: +8.20 and −4.33.
    /// </summary>
    internal const double TopGain = 4.7, SideLoss = 1.6;

    private const int Size = 512;

    private static readonly string Frames = Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "bevel-tests")).FullName;

    private static readonly Vector3 Min = Vector3.Zero, Max = Vector3.One;

    internal sealed record Reading(string Map, double Middle, double TopStrip, double SideStrip, double TopFace, double SideFace)
    {
        public bool TopBrighter => TopStrip - Middle >= TopGain && Math.Abs(TopStrip - TopFace) < Math.Abs(Middle - TopFace);
        // Not "toward +X's shade", by the person's ruling (2026-11-20): the edge's mean normal reads darker than both faces in this
        // view (T-120's probe: R+, which is that normal, 114.3 luma; the +X face 116.5; flat 117.5), so the strip overshoots +X's shade.
        public bool SideDarker => Middle - SideStrip >= SideLoss;
        public string Row => string.Join('\t', new object[] { Map, Middle, TopStrip, SideStrip, TopFace, SideFace, TopStrip - Middle, SideStrip - Middle, TopBrighter, SideDarker }
            .Select(v => v is double d ? d.ToString("F2", CultureInfo.InvariantCulture) : v.ToString()));
    }

    private static double Luma((int R, int G, int B) c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

    private static Vector2 Pixel(Vector3 world) => MaterialRenderTests.PixelOf(world, CameraSpec.ForView(RenderView.Front), Min, Max, Size);

    /// <summary>The mean luma over points of the box, each read at its nearest pixel.</summary>
    private static double Mean(ImageResult image, IEnumerable<Vector3> points) => points.Select(p => Luma(CheckerLookTests.At(image, Pixel(p)))).Average();

    private static IEnumerable<float> Steps(float from, float to, int n) => Enumerable.Range(0, n).Select(i => from + (to - from) * i / (n - 1));

    internal static Reading Read(string map, ImageResult image)
    {
        var middle = Mean(image, from x in Steps(0.4f, 0.6f, 5) from y in Steps(0.4f, 0.6f, 5) select new Vector3(x, y, 1));
        var topStrip = Mean(image, from x in Steps(0.3f, 0.7f, 9) from d in Steps(0.005f, 0.045f, 5) select new Vector3(x, 1 - d, 1));
        var sideStrip = Mean(image, from y in Steps(0.3f, 0.7f, 9) from d in Steps(0.005f, 0.045f, 5) select new Vector3(1 - d, y, 1));
        var topFace = Mean(image, from x in Steps(0.3f, 0.7f, 5) from z in Steps(0.3f, 0.7f, 5) select new Vector3(x, 1, z));
        var sideFace = Mean(image, from y in Steps(0.3f, 0.7f, 5) from z in Steps(0.3f, 0.7f, 5) select new Vector3(1, y, z));
        return new Reading(map, middle, topStrip, sideStrip, topFace, sideFace);
    }

    private async Task<ImageResult> RenderAsync(CarveProcess carve, string material, string normal, string keepAs)
    {
        Ok(await carve.CallToolAsync("define_material", JsonSerializer.Serialize(new { name = material, base_colour = "#808080", normal_texture = normal })));
        Ok(await carve.CallToolAsync("set_material", JsonSerializer.Serialize(new { mesh = "box", faces = new[] { 1 }, material })));
        var result = await carve.CallToolAsync("render", """{"view":"front"}""");
        if (IsError(result)) Assert.Fail(TextOf(result));
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(Size, image.Width);
        return image;
    }

    /// <summary>The PNG with one channel inverted (255 − c), the break the check must catch.</summary>
    private string Inverted(string path, int channel, string name)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlue);
        var bytes = TestPng.Encode(image.Width, image.Height, (x, y) =>
        {
            var at = (y * image.Width + x) * 3;
            var c = new[] { image.Data[at], image.Data[at + 1], image.Data[at + 2] };
            c[channel] = (byte)(255 - c[channel]);
            return (c[0], c[1], c[2]);
        });
        var file = Path.Combine(Root, name);
        File.WriteAllBytes(file, bytes);
        return file;
    }

    [Fact]
    public async Task The_baked_bevel_rounds_the_front_face_toward_the_top_and_the_side_as_the_probe_predicts_and_each_inverted_channel_fails()
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        using var scope = carve;
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", MaterialRenderTests.Box("box", 0)));
        Ok(await carve.CallToolAsync("set_uvs", MaterialRenderTests.FrontUvs("box")));
        // The other faces in the same grey with no map, so the top and +X faces' shades are the ones the strips lean toward.
        Ok(await carve.CallToolAsync("define_material", """{"name":"grey","base_colour":"#808080"}"""));
        Ok(await carve.CallToolAsync("set_material", """{"mesh":"box","faces":[0,2,3,4,5],"material":"grey"}"""));
        Ok(await carve.CallToolAsync("bake", """{"size":256,"padding":8,"maps":["normal"],"bevel":{"width":0.1}}"""));
        var normal = Path.Combine(Root, "textures", "box.normal.png");
        File.Copy(normal, Path.Combine(Frames, "box.normal.png"), overwrite: true);

        var flat = Path.Combine(Root, "flat.png");
        File.WriteAllBytes(flat, TestPng.Encode(16, 16, (_, _) => (128, 128, 255)));
        var readings = new List<Reading>
        {
            Read("flat", await RenderAsync(carve, "flat", flat, "bevel-flat")),
            Read("baked", await RenderAsync(carve, "baked", normal, "bevel-baked")),
            Read("g-inverted", await RenderAsync(carve, "g-inverted", Inverted(normal, 1, "g-inverted.png"), "bevel-g-inverted")),
            Read("r-inverted", await RenderAsync(carve, "r-inverted", Inverted(normal, 0, "r-inverted.png"), "bevel-r-inverted")),
        };
        File.WriteAllLines(Path.Combine(Frames, "bevel-readings.tsv"),
            readings.Select(r => r.Row).Prepend("map\tmiddle\ttop_strip\tside_strip\ttop_face\tside_face\ttop_gain\tside_change\ttop_brighter\tside_darker"));
        var all = string.Join("\n", readings.Select(r => r.Row));

        var baked = readings[1];
        Assert.True(baked.TopBrighter, $"the top strip must read brighter than the middle, toward the top face:\n{all}");
        Assert.True(baked.SideDarker, $"the +X strip must read darker than the middle by SideLoss:\n{all}");
        Assert.False(readings[0].TopBrighter || readings[0].SideDarker, $"a flat map rounds nothing, so it must fail both:\n{all}");
        Assert.False(readings[2].TopBrighter, $"G inverted must fail the top strip:\n{all}");
        Assert.False(readings[3].SideDarker, $"R inverted must fail the +X strip:\n{all}");
    }
}

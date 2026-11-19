using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>
/// T-121: <c>render</c> <c>mode: "uvs"</c> with an <c>image</c>, through the real <c>carve</c>. The mesh is one quad whose UVs
/// cover only the middle of the unit square (u and v 0.25 to 0.75), so the frame has an island, the padding's ground
/// inside the unit square, and the margin beyond it. Every pixel is worked from the marker's stored texels and the window rule
/// (the unit square alone: side 8/7), the layout's pixel rule as <c>UvLayoutLookTests</c> has it.
/// SAMPLING (probed, see <see cref="Sampling_is_nearest_so_a_texel_edge_is_a_step_not_a_blend"/>): nearest, no mips, so the
/// frame pixel at a uv shows texel <c>floor(uv * size)</c> as stored; the padding's ground shows it with each channel
/// <c>(c + 1) / 2</c>.
/// </summary>
[Trait("Tier", "regression")]
public sealed class UvImageLookTests() : TempFolderTests("carve-uv-image")
{
    private const string Quad = """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0]],"faces":[[0,1,2,3]]}""";

    /// <summary>The quad over the middle of the unit square, uv (0.25, 0.25) at its top-left.</summary>
    private const string MiddleUvs = """{"faces":[{"face":0,"uv":[[0.25,0.75],[0.75,0.75],[0.75,0.25],[0.25,0.25]]}]}""";

    private const int Size = 512;

    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "uv-image-tests")).FullName;

    private static (int R, int G, int B) Px(ImageResult image, int x, int y)
    {
        var at = (y * image.Width + x) * 4;
        return (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
    }

    private static bool Near((int R, int G, int B) got, (int R, int G, int B) want, int by = 2) =>
        Math.Abs(got.R - want.R) <= by && Math.Abs(got.G - want.G) <= by && Math.Abs(got.B - want.B) <= by;

    /// <summary>The frame pixel whose centre is at <paramref name="uv"/>: the window is the unit square grown by 8/7.</summary>
    private static int At(double uv) => (int)Math.Round(((uv - 0.5) / (8.0 / 7) + 0.5) * Size - 0.5);

    private static (int R, int G, int B) Half((int R, int G, int B) c) => ((c.R + 1) / 2, (c.G + 1) / 2, (c.B + 1) / 2);

    private static readonly (int R, int G, int B) Red = (255, 0, 0), Green = (0, 255, 0), Blue = (0, 0, 255), White = (255, 255, 255);

    private async Task<CarveProcess> StartAsync(bool seam = false)
    {
        var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Quad));
        Ok(await carve.CallToolAsync("set_uvs", MiddleUvs));
        if (seam) Ok(await carve.CallToolAsync("mark_edges", """{"edges":[[0,1]],"seam":true}"""));
        return carve;
    }

    private static void Ok(JsonElement result) => Assert.False(IsError(result), IsError(result) ? TextOf(result) : "");

    private static ImageResult Decode(JsonElement result, string keepAs)
    {
        Ok(result);
        var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(Size, image.Width);
        return image;
    }

    private static async Task<ImageResult> RenderAsync(CarveProcess carve, string? image, string keepAs, string extra = "")
    {
        var arguments = image is null ? $$"""{"mode":"uvs"{{extra}}}""" : $$"""{"mode":"uvs","image":"{{image}}"{{extra}}}""";
        return Decode(await carve.CallToolAsync("render", arguments), keepAs);
    }

    /// <summary>The marker's four quadrants at the centres of their island parts (u, v of 0.375 or 0.625), stored colours.</summary>
    private static List<string> InsideProblems(ImageResult image) => Check(image, [0.375, 0.625], c => c);

    /// <summary>The same four quadrants in the padding (u, v of 0.1 or 0.9), at half brightness.</summary>
    private static List<string> OutsideProblems(ImageResult image) => Check(image, [0.1, 0.9], Half);

    private static List<string> Check(ImageResult image, double[] centres, Func<(int R, int G, int B), (int R, int G, int B)> shown)
    {
        var problems = new List<string>();
        (int R, int G, int B)[] marker = [Red, Green, Blue, White];
        for (var q = 0; q < 4; q++)
        {
            var (u, v) = (centres[q % 2], centres[q / 2]);
            var (x, y) = (At(u), At(v));
            var want = shown(marker[q]);
            if (!Near(Px(image, x, y), want)) problems.Add($"({x}, {y}) uv ({u}, {v}) is {Px(image, x, y)}, wanted {want}");
        }

        return problems;
    }

    [Fact]
    public async Task The_marker_is_as_stored_inside_the_island_and_halved_in_the_padding_and_the_margin_is_the_checker()
    {
        File.WriteAllBytes(Path.Combine(Root, "marker.png"), TestPng.Marker);
        using var carve = await StartAsync();
        var result = await carve.CallToolAsync("render", """{"mode":"uvs","image":"marker.png"}""");
        var image = Decode(result, "marker");
        Assert.Equal("512x512, uvs mesh, image marker.png 64 x 64, 4 vertices, 1 face", TextOf(result, 1));

        Assert.Empty(InsideProblems(image));
        Assert.Empty(OutsideProblems(image));

        // Beyond the unit square the ground is as it is without an image: the dimmed checker.
        var plain = await RenderAsync(carve, null, "marker-plain");
        foreach (var (x, y) in new[] { (5, 5), (506, 5), (5, 506), (506, 506), (256, 4) })
            Assert.True(Near(Px(image, x, y), Px(plain, x, y), 0), $"({x}, {y}) is {Px(image, x, y)}, without the image {Px(plain, x, y)}");

        // The island's own pixels are the image where the checker was bright; the padding is not (dimmed checker before).
        Assert.False(Near(Px(plain, At(0.375), At(0.375)), Px(image, At(0.375), At(0.375)), 20));
    }

    [Fact]
    public async Task Sampling_is_nearest_so_a_texel_edge_is_a_step_not_a_blend()
    {
        File.WriteAllBytes(Path.Combine(Root, "marker.png"), TestPng.Marker);
        using var carve = await StartAsync();
        var image = await RenderAsync(carve, "marker.png", "marker-edge");
        // The quadrants meet at u = 0.5, frame x about 255.5 (the window's middle). A texel is 7 frame pixels wide; with linear
        // filtering the pixel 2 away from the edge would be a blend of red and green by about 20%. Nearest gives the pure colour.
        var edge = At(0.5);
        var y = At(0.375);
        Assert.True(Near(Px(image, edge - 2, y), Red, 1), $"left of the edge {Px(image, edge - 2, y)}");
        Assert.True(Near(Px(image, edge + 3, y), Green, 1), $"right of the edge {Px(image, edge + 3, y)}");
        // The padding likewise, half brightness: pure half red and half green, no mid colour.
        var padY = At(0.1);
        Assert.True(Near(Px(image, edge - 2, padY), Half(Red), 1), $"padding left {Px(image, edge - 2, padY)}");
        Assert.True(Near(Px(image, edge + 3, padY), Half(Green), 1), $"padding right {Px(image, edge + 3, padY)}");
    }

    [Fact]
    public async Task A_v_flipped_image_and_a_u_mirrored_one_each_fail_the_marker_check()
    {
        File.WriteAllBytes(Path.Combine(Root, "flipped.png"), TestPng.Encode(64, 64, (x, y) => MarkerAt(x, 63 - y)));
        File.WriteAllBytes(Path.Combine(Root, "mirrored.png"), TestPng.Encode(64, 64, (x, y) => MarkerAt(63 - x, y)));
        using var carve = await StartAsync();

        foreach (var name in new[] { "flipped", "mirrored" })
        {
            var image = await RenderAsync(carve, name + ".png", name);
            Assert.NotEmpty(InsideProblems(image));
            Assert.NotEmpty(OutsideProblems(image));
        }

        File.WriteAllBytes(Path.Combine(Root, "marker.png"), TestPng.Marker);
        var right = await RenderAsync(carve, "marker.png", "marker-again");
        Assert.Empty(InsideProblems(right));
    }

    private static (byte R, byte G, byte B) MarkerAt(int x, int y) => (x < 32, y < 32) switch
    {
        (true, true) => (255, 0, 0),
        (false, true) => (0, 255, 0),
        (true, false) => (0, 0, 255),
        _ => (255, 255, 255),
    };

    [Fact]
    public async Task Edges_seams_and_the_highlight_are_drawn_over_the_image_exactly_as_without_it()
    {
        File.WriteAllBytes(Path.Combine(Root, "marker.png"), TestPng.Marker);
        using var carve = await StartAsync(seam: true);
        var plain = await RenderAsync(carve, null, "lines-plain", ",\"labels\":\"faces\"");
        var drawn = await RenderAsync(carve, "marker.png", "lines-image", ",\"labels\":\"faces\"");

        // Every pixel the plain frame draws as pure white (the labels, the border and the edges' cores) is the same with the image under it.
        var white = 0;
        for (var y = 0; y < Size; y++)
        for (var x = 0; x < Size; x++)
        {
            if (Px(plain, x, y) != (255, 255, 255)) continue;
            Assert.True(Px(drawn, x, y) == (255, 255, 255), $"({x}, {y}) is {Px(drawn, x, y)}, plain white");
            white++;
        }

        Assert.True(white > 200, $"{white} white pixels");

        // The lines are antialiased, so over a different ground they blend differently; each is still the line's colour over it.
        // The seam is the bottom edge (v = 0.75, rows 367 and 368): over the blue and the white quadrant it is yellow, not the
        // image; the plain frame's seam there is about (189, 170, 76) over the dimmed checker.
        foreach (var u in new[] { 0.375, 0.625 })
        {
            var x = At(u);
            var rows = new[] { Px(drawn, x, At(0.75) - 1), Px(drawn, x, At(0.75)) };
            Assert.Contains(rows, p => p.R > 150 && p.G > 130 && p.B < 130 && p.R - p.B > 80);
        }

        // The top edge (v = 0.25, white) over the red and the green quadrant: the green or blue channel the image lacks is lit by the line.
        foreach (var (u, lacks) in new[] { (0.375, 1), (0.625, 0) })
        {
            var x = At(u);
            var rows = new[] { Px(drawn, x, At(0.25) - 1), Px(drawn, x, At(0.25)) };
            Assert.Contains(rows, p => (lacks == 1 ? p.G : p.R) > 100);
        }

        // A highlighted face is solid orange over the image, not the image.
        var lit = await RenderAsync(carve, "marker.png", "lines-highlight", ",\"highlight\":{\"faces\":[0]}");
        var litPlain = await RenderAsync(carve, null, "lines-highlight-plain", ",\"highlight\":{\"faces\":[0]}");
        var (cx, cy) = (At(0.375), At(0.375));
        Assert.True(Px(lit, cx, cy) == Px(litPlain, cx, cy), $"highlight {Px(lit, cx, cy)}, plain {Px(litPlain, cx, cy)}");
    }

    [Fact]
    public async Task A_grey_png_shows_as_grey_and_a_frame_without_an_image_after_one_with_is_the_plain_frame()
    {
        // 8-bit grey (colour type 0): 200 on the left half, 60 on the right.
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header, 64);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 64);
        header[8] = 8;
        header[9] = 0;
        var raw = new byte[64 * 65];
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 64; x++) raw[y * 65 + 1 + x] = (byte)(x < 32 ? 200 : 60);
        File.WriteAllBytes(Path.Combine(Root, "grey.png"), TestPng.Write(header, raw));

        using var carve = await StartAsync();
        var plain = await RenderAsync(carve, null, "grey-plain");
        var grey = await RenderAsync(carve, "grey.png", "grey");
        Assert.True(Near(Px(grey, At(0.375), At(0.5)), (200, 200, 200)), $"inside left {Px(grey, At(0.375), At(0.5))}");
        Assert.True(Near(Px(grey, At(0.625), At(0.5)), (60, 60, 60)), $"inside right {Px(grey, At(0.625), At(0.5))}");
        Assert.True(Near(Px(grey, At(0.1), At(0.5)), (100, 100, 100)), $"padding left {Px(grey, At(0.1), At(0.5))}");
        Assert.True(Near(Px(grey, At(0.9), At(0.5)), (30, 30, 30)), $"padding right {Px(grey, At(0.9), At(0.5))}");

        var after = await RenderAsync(carve, null, "grey-after");
        Assert.True(plain.Data.AsSpan().SequenceEqual(after.Data), "the frame without an image is not the plain frame after one with");
    }
}

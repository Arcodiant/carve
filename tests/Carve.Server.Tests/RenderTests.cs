using System.Numerics;
using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>The real <c>carve</c> over stdio; these need a Vulkan device, as verify does (M-1).</summary>
public class RenderTests
{
    private const int Size = 512;
    private const int MinimumStep = 10;

    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private const string Pyramid =
        """{"vertices":[[0,0,0],[2,0,0],[2,0,2],[0,0,2],[1,2,1]],"faces":[[0,1,2,3],[0,4,1],[1,4,2],[2,4,3],[3,4,0]]}""";

    /// <summary>A cube's counts in another shape.</summary>
    private const string LongBox =
        """{"vertices":[[0,0,0],[3,0,0],[3,1,0],[0,1,0],[0,0,1],[3,0,1],[3,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    /// <summary>One quad facing -Z: the front view sees only its back.</summary>
    private const string QuadFacingAway =
        """{"vertices":[[0,0,0],[0,1,0],[1,1,0],[1,0,0]],"faces":[[0,1,2,3]]}""";

    private static readonly string Frames = Path.Combine(Paths.RepoRoot, "artifacts", "render-tests");

    private static Task<JsonElement> RenderAsync(CarveProcess carve, string? view = null) =>
        carve.CallToolAsync("render", view is null ? "{}" : $$"""{"view":"{{view}}"}""");

    private static string Caption(JsonElement result) => TextOf(result, 1);

    private static string ErrorText(JsonElement result)
    {
        Assert.True(IsError(result));
        return TextOf(result);
    }

    private static byte[] Png(JsonElement result)
    {
        Assert.False(IsError(result), result.ToString());
        var image = result.GetProperty("content")[0];
        Assert.Equal("image", image.GetProperty("type").GetString());
        Assert.Equal("image/png", image.GetProperty("mimeType").GetString());
        return Convert.FromBase64String(image.GetProperty("data").GetString()!);
    }

    private static ImageResult Decode(byte[] png, string keepAs)
    {
        Directory.CreateDirectory(Frames);
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);

        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(Size, image.Width);
        Assert.Equal(Size, image.Height);
        return image;
    }

    private static (int R, int G, int B) At(ImageResult image, (int X, int Y) pixel)
    {
        var at = (pixel.Y * image.Width + pixel.X) * 4;
        return (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
    }

    /// <summary>Where a world point lands in the image, for the unit cube's framing.</summary>
    private static (int X, int Y) PixelOf(Vector3 world, RenderView view)
    {
        var frame = ViewFraming.Frame(Vector3.Zero, Vector3.One, view);
        var clip = Vector4.Transform(new Vector4(world, 1), frame.View * frame.Projection);
        return ((int)((clip.X / clip.W + 1) / 2 * Size), (int)((1 - clip.Y / clip.W) / 2 * Size));
    }

    private static void AssertBackground((int R, int G, int B) pixel)
    {
        // Linear (0.03, 0.035, 0.04), sRGB encoded.
        Assert.InRange(pixel.R, 47, 49);
        Assert.InRange(pixel.G, 51, 53);
        Assert.InRange(pixel.B, 55, 57);
    }

    public static TheoryData<RenderView, string, float[], float[], float[]> Views => new()
    {
        { RenderView.Front, "front", [0.5f, 1, 0.5f], [0.5f, 0.5f, 1], [1, 0.5f, 0.5f] },
        { RenderView.Back, "back", [0.5f, 0.5f, 0], [0, 0.5f, 0.5f], [0.5f, 0, 0.5f] },
    };

    [Theory]
    [MemberData(nameof(Views))]
    public async Task A_cube_shows_three_faces_in_the_expected_order_on_the_background(
        RenderView view, string name, float[] brightest, float[] middle, float[] darkest)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        Assert.False(IsError(await carve.CallToolAsync("define_mesh", Cube)));

        var result = await RenderAsync(carve, name);

        Assert.Equal($"512x512, view {name}, 8 vertices, 6 faces", Caption(result));
        var image = Decode(Png(result), $"cube-{name}");
        AssertBackground(At(image, (2, 2)));

        var pixels = new[] { brightest, middle, darkest }
            .Select(p => PixelOf(new Vector3(p[0], p[1], p[2]), view))
            .ToArray();
        File.AppendAllText(Path.Combine(Frames, "samples.txt"),
            $"{name}: {string.Join(" ", pixels.Select(p => $"{p} = {At(image, p)}"))}\n");

        var (high, mid, low) = (At(image, pixels[0]), At(image, pixels[1]), At(image, pixels[2]));
        Assert.True(high.G >= mid.G + MinimumStep, $"{high} then {mid}");
        Assert.True(mid.G >= low.G + MinimumStep, $"{mid} then {low}");
        Assert.True(low.G >= 53 + MinimumStep, $"{low} against the background");
    }

    [Fact]
    public async Task The_default_view_is_front_and_a_repeat_is_the_same_image()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var first = await RenderAsync(carve);
        var back = await RenderAsync(carve, "back");
        var front = await RenderAsync(carve, "front");

        Assert.Equal("512x512, view front, 8 vertices, 6 faces", Caption(first));
        Assert.Equal(Png(first), Png(front));
        Assert.NotEqual(Png(first), Png(back));
    }

    [Fact]
    public async Task Render_before_define_is_a_tool_error_saying_to_call_define_mesh()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();

        Assert.Contains("define_mesh", ErrorText(await RenderAsync(carve)));
    }

    [Fact]
    public async Task An_unknown_view_is_a_tool_error_naming_the_views()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var text = ErrorText(await RenderAsync(carve, "left"));

        Assert.Contains("front", text);
        Assert.Contains("back", text);
    }

    [Fact]
    public async Task Replacing_the_mesh_changes_the_image_on_the_very_next_call()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);
        var cube = await RenderAsync(carve);

        await carve.CallToolAsync("define_mesh", Pyramid);
        var pyramid = await RenderAsync(carve);

        Assert.Equal("512x512, view front, 5 vertices, 5 faces", Caption(pyramid));
        Assert.NotEqual(Png(cube), Png(pyramid));
        Decode(Png(pyramid), "pyramid-front");
    }

    public static TheoryData<string, string> Replacements => new()
    {
        { Pyramid, "512x512, view front, 5 vertices, 5 faces" }, // The caption would tell.
        { LongBox, "512x512, view front, 8 vertices, 6 faces" }, // The same counts: only the image can.
    };

    /// <summary>
    /// The burst holds the frame thread inside one drain of the queue: the wait is on its first
    /// answer, so no delay has to be guessed, and the later calls land while the rest are queued.
    /// </summary>
    [Theory]
    [MemberData(nameof(Replacements))]
    public async Task A_render_after_define_mesh_returned_shows_that_mesh_with_renders_overlapping(string mesh, string caption)
    {
        const int Burst = 30;

        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();

        // Rendered with nothing overlapping, and byte-equal within a process.
        await carve.CallToolAsync("define_mesh", mesh);
        var quiet = Png(await RenderAsync(carve));

        for (var round = 0; round < 3; round++)
        {
            await carve.CallToolAsync("define_mesh", Cube);
            Assert.NotEqual(quiet, Png(await RenderAsync(carve)));

            var burst = Enumerable.Range(0, Burst).Select(_ => RenderAsync(carve)).ToArray();
            await Task.WhenAny(burst);

            Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh)));
            var after = await RenderAsync(carve);

            Assert.Equal(caption, Caption(after));
            Assert.Equal(quiet, Png(after));
            Assert.All(await Task.WhenAll(burst), r => Assert.False(IsError(r), r.ToString()));
        }
    }

    /// <summary>A cube at the origin is 376 wide; the floor used to leave these 10 to 99.</summary>
    [Theory]
    [InlineData(1e-8, 0.0, "tiny")]
    [InlineData(1.0, 1e5, "far-1e5")]
    [InlineData(1.0, 1e6, "far-1e6")]
    public async Task A_tiny_or_far_cube_keeps_its_size_in_the_frame(double side, double offset, string name)
    {
        double[][] unit = [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];
        var vertices = string.Join(",", unit.Select(v =>
            "[" + string.Join(",", v.Select(c => (c * side + offset).ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]"));
        var mesh = $$"""{"vertices":[{{vertices}}],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        Assert.False(IsError(await carve.CallToolAsync("define_mesh", mesh)));

        var image = Decode(Png(await RenderAsync(carve)), $"cube-{name}");

        var columns = Enumerable.Range(0, Size)
            .Where(x => Enumerable.Range(0, Size).Any(y => At(image, (x, y)).G > 60))
            .ToArray();
        var width = columns.Length == 0 ? 0 : columns[^1] - columns[0] + 1;
        File.AppendAllText(Path.Combine(Frames, "samples.txt"), $"{name}: width {width}\n");
        Assert.InRange(width, 340, 412);
    }

    [Fact]
    public async Task A_face_turned_away_gives_an_empty_image_and_no_error()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", QuadFacingAway);

        var image = Decode(Png(await RenderAsync(carve)), "quad-away");

        AssertBackground(At(image, (Size / 2, Size / 2)));
        Assert.Single(image.Data.Chunk(4).Select(p => (p[0], p[1], p[2])).Distinct());
    }

    [Fact]
    public async Task With_no_device_render_is_a_tool_error_naming_vulkan_and_the_server_lives_on()
    {
        using var carve = new CarveProcess([], new Dictionary<string, string>
        {
            ["VK_LOADER_DRIVERS_SELECT"] = "nonexistent",
        });
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        Assert.Contains("Vulkan", ErrorText(await RenderAsync(carve)));
        Assert.Contains("Vulkan", ErrorText(await RenderAsync(carve, "back")));

        carve.CloseStdin();
        Assert.Equal(0, await carve.WaitForExitAsync(Exit));
    }

    [Fact]
    public async Task The_tool_lists_its_views_and_says_what_missing_and_empty_mean()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();

        var tools = await carve.ListToolsAsync();
        var render = tools.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "render");

        var description = render.GetProperty("description").GetString()!;
        Assert.Contains("front", description);
        Assert.Contains("back", description);
        Assert.Contains("MISSING", description);
        Assert.Contains("EMPTY", description);

        var view = render.GetProperty("inputSchema").GetProperty("properties").GetProperty("view");
        Assert.Equal(["front", "back"], view.GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("front", view.GetProperty("default").GetString());
    }
}

using System.Numerics;
using System.Text.Json;
using StbImageSharp;

namespace Carve.Server.Tests;

/// <summary>The real <c>carve</c> over stdio; these need a Vulkan device, as verify does (M-1).</summary>
[Trait("Tier", "regression")]
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

    /// <summary>Made here, so no test depends on another having written first.</summary>
    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "render-tests")).FullName;

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

    private static ImageResult Decode(byte[] png, string keepAs, int size = Size)
    {
        File.WriteAllBytes(Path.Combine(Frames, keepAs + ".png"), png);

        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
        Assert.Equal(size, image.Width);
        Assert.Equal(size, image.Height);
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
        var space = DrawnSpace.Of(Vector3.Zero, Vector3.One);
        var frame = ViewFraming.Frame(space.Apply(Vector3.One), view);
        var clip = Vector4.Transform(new Vector4(space.Apply(world), 1), frame.View * frame.Projection);
        return ((int)((clip.X / clip.W + 1) / 2 * Size), (int)((1 - clip.Y / clip.W) / 2 * Size));
    }

    private static bool IsBackground((int R, int G, int B) pixel) =>
        pixel.R is >= 47 and <= 49 && pixel.G is >= 51 and <= 53 && pixel.B is >= 55 and <= 57; // Linear (0.03, 0.035, 0.04), sRGB encoded.

    private static void AssertBackground((int R, int G, int B) pixel) => Assert.True(IsBackground(pixel), pixel.ToString());

    /// <summary>The bounding box of the pixels that aren't background, and how many there are.</summary>
    private static (int Left, int Top, int Right, int Bottom, int Count) Foreground(ImageResult image)
    {
        var drawn = (
            from y in Enumerable.Range(0, Size)
            from x in Enumerable.Range(0, Size)
            where !IsBackground(At(image, (x, y)))
            select (x, y)).ToArray();
        Assert.NotEmpty(drawn);
        return (drawn.Min(p => p.x), drawn.Min(p => p.y), drawn.Max(p => p.x), drawn.Max(p => p.y), drawn.Length);
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

    private static string Scaled(double side, double x, double y, double z)
    {
        var at = (double c, double offset) => (c * side + offset).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        double[][] unit = [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];
        var vertices = string.Join(",", unit.Select(v => $"[{at(v[0], x)},{at(v[1], y)},{at(v[2], z)}]"));
        return $$"""{"vertices":[{{vertices}}],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";
    }

    /// <summary>
    /// Every size and place draws the unit cube's picture, in both views: the bridge normalises what
    /// is drawn, so nothing is too big, too small or too far. Each of these stores as an exact cube,
    /// two coordinates to an axis, so the measured difference is none: the same box, the same bytes.
    /// </summary>
    [Theory]
    [InlineData(1.0, 0.0, 0.0, 0.0)]
    [InlineData(1e-30, 0.0, 0.0, 0.0)]
    [InlineData(1e-20, 0.0, 0.0, 0.0)]
    [InlineData(1e-15, 0.0, 0.0, 0.0)]
    [InlineData(1e19, 0.0, 0.0, 0.0)]
    [InlineData(1e30, 0.0, 0.0, 0.0)]
    [InlineData(1.0, 1e6, 1e6, 1e6)]
    [InlineData(1.0, 2.5e6, 2.5e6, 2.5e6)]
    [InlineData(1.0, 4e6, 4e6, 4e6)]
    [InlineData(1.0, 1e7, 1e7, 1e7)]
    [InlineData(1.0, -3598575.2, -3598575.2, 3598575.2)]
    [InlineData(1.0, 0.0, -4e6, 0.0)]
    [InlineData(1.0, 0.0, 5e6, 5e6)]
    [InlineData(4e-5, 100.0, 100.0, 100.0)]
    public async Task A_cube_of_any_size_at_any_place_draws_like_the_unit_cube(double side, double x, double y, double z)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);
        var plain = new[] { "front", "back" }.ToDictionary(v => v, v => RenderAsync(carve, v).Result);

        Assert.False(IsError(await carve.CallToolAsync("define_mesh", Scaled(side, x, y, z))));

        foreach (var view in plain.Keys)
        {
            var name = $"any-{side}-{x}-{y}-{z}-{view}";
            var image = Decode(Png(await RenderAsync(carve, view)), name);
            var unit = Decode(Png(plain[view]), $"any-unit-{view}");

            var edges = Enumerable.Range(0, Size).SelectMany(i => new[] { (i, 0), (i, Size - 1), (0, i), (Size - 1, i) });
            Assert.All(edges, p => AssertBackground(At(image, p)));

            var (found, wanted) = (Foreground(image), Foreground(unit));
            File.AppendAllText(Path.Combine(Frames, "samples.txt"), $"{name}: {found}\n");

            Assert.Equal(wanted, found);
            Assert.Equal(unit.Data, image.Data);
        }

        await carve.CallToolAsync("define_mesh", Cube);
        Assert.Equal(Png(plain["front"]), Png(await RenderAsync(carve, "front")));
        Assert.DoesNotContain("fail:", carve.Stderr);
    }

    /// <summary>
    /// Where floats are too coarse for the mesh, the picture is the mesh as stored. Side 1.3 at 1e7
    /// on X stores as side 1 there, so this draws the same 1 by 1.3 by 1.3 box as one at the origin.
    /// </summary>
    [Theory]
    [InlineData("front")]
    [InlineData("back")]
    public async Task A_mesh_coarsened_by_float_storage_draws_as_stored_and_is_not_refused(string view)
    {
        static string Box(float x, float side) =>
            $$"""{"vertices":[[{{x}},0,0],[{{x + 1}},0,0],[{{x + 1}},{{side}},0],[{{x}},{{side}},0],[{{x}},0,{{side}}],[{{x + 1}},0,{{side}}],[{{x + 1}},{{side}},{{side}}],[{{x}},{{side}},{{side}}]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Scaled(1.3, 1e7, 0, 0));
        var far = await RenderAsync(carve, view);

        await carve.CallToolAsync("define_mesh", Box(0, 1.3f));
        var stored = await RenderAsync(carve, view);

        Assert.Equal(Decode(Png(stored), $"coarsened-stored-{view}").Data, Decode(Png(far), $"coarsened-far-{view}").Data);
    }

    /// <summary>
    /// The frame thread steps about once per several define_mesh, and a render takes 2 steps, so
    /// the cap on the calls is a cap in frames, however long each one takes.
    /// </summary>
    [Fact]
    public async Task A_queued_render_answers_during_a_stream_of_define_mesh()
    {
        const int MostCalls = 2000;

        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var render = RenderAsync(carve, "back");
        var calls = 0;
        while (!render.IsCompleted)
        {
            Assert.True(++calls <= MostCalls, $"no answer in {MostCalls} calls of define_mesh");
            Assert.False(IsError(await carve.CallToolAsync("define_mesh", Pyramid)));
        }

        Assert.False(IsError(await render));
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
        Assert.Contains("'labels'", description);
        Assert.Contains("Only what faces the camera is labelled", description);
        Assert.Contains("name an edge by its two vertex ids", description);
        Assert.Contains("a label may sit on a face hidden", description);
        Assert.Contains("'views'", description);
        Assert.Contains("'columns'", description);
        Assert.Contains("the six axis views, orthographic, at 256", description);
        Assert.Contains("'3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, 8 vertices, 6 faces'", description);
        Assert.Contains("fewer than 2 views or more than 6", description);
        Assert.Contains("columns other than 1, 2 or 3", description);
        Assert.Contains("views together with view, direction, zoom or target", description);

        var view = render.GetProperty("inputSchema").GetProperty("properties").GetProperty("view");
        Assert.Equal(["front", "back", "+x", "-x", "+y", "-y", "+z", "-z"], view.GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Contains("direction", description);
        Assert.Contains("zoom", description);
        Assert.Contains("target", description);
    }

    [Theory]
    [InlineData("""{"view":"front","direction":[1,0,0]}""", "view and direction")]
    [InlineData("""{"direction":[0,0,0]}""", "direction")]
    [InlineData("""{"direction":[1,2]}""", "direction")]
    [InlineData("""{"direction":[1,1e999,0]}""", "direction")]
    [InlineData("""{"zoom":0.5}""", "zoom")]
    [InlineData("""{"zoom":9}""", "zoom")]
    [InlineData("""{"target":[2,0,0]}""", "target")]
    [InlineData("""{"target":[0,0]}""", "target")]
    [InlineData("""{"target":[0,0,-0.001]}""", "target")]
    [InlineData("""{"view":"top"}""", "view: 'top' is not a view; use 'front', 'back', '+x', '-x', '+y', '-y', '+z' or '-z'.")]
    [InlineData("""{"view":"+w"}""", "view: '+w' is not a view")]
    [InlineData("""{"projection":"iso"}""", "projection: 'iso' is not a projection; use 'perspective' or 'orthographic'.")]
    [InlineData("""{"size":300}""", "size: 300 is not a size; use 256, 512 or 1024")]
    [InlineData("""{"size":2048}""", "size: 2048 is not a size; use 256, 512 or 1024")]
    [InlineData("""{"size":512.5}""", "size: 512.5 is not a size; use 256, 512 or 1024")]
    [InlineData("""{"mode":"xray"}""", "mode: 'xray' is not a mode; use 'shaded', 'wireframe', 'normals' or 'checker'.")]
    public async Task A_bad_camera_is_a_tool_error_and_the_mesh_still_renders(string arguments, string mentions)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);
        var before = Png(await RenderAsync(carve));

        Assert.Contains(mentions, ErrorText(await carve.CallToolAsync("render", arguments)));
        Assert.Equal(before, Png(await RenderAsync(carve)));
    }

    /// <summary>B-1: 1.02 is not exact in float, so the stored bound is 1.0199999 and a double compare refused it.</summary>
    [Fact]
    public async Task A_target_on_the_reported_bound_of_a_mesh_inexact_in_float_is_accepted_and_one_beyond_is_refused()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[1.02,0,0],[1.02,1,0],[0,1,0]],"faces":[[0,1,2,3]]}""");

        Assert.False(IsError(await carve.CallToolAsync("render", """{"direction":[2,1,1],"target":[1.02,0.4,0]}""")));
        Assert.Contains("target", ErrorText(await carve.CallToolAsync("render", """{"direction":[2,1,1],"target":[1.021,0.4,0]}""")));
    }

    [Fact]
    public async Task The_caption_echoes_the_direction_and_shows_zoom_and_target_only_when_set()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        Assert.Equal("512x512, direction (2, 1, 1), 8 vertices, 6 faces", Caption(await carve.CallToolAsync("render", """{"direction":[2,1,1]}""")));
        Assert.Equal(
            "512x512, direction (2, 1, 1), zoom 3, target (1, 0.5, 0.25), 8 vertices, 6 faces",
            Caption(await carve.CallToolAsync("render", """{"direction":[2,1,1],"zoom":3,"target":[1,0.5,0.25]}""")));
        Assert.Equal("512x512, view back, zoom 2, 8 vertices, 6 faces", Caption(await carve.CallToolAsync("render", """{"view":"back","zoom":2}""")));
        Assert.Equal("512x512, view front, 8 vertices, 6 faces", Caption(await carve.CallToolAsync("render", "{}")));
    }

    [Fact]
    public async Task The_caption_opens_with_the_size_and_says_orthographic_after_the_view()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        foreach (var (arguments, caption) in new[]
        {
            ("""{"size":256}""", "256x256, view front, 8 vertices, 6 faces"),
            ("""{"size":512}""", "512x512, view front, 8 vertices, 6 faces"),
            ("""{"size":1024,"view":"back"}""", "1024x1024, view back, 8 vertices, 6 faces"),
            ("""{"view":"+x"}""", "512x512, view +x, 8 vertices, 6 faces"),
            ("""{"view":"-y","projection":"perspective"}""", "512x512, view -y, 8 vertices, 6 faces"),
            ("""{"view":"+x","projection":"orthographic"}""", "512x512, view +x, orthographic, 8 vertices, 6 faces"),
            ("""{"direction":[2,1,1],"projection":"orthographic"}""", "512x512, direction (2, 1, 1), orthographic, 8 vertices, 6 faces"),
            ("""{"size":256,"view":"-z","projection":"orthographic","zoom":2,"target":[1,0.5,0.25]}""", "256x256, view -z, orthographic, zoom 2, target (1, 0.5, 0.25), 8 vertices, 6 faces"),
        })
        {
            Assert.Equal(caption, Caption(await carve.CallToolAsync("render", arguments)));
        }
    }

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public async Task The_image_is_the_size_asked_for_and_shows_the_mesh(int size)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var image = Decode(Png(await carve.CallToolAsync("render", $$"""{"size":{{size}}}""")), $"cube-size-{size}", size);

        AssertBackground(At(image, (2, 2)));
        Assert.NotEqual(At(image, (2, 2)), At(image, (size / 2, size / 2)));
    }

    [Theory]
    [InlineData("front", "perspective")]
    [InlineData("back", "perspective")]
    [InlineData("+x", "perspective")]
    [InlineData("-x", "perspective")]
    [InlineData("+y", "perspective")]
    [InlineData("-y", "perspective")]
    [InlineData("+z", "perspective")]
    [InlineData("-z", "perspective")]
    [InlineData("+y", "orthographic")]
    [InlineData("-y", "orthographic")]
    public async Task Every_named_view_renders_the_mesh(string view, string projection)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var image = Decode(Png(await carve.CallToolAsync("render", $$"""{"view":"{{view}}","projection":"{{projection}}"}""")), $"cube-view-{view}-{projection}");

        AssertBackground(At(image, (2, 2)));
        Assert.NotEqual(At(image, (2, 2)), At(image, (Size / 2, Size / 2)));
    }

    [Theory]
    [InlineData("+x", "[1,0,0]")]
    [InlineData("-y", "[0,-1,0]")]
    public async Task An_axis_name_is_that_direction(string view, string direction)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", """{"vertices":[[0,0,0],[2,0,0],[2,0,3],[0,0,3],[1,2,1]],"faces":[[0,1,2,3],[0,4,1],[1,4,2],[2,4,3],[3,4,0]]}""");

        Assert.Equal(
            Png(await carve.CallToolAsync("render", $$"""{"direction":{{direction}}}""")),
            Png(await RenderAsync(carve, view)));
    }

    [Fact]
    public async Task The_description_says_what_the_new_arguments_do()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        var description = (await carve.ListToolsAsync()).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "render")
            .GetProperty("description").GetString()!;

        foreach (var words in new[]
        {
            "'+x'", "'-x'", "'+y'", "'-y'", "'+z'", "'-z'", "'projection'", "keeps parallel edges parallel", "what a game shows",
            "reading dimensions", "'size'", "a sheet tile", "dense mesh", "not one of the eight names", "a size other than",
            "a projection other than", "'front' (the default) looks from",
        })
        {
            Assert.Contains(words, description);
        }
    }

    [Fact]
    public async Task A_direction_along_the_front_view_is_the_front_view_and_zoom_changes_the_image()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var front = Png(await RenderAsync(carve, "front"));
        var along = Png(await carve.CallToolAsync("render", """{"direction":[1.6,1.2,2.2]}"""));
        var zoomed = Png(await carve.CallToolAsync("render", """{"zoom":3}"""));
        Assert.Equal(front, along);
        Assert.NotEqual(front, zoomed);
    }

    [Theory]
    [InlineData("""{"highlight":3}""", "highlight: expected an object with any of vertices, edges and faces, got 3")]
    [InlineData("""{"highlight":[1]}""", "highlight: expected an object with any of vertices, edges and faces, got")]
    [InlineData("""{"highlight":{"faces":"top"}}""", "highlight.faces: expected an array of integer ids")]
    [InlineData("""{"highlight":{"edges":[1]}}""", "highlight.edges: the 1st listed entry: expected a pair [a, b]")]
    [InlineData("""{"highlight":{"faces":[0,99]}}""", "faces[99]: the 2nd listed face: there is no face 99: ids run 0..5")]
    [InlineData("""{"highlight":{"vertices":[8]}}""", "vertices[8]: the 1st listed vertex: there is no vertex 8: ids run 0..7")]
    [InlineData("""{"highlight":{"edges":[[0,6]]}}""", "edges[0-6]: the 1st listed edge, [0, 6], is not an edge of the mesh; select with kind edges lists the real edges")]
    public async Task A_bad_highlight_is_a_tool_error_and_the_mesh_is_untouched(string arguments, string expected)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var text = ErrorText(await carve.CallToolAsync("render", arguments));

        Assert.StartsWith("render failed with 1 problem(s); the current mesh is unchanged:", text);
        Assert.Contains(expected, text);
        Assert.Equal("512x512, view front, 8 vertices, 6 faces", Caption(await RenderAsync(carve)));
    }

    [Fact]
    public async Task Every_problem_of_a_highlight_is_listed()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var text = ErrorText(await carve.CallToolAsync("render", """{"highlight":{"vertices":[9],"edges":[[0,6]],"faces":[7]}}"""));

        Assert.StartsWith("render failed with 3 problem(s)", text);
        Assert.Contains("vertices[9]", text);
        Assert.Contains("edges[0-6]", text);
        Assert.Contains("faces[7]", text);
    }

    [Fact]
    public async Task The_caption_says_the_mode_and_the_highlight()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        foreach (var (arguments, caption) in new[]
        {
            ("""{"mode":"shaded"}""", "512x512, view front, 8 vertices, 6 faces"),
            ("""{"mode":"wireframe"}""", "512x512, view front, wireframe, 8 vertices, 6 faces"),
            ("""{"mode":"normals","view":"back"}""", "512x512, view back, normals, 8 vertices, 6 faces"),
            ("""{"highlight":{}}""", "512x512, view front, 8 vertices, 6 faces"),
            ("""{"highlight":{"faces":[3]}}""", "512x512, view front, highlight 1 face, 8 vertices, 6 faces"),
            ("""{"highlight":{"edges":[[0,1],[1,0]]}}""", "512x512, view front, highlight 1 edge, 8 vertices, 6 faces"),
            ("""{"highlight":{"vertices":[6]}}""", "512x512, view front, highlight 1 vertex, 8 vertices, 6 faces"),
            ("""{"mode":"wireframe","highlight":{"faces":[1,2,3],"edges":[[0,1],[1,2]],"vertices":[6]}}""", "512x512, view front, wireframe, highlight 3 faces, 2 edges, 1 vertex, 8 vertices, 6 faces"),
            ("""{"highlight":{"faces":[3,5],"vertices":[6,7]}}""", "512x512, view front, highlight 2 faces, 2 vertices, 8 vertices, 6 faces"),
            ("""{"size":256,"view":"+x","projection":"orthographic","mode":"normals","highlight":{"faces":[5]},"zoom":2,"target":[1,0.5,0.25]}""", "256x256, view +x, orthographic, normals, highlight 1 face, zoom 2, target (1, 0.5, 0.25), 8 vertices, 6 faces"),
        })
        {
            Assert.Equal(caption, Caption(await carve.CallToolAsync("render", arguments)));
        }
    }

    [Fact]
    public async Task A_select_result_is_a_highlight_as_it_stands()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var picked = TextOf(await carve.CallToolAsync("select", """{"kind":"faces","where":"normal.y > 0.9"}"""));
        var result = await carve.CallToolAsync("render", $$"""{"highlight":{{picked}}}""");

        Assert.Equal("512x512, view front, highlight 1 face, 8 vertices, 6 faces", Caption(result));
    }

    [Fact]
    public async Task Shaded_with_an_empty_highlight_is_the_frame_of_no_arguments_even_after_the_other_looks()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);
        var plain = Png(await RenderAsync(carve));

        Assert.Equal(plain, Png(await carve.CallToolAsync("render", """{"mode":"shaded","highlight":{}}""")));
        Assert.Equal(plain, Png(await carve.CallToolAsync("render", """{"mode":"shaded","highlight":{"vertices":[],"edges":[],"faces":[]}}""")));

        var wire = Png(await carve.CallToolAsync("render", """{"mode":"wireframe","highlight":{"faces":[3],"edges":[[0,1]],"vertices":[6]}}"""));
        Assert.NotEqual(plain, wire);
        var normals = Png(await carve.CallToolAsync("render", """{"mode":"normals"}"""));
        Assert.NotEqual(plain, normals);

        Assert.Equal(plain, Png(await carve.CallToolAsync("render", """{"mode":"shaded"}""")));
    }

    /// <summary>
    /// The second render asks for the same look as the first, so only the edit can rebuild it: the frame must be the
    /// look of the moved mesh, the same bytes as a fresh process drawing the moved cube, and not the shaded one.
    /// </summary>
    [Fact]
    public async Task A_look_follows_the_mesh_through_an_edit()
    {
        const string look = """{"mode":"wireframe","highlight":{"edges":[[5,6]],"vertices":[6]}}""";
        const string moved =
            """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1.3,1.3,1.3],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);
        var before = Png(await carve.CallToolAsync("render", look));

        Assert.False(IsError(await carve.CallToolAsync("move", """{"vertices":[6],"by":[0.3,0.3,0.3]}""")));
        var after = Png(await carve.CallToolAsync("render", look));
        var shaded = Png(await carve.CallToolAsync("render", """{"highlight":{"edges":[[5,6]],"vertices":[6]}}"""));

        using var fresh = new CarveProcess([]);
        await fresh.InitializeAsync();
        await fresh.CallToolAsync("define_mesh", moved);
        var expected = Png(await fresh.CallToolAsync("render", look));

        Assert.NotEqual(before, after);
        Assert.NotEqual(shaded, after);
        Assert.Equal(expected, after);
    }

    [Fact]
    public async Task The_tool_describes_its_looks()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();

        var tools = await carve.ListToolsAsync();
        var render = tools.EnumerateArray().Single(t => t.GetProperty("name").GetString() == "render");

        var description = render.GetProperty("description").GetString()!;
        foreach (var part in new[] { "wireframe", "normals", "highlight", "hidden-line", "(normal + 1) / 2", "spike", "a mode other than", "a highlight that is not an object" })
        {
            Assert.Contains(part, description);
        }

        var schema = render.GetProperty("inputSchema").GetProperty("properties");
        Assert.Equal(["shaded", "wireframe", "normals", "checker"], schema.GetProperty("mode").GetProperty("enum").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("object", schema.GetProperty("highlight").GetProperty("type").GetString());
        Assert.Equal(["vertices", "edges", "faces"], schema.GetProperty("highlight").GetProperty("properties").EnumerateObject().Select(p => p.Name));
    }

    [Theory]
    [InlineData("""{"labels":"edges"}""", "labels: 'edges' is not a label set; use 'none', 'faces', 'vertices' or 'both'.")]
    [InlineData("""{"views":["+x"]}""", "views: 1 given; a sheet takes 2 to 6.")]
    [InlineData("""{"views":[]}""", "views: 0 given; a sheet takes 2 to 6.")]
    [InlineData("""{"views":["+x","-x","+y","-y","+z","-z","front"]}""", "views: 7 given; a sheet takes 2 to 6.")]
    [InlineData("""{"views":"+x"}""", "views: expected a list of 2 to 6 views, got")]
    [InlineData("""{"views":["+x",3]}""", "views: the 2nd listed entry: expected a view name or an object with 'view' or 'direction' and optional 'zoom' and 'target', got 3")]
    [InlineData("""{"views":["+x","top"]}""", "views: the 2nd listed entry: view: 'top' is not a view")]
    [InlineData("""{"views":["+x",{"zoom":2}]}""", "views: the 2nd listed entry: expected a view name or an object with 'view' or 'direction' and optional 'zoom' and 'target', got neither")]
    [InlineData("""{"views":["+x",{"view":"+y","roll":1}]}""", "views: the 2nd listed entry: expected a view name or an object with 'view' or 'direction' and optional 'zoom' and 'target', got 'roll'")]
    [InlineData("""{"views":["+x",{"view":"+y","direction":[1,0,0]}]}""", "views: the 2nd listed entry: view and direction: give one, not both.")]
    [InlineData("""{"views":["+x","-x",{"view":"+y","zoom":9}]}""", "views: the 3rd listed entry: zoom: 9 is outside 1 to 8.")]
    [InlineData("""{"views":["+x","-x",{"view":"+y","zoom":"far"}]}""", "views: the 3rd listed entry: zoom: expected a number")]
    [InlineData("""{"views":[{"view":"+y","target":[5,0,0]},"-x"]}""", "views: the 1st listed entry: target")]
    [InlineData("""{"views":[{"view":"+y","target":[0,0]},"-x"]}""", "views: the 1st listed entry: target: must be three finite numbers [x, y, z].")]
    [InlineData("""{"views":[{"direction":[0,0,0]},"-x"]}""", "views: the 1st listed entry: direction: must not be zero")]
    [InlineData("""{"views":[{"direction":"up"},"-x"]}""", "views: the 1st listed entry: direction: must be three finite numbers [x, y, z].")]
    [InlineData("""{"views":["+x","-x"],"columns":4}""", "columns: 4 is not 1, 2 or 3.")]
    [InlineData("""{"views":["+x","-x"],"columns":0}""", "columns: 0 is not 1, 2 or 3.")]
    [InlineData("""{"views":["+x","-x"],"columns":1.5}""", "columns: 1.5 is not 1, 2 or 3.")]
    [InlineData("""{"views":["+x","-x"],"view":"front"}""", "views: not with view, direction, zoom or target")]
    [InlineData("""{"views":["+x","-x"],"direction":[1,0,0]}""", "views: not with view, direction, zoom or target")]
    [InlineData("""{"views":["+x","-x"],"zoom":2}""", "views: not with view, direction, zoom or target")]
    [InlineData("""{"views":["+x","-x"],"target":[0,0,0]}""", "views: not with view, direction, zoom or target")]
    public async Task Bad_labels_and_views_are_tool_errors(string arguments, string says)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        Assert.Contains(says, ErrorText(await carve.CallToolAsync("render", arguments)));
    }

    [Fact]
    public async Task The_captions_of_labels_and_sheets()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        foreach (var (arguments, caption) in new[]
        {
            ("""{"labels":"none"}""", "512x512, view front, 8 vertices, 6 faces"),
            ("""{"labels":"both"}""", "512x512, view front, labels both, 8 vertices, 6 faces"),
            ("""{"labels":"faces","mode":"wireframe","projection":"orthographic","highlight":{"faces":[1]},"zoom":2,"size":256}""", "256x256, view front, orthographic, wireframe, labels faces, highlight 1 face, zoom 2, 8 vertices, 6 faces"),
            ("""{"views":["+x","-x","+y","-y","+z","-z"],"projection":"orthographic","size":256}""", "3x2 tiles of 256x256, orthographic, views +x, -x, +y, -y, +z, -z, 8 vertices, 6 faces"),
            ("""{"views":["front","+x","-x","+y","-y","+z"],"projection":"orthographic","size":256}""", "3x2 tiles of 256x256, orthographic, views front, +x, -x, +y, -y, +z, 8 vertices, 6 faces"),
            ("""{"views":["front",{"direction":[2,1,1]}],"columns":1,"mode":"wireframe","labels":"faces","size":256,"highlight":{"faces":[1]}}""", "1x2 tiles of 256x256, wireframe, labels faces, highlight 1 face, views front, (2, 1, 1), 8 vertices, 6 faces"),
            ("""{"views":["front",{"direction":[2,1,1],"zoom":2,"target":[1,0.5,0.5]},{"view":"+x","zoom":3}],"columns":2,"size":256}""", "2x2 tiles of 256x256, views front, (2, 1, 1), zoom 2, target (1, 0.5, 0.5), +x, zoom 3, 8 vertices, 6 faces"),
        })
        {
            Assert.Equal(caption, Caption(await carve.CallToolAsync("render", arguments)));
        }
    }

    [Theory]
    [InlineData("""{"views":["+x","-x","+y","-y","+z","-z"],"size":256}""", 772, 514)]
    [InlineData("""{"views":["+x","-x","+y","-y"],"size":256,"columns":2}""", 514, 514)]
    [InlineData("""{"views":["+x","-x"],"size":256}""", 514, 256)]
    [InlineData("""{"views":["+x","-x"],"size":256,"columns":3}""", 772, 256)]
    [InlineData("""{"views":["+x","-x","+y"],"size":256,"columns":1}""", 256, 772)]
    [InlineData("""{"views":["+x","-x"],"size":512,"columns":1}""", 512, 1026)]
    [InlineData("""{"views":["+x","-x"],"size":1024}""", 2050, 1024)]
    public async Task A_sheet_is_the_size_its_tiles_make(string arguments, int width, int height)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var image = ImageResult.FromMemory(Png(await carve.CallToolAsync("render", arguments)), ColorComponents.RedGreenBlueAlpha);

        Assert.Equal((width, height), (image.Width, image.Height));
    }

    [Fact]
    public async Task Labels_none_is_the_same_render_as_no_labels()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var without = Png(await carve.CallToolAsync("render", """{"size":256}"""));
        var none = Png(await carve.CallToolAsync("render", """{"size":256,"labels":"none"}"""));

        Assert.Equal(without, none);
    }

    /// <summary>The first frame of all, labelled: the surface does not exist until a frame has been drawn.</summary>
    [Fact]
    public async Task A_first_render_with_labels_draws_them_and_what_follows_is_unmarked()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var labelled = Png(await carve.CallToolAsync("render", """{"labels":"both","size":256}"""));
        var plain = Png(await carve.CallToolAsync("render", """{"size":256}"""));
        var again = Png(await carve.CallToolAsync("render", """{"labels":"both","size":256}"""));

        Assert.NotEqual(plain, labelled);
        Assert.Equal(labelled, again);

        using var fresh = new CarveProcess([]);
        await fresh.InitializeAsync();
        await fresh.CallToolAsync("define_mesh", Cube);
        Assert.Equal(plain, Png(await fresh.CallToolAsync("render", """{"size":256}""")));
    }

    /// <summary>A sheet's first tile, likewise: its caption needs the surface too.</summary>
    [Fact]
    public async Task A_first_render_may_be_a_sheet()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var image = ImageResult.FromMemory(Png(await carve.CallToolAsync("render", """{"views":["+x","-x"],"size":256}""")), ColorComponents.RedGreenBlueAlpha);

        var caption = Enumerable.Range(0, 16 * 16).Select(k => image.Data[(((4 + k / 16) * image.Width) + 4 + k % 16) * 4]);
        Assert.Contains(caption, red => red >= 250);
    }

    /// <summary>Labels, the mode and the highlight reach every tile: each tile differs from the same tile without them, away from its caption.</summary>
    [Theory]
    [InlineData("labels", "\"faces\"")]
    [InlineData("mode", "\"wireframe\"")]
    [InlineData("highlight", """{"faces":[0,1,2,3,4,5]}""")]
    public async Task What_applies_to_a_sheet_applies_to_every_tile(string argument, string value)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var plain = ImageResult.FromMemory(Png(await carve.CallToolAsync("render", """{"views":["front","back"],"size":256}""")), ColorComponents.RedGreenBlueAlpha);
        var asked = ImageResult.FromMemory(Png(await carve.CallToolAsync("render", $$$"""{"views":["front","back"],"size":256,"{{{argument}}}":{{{value}}}}""")), ColorComponents.RedGreenBlueAlpha);

        foreach (var left in new[] { 0, 258 })
        {
            var differ = 0;
            for (var y = 30; y < 256; y++)
            for (var x = left; x < left + 256; x++)
            {
                var at = (y * plain.Width + x) * 4;
                differ += plain.Data[at + 1] != asked.Data[at + 1] ? 1 : 0;
            }

            Assert.True(differ > 100, $"the tile at {left} differs in {differ} pixels");
        }
    }

    /// <summary>A client that sends nulls, or fills in the schema's defaults, still gets a sheet.</summary>
    [Theory]
    [InlineData("""{"views":["+x","-x"],"size":256,"view":null}""")]
    [InlineData("""{"views":["+x","-x"],"size":256,"view":null,"direction":null,"target":null}""")]
    [InlineData("""{"views":["+x","-x"],"size":256,"zoom":1}""")]
    [InlineData("""{"views":["+x","-x"],"size":256,"view":null,"direction":null,"zoom":1,"target":null}""")]
    public async Task Views_with_null_single_view_arguments_or_zoom_1_is_a_sheet(string arguments)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        Assert.Equal("2x1 tiles of 256x256, views +x, -x, 8 vertices, 6 faces", Caption(await carve.CallToolAsync("render", arguments)));
    }

    /// <summary>
    /// The look's lines and the highlight's orange reach the camera of every size, not only 512's: the wireframe's light
    /// grey edges and an orange edge are counted at each.
    /// </summary>
    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public async Task The_looks_are_drawn_at_every_size(int size)
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var image = Decode(Png(await carve.CallToolAsync("render", $$$"""{"mode":"wireframe","size":{{{size}}},"highlight":{"edges":[[4,5]]}}""")), $"wireframe-highlight-{size}", size);

        int grey = 0, orange = 0;
        for (var at = 0; at < image.Data.Length; at += 4)
        {
            var (r, g, b) = (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
            grey += r > 180 && g > 180 && b > 180 && Math.Abs(r - b) < 12 ? 1 : 0;
            orange += r > 200 && g is > 60 and < 210 && b < 100 ? 1 : 0;
        }

        Assert.True(grey > size, $"{grey} light grey edge pixels at {size}");
        Assert.True(orange > size / 8, $"{orange} orange pixels at {size}");
    }

    [Fact]
    public async Task A_single_render_has_no_tile_caption()
    {
        using var carve = new CarveProcess([]);
        await carve.InitializeAsync();
        await carve.CallToolAsync("define_mesh", Cube);

        var image = Decode(Png(await carve.CallToolAsync("render", """{"view":"+x","size":256}""")), "single-no-caption", 256);

        AssertBackground(At(image, (6, 8)));
    }
}

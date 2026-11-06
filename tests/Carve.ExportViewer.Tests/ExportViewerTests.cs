using System.Text.Json;
using System.Text.RegularExpressions;
using StbImageSharp;
using Xunit;

namespace Carve.ExportViewer.Tests;

/// <summary>A decoded PNG read through a pixel function, so a check can be run on the mirror of a real frame.</summary>
internal sealed class Frame
{
    private readonly Func<int, int, (int R, int G, int B)> _pixel;

    private Frame(string name, int width, int height, Func<int, int, (int R, int G, int B)> pixel)
    {
        Name = name;
        Width = width;
        Height = height;
        _pixel = pixel;
    }

    public string Name { get; }

    public int Width { get; }

    public int Height { get; }

    public (int R, int G, int B) Pixel(int x, int y) => _pixel(x, y);

    public static Frame Load(string path)
    {
        var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
        return new Frame(Path.GetFileName(path), image.Width, image.Height, (x, y) =>
        {
            var at = (y * image.Width + x) * 4;
            return (image.Data[at], image.Data[at + 1], image.Data[at + 2]);
        });
    }

    /// <summary>The frame as a camera with +X to its left would have drawn it.</summary>
    public Frame Mirrored() => new(Name + " (mirrored)", Width, Height, (x, y) => _pixel(Width - 1 - x, y));

    /// <summary>The frame as a camera with its up turned over would have drawn it.</summary>
    public Frame UpFlipped() => new(Name + " (up flipped)", Width, Height, (x, y) => _pixel(x, Height - 1 - y));
}

/// <summary>
/// What a game's look at the exported crate must show: the checks after the viewer, in suite <c>scenario-export</c>.
/// The run folder comes from <c>CARVE_EXPORT_RUN</c>, which <c>scripts/run-export-scenario.ps1</c> sets. Each
/// check is a function returning what it found wrong, so the same check can be run on a frame made wrong on purpose.
/// </summary>
internal static class Checks
{
    /// <summary>The 10/255 of M-1: two things that must look different differ by at least this in some channel.</summary>
    public const int MinimumStep = 10;

    // ---- sampled pixels (x, y from the top left of a 512 x 512 frame), picked from the first good run ----
    // Each sits well inside one flat face, at least 8 pixels from any edge. The shades in that run (red channel):
    // front: top 207, +Z 191, +X 171, a bar's top 206; minus-x: top 207, -X 125, +Z 191; background 0.
    // The +Z panel and the +Z frame are the same shade, since the sun is one fixed light and nothing casts a
    // shadow: the recess shows as its wall, so the recess check samples the wall between frame and panel.

    // front: the top, +Z and +X frame faces, brightest first. The top frame's left band; the +Z frame's left
    // band; the +X frame below the bar, on its near band.
    public static readonly (int X, int Y) FrontTop = (95, 150);
    public static readonly (int X, int Y) FrontPlusZFrame = (72, 300);
    public static readonly (int X, int Y) FrontPlusX = (306, 330);

    // front: the recess wall that stands between the +Z frame's left band and the +Z panel (170, facing +X, in
    // shade where the frame and the panel are 191): this is where the recess shows.
    public static readonly (int X, int Y) FrontRecessWall = (91, 300);

    // front: the +X bar's top (206, lit like the lid) and the +X panel above it (171): they differ only because
    // the bar stands there.
    public static readonly (int X, int Y) FrontBarTop = (350, 290);
    public static readonly (int X, int Y) FrontPanelBehindBar = (350, 240);

    // front: five points across the +Z panel, which is one flat face (191), so smooth normals would show.
    public static readonly (int X, int Y)[] FrontPlusZPanel = [(110, 300), (150, 300), (200, 300), (250, 300), (285, 300)];

    // minus-x: the -X panel (125) on the left and the +Z face (191) on the right: a mirrored view swaps them.
    public static readonly (int X, int Y) MinusXPanel = (150, 250);
    public static readonly (int X, int Y) MinusXPlusZ = (350, 300);

    // minus-x: the -X bar's top (206) and the -X panel above it (125).
    public static readonly (int X, int Y) MinusXBarTop = (210, 300);
    public static readonly (int X, int Y) MinusXPanelBehindBar = (210, 250);

    // minus-x: the lid (207), which an upside-down view would put at the bottom.
    public static readonly (int X, int Y) MinusXTop = (250, 160);

    public static int Gap((int R, int G, int B) a, (int R, int G, int B) b) =>
        Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    private static bool IsBackground((int R, int G, int B) p) => p.R <= 2 && p.G <= 2 && p.B <= 2;

    public static List<string> Corners(Frame frame)
    {
        var found = new List<string>();
        foreach (var (x, y) in new[] { (0, 0), (frame.Width - 1, 0), (0, frame.Height - 1), (frame.Width - 1, frame.Height - 1) })
        {
            if (!IsBackground(frame.Pixel(x, y)))
            {
                found.Add($"{frame.Name}: the corner ({x}, {y}) is {frame.Pixel(x, y)}, not the background");
            }
        }

        return found;
    }

    /// <summary><paramref name="brighter"/> is at least a step above <paramref name="darker"/>; both are on the model.</summary>
    public static List<string> Above(Frame frame, (int X, int Y) brighter, string whatBrighter, (int X, int Y) darker, string whatDarker)
    {
        var (a, b) = (frame.Pixel(brighter.X, brighter.Y), frame.Pixel(darker.X, darker.Y));
        var found = new List<string>();
        if (IsBackground(b))
        {
            found.Add($"{frame.Name}: {whatDarker} at {darker} is the background");
        }

        if (a.R < b.R + MinimumStep)
        {
            found.Add($"{frame.Name}: {whatBrighter} {a.R} should be at least {MinimumStep} above {whatDarker} {b.R}");
        }

        return found;
    }

    public static List<string> Differ(Frame frame, (int X, int Y) a, string whatA, (int X, int Y) b, string whatB)
    {
        var found = new List<string>();
        foreach (var (at, what) in new[] { (a, whatA), (b, whatB) })
        {
            if (IsBackground(frame.Pixel(at.X, at.Y)))
            {
                found.Add($"{frame.Name}: {what} at {at} is the background");
            }
        }

        var gap = Gap(frame.Pixel(a.X, a.Y), frame.Pixel(b.X, b.Y));
        if (gap < MinimumStep)
        {
            found.Add($"{frame.Name}: {whatA} and {whatB} differ by {gap}/255, at least {MinimumStep} wanted");
        }

        return found;
    }

    /// <summary>The front's frame faces, brightest first (top, +Z, +X), each a step above the next and +X a step above the background.</summary>
    public static List<string> FrontFaceOrder(Frame frame)
    {
        var found = Above(frame, FrontTop, "the top", FrontPlusZFrame, "+Z");
        found.AddRange(Above(frame, FrontPlusZFrame, "+Z", FrontPlusX, "+X"));
        if (frame.Pixel(FrontPlusX.X, FrontPlusX.Y).R < MinimumStep)
        {
            found.Add($"{frame.Name}: +X at {FrontPlusX} is not a step above the background");
        }

        return found;
    }

    public static List<string> FrontFrame(Frame frame)
    {
        var found = Corners(frame);
        found.AddRange(FrontFaceOrder(frame));
        found.AddRange(Differ(frame, FrontBarTop, "the +X bar's top", FrontPanelBehindBar, "the +X panel behind it"));
        found.AddRange(Differ(frame, FrontRecessWall, "the recess wall", FrontPlusZFrame, "the +Z frame"));
        found.AddRange(FlatPlusZPanel(frame));
        return found;
    }

    /// <summary>The +Z panel is one flat shade; a face lit by smooth normals runs from one value to another.</summary>
    public static List<string> FlatPlusZPanel(Frame frame)
    {
        var shades = FrontPlusZPanel.Select(p => frame.Pixel(p.X, p.Y).R).ToArray();
        return shades.Max() - shades.Min() > 2
            ? [$"{frame.Name}: the +Z panel is not one flat shade: {string.Join(", ", shades)}"]
            : [];
    }

    public static List<string> MinusXFrame(Frame frame)
    {
        var found = Corners(frame);

        // -X is on the left and darker, +Z on the right and brighter, as the agreed view has them; a mirrored view swaps them.
        found.AddRange(Above(frame, MinusXPlusZ, "+Z", MinusXPanel, "-X"));
        found.AddRange(Above(frame, MinusXTop, "the top", MinusXPlusZ, "+Z"));
        found.AddRange(Differ(frame, MinusXBarTop, "the -X bar's top", MinusXPanelBehindBar, "the -X panel behind it"));
        return found;
    }
}

[Trait("Tier", "regression")]
public sealed class ExportViewerTests
{
    private static readonly string Run = Environment.GetEnvironmentVariable("CARVE_EXPORT_RUN")
        ?? throw new InvalidOperationException("CARVE_EXPORT_RUN is not set; run this through scripts/run-export-scenario.ps1");

    private static string Viewer(string file) => Path.Combine(Run, "viewer", file);

    [Fact]
    public void View_json_matches_what_was_exported()
    {
        var exported = File.ReadAllText(Path.Combine(Run, "export-result.txt"));
        var triangles = int.Parse(Regex.Match(exported, @"(\d+) triangles").Groups[1].Value);

        using var json = JsonDocument.Parse(File.ReadAllText(Viewer("view.json")));
        var root = json.RootElement;
        Assert.Equal(triangles, root.GetProperty("triangles").GetInt32());

        var materials = root.GetProperty("materials");
        Assert.Equal(1, materials.GetArrayLength());
        var material = materials[0];
        Assert.Equal([0.72, 0.72, 0.70, 1], material.GetProperty("baseColor").EnumerateArray().Select(v => v.GetDouble()).ToArray(), new Tolerance());
        Assert.Equal(0, material.GetProperty("metallic").GetDouble());
        Assert.Equal(1, material.GetProperty("roughness").GetDouble());

        var bounds = root.GetProperty("bounds");
        Assert.Equal([-0.02, 0, 0], bounds.GetProperty("min").EnumerateArray().Select(v => v.GetDouble()).ToArray(), new Tolerance());
        Assert.Equal([1.02, 0.8, 1], bounds.GetProperty("max").EnumerateArray().Select(v => v.GetDouble()).ToArray(), new Tolerance());
    }

    [Fact]
    public void The_front_frame_shows_the_faces_the_bar_and_the_recess()
    {
        Assert.Empty(Checks.FrontFrame(Frame.Load(Viewer("front.png"))));
    }

    [Fact]
    public void The_minus_x_frame_shows_minus_x_on_the_left_and_the_bar()
    {
        Assert.Empty(Checks.MinusXFrame(Frame.Load(Viewer("minus-x.png"))));
    }

    [Fact]
    public void A_mirrored_or_up_flipped_view_would_fail_a_check_in_each_frame()
    {
        // The crate is symmetric about x = 0.5, so each view is checked on its own mirror and its own upside-down
        // image: the same checks that pass above must find something wrong.
        foreach (var (frame, check) in new (Frame, Func<Frame, List<string>>)[]
                 {
                     (Frame.Load(Viewer("front.png")), Checks.FrontFrame),
                     (Frame.Load(Viewer("minus-x.png")), Checks.MinusXFrame),
                 })
        {
            Assert.NotEmpty(check(frame.Mirrored()));
            Assert.NotEmpty(check(frame.UpFlipped()));
        }

        // And the specific check that tells a mirror apart, which a symmetric crate would otherwise hide.
        Assert.NotEmpty(Checks.FrontFaceOrder(Frame.Load(Viewer("front.png")).Mirrored()));
        Assert.NotEmpty(Checks.Above(Frame.Load(Viewer("minus-x.png")).Mirrored(), Checks.MinusXPlusZ, "+Z", Checks.MinusXPanel, "-X"));
    }

    private sealed class Tolerance : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-4;

        public int GetHashCode(double obj) => 0;
    }
}

using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Crate;

/// <summary>
/// The wireframe, normals and highlight frames of <c>render</c>, on the finished crate and then the cube.
/// Each check is at a point of the mesh projected by <see cref="CameraPixels"/>, so a mirrored or flipped
/// camera puts what is looked for somewhere else; the crate is symmetric about x = 0.5, so what tells a
/// mirror is the corner view's own asymmetry, the tilted direction and the handle on the +X side.
/// </summary>
internal static class CrateLooks
{
    private const int Size = 512;

    internal static readonly Vector3 CrateMin = new(-0.02f, 0, 0);
    internal static readonly Vector3 CrateMax = new(1.02f, 0.8f, 1);
    internal static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f);
    internal static readonly Vector3 Tilted = new(0.4f, 2, 0.8f);

    // Linear values as the task gives them, and how each looks in a frame.
    private static readonly (int R, int G, int B) Fill = Srgb(0.09, 0.095, 0.10);
    private static readonly (int R, int G, int B) Edge = Srgb(0.85, 0.85, 0.85);
    private static readonly (int R, int G, int B) Orange = Srgb(1, 0.55, 0.1);
    private static readonly (int R, int G, int B) PlusX = Srgb(1, 0.5, 0.5);
    private static readonly (int R, int G, int B) PlusY = Srgb(0.5, 1, 0.5);
    private static readonly (int R, int G, int B) PlusZ = Srgb(0.5, 0.5, 1);
    private static readonly (int R, int G, int B) MinusX = Srgb(0, 0.5, 0.5);
    private static readonly (int R, int G, int B) MinusY = Srgb(0.5, 0, 0.5);
    private static readonly (int R, int G, int B) MinusZ = Srgb(0.5, 0.5, 0);

    // From the front the recess's near walls hide the panel's strips along +X and +Z, so its corners as
    // seen are these: the far corners of the panel, and the ends of the strips' edges, then the middle.
    private static readonly Vector3[] PanelSeenFromFront =
    [
        new(0.16f, 0.72f, 0.16f), new(0.72f, 0.72f, 0.16f), new(0.16f, 0.72f, 0.66f), new(0.72f, 0.72f, 0.66f), new(0.45f, 0.72f, 0.4f),
    ];

    // [0.4, 2, 0.8] puts the eye at x = 1.067, barely off the plane of the +X frame, so that frame is a
    // sliver one to three pixels wide: these are points of it that fill a pixel, at both ends of its top
    // band and along its band at the +Z end. Most other points of it fall on the sliver's blended edge.
    private static readonly Vector3[] PlusXFrameSeenFromAbove =
    [
        new(1, 0.74f, 0.1f), new(1, 0.72f, 0.15f), new(1, 0.74f, 0.8f), new(1, 0.72f, 0.85f),
        new(1, 0.3f, 0.96f), new(1, 0.6f, 0.96f), new(1, 0.2f, 0.92f),
    ];

    /// <summary>The flat colours are within this of the sRGB value; a line pixel partly covers its neighbours.</summary>
    private const int Exact = 2;

    internal sealed record Shape(Vector3[] Vertices, int[][] Faces)
    {
        public Vector3 Centre(int face) => Faces[face].Aggregate(Vector3.Zero, (sum, v) => sum + Vertices[v]) / Faces[face].Length;

        /// <summary>Near each corner of a face and part way to its centre: flat colour, clear of the edges and of the face's spike.</summary>
        public IEnumerable<Vector3> Samples(int face) =>
            Faces[face].SelectMany(v => new[] { Vector3.Lerp(Vertices[v], Centre(face), 0.3f), Vector3.Lerp(Vertices[v], Centre(face), 0.6f) });

        public int Find(Func<Vector3, bool> every) => Enumerable.Range(0, Faces.Length).Single(f => Faces[f].All(v => every(Vertices[v])));

        public int Vertex(Func<Vector3, bool> at) => Enumerable.Range(0, Vertices.Length).Single(v => at(Vertices[v]));

        public int[] FindAll(Func<Vector3, bool> every) => Enumerable.Range(0, Faces.Length).Where(f => Faces[f].All(v => every(Vertices[v]))).ToArray();
    }

    public static async Task RunAsync(ScenarioDriver driver, ScenarioFrame shadedFront, ScenarioFrame shadedTilted)
    {
        var crate = await ReadAsync(driver);
        var frontPixel = Perspective(CrateMin, CrateMax, Front, Size);
        var tiltedPixel = Perspective(CrateMin, CrateMax, Tilted, Size);

        const float Near = 0.001f;
        var lidPanel = TopFace;
        var plusZBand = crate.Find(p => p.Z > 1 - Near && p.Y > 0.68f - Near);
        var plusXBand = crate.Find(p => Math.Abs(p.X - 1) < Near && p.Y > 0.68f - Near);
        var lidBand = crate.FindAll(p => p.Y > 0.8f - Near).First(f => crate.Faces[f].Any(v => crate.Vertices[v].Z > 1 - Near));

        // The lid's recess: its panel and the four walls round it.
        var recess = crate.FindAll(p => p.Y > 0.72f - Near && p.X > 0.12f - Near && p.X < 0.88f + Near && p.Z > 0.12f - Near && p.Z < 0.88f + Near);
        if (recess.Length != 5 || !recess.Contains(lidPanel))
        {
            driver.Fail($"the lid's recess: expected the panel and four walls, found faces {string.Join(", ", recess)}");
        }

        // The +X bar: its top is the level face standing out past x = 1; a corner away from that face has the star alone.
        var barTop = crate.Find(p => p.X > 0.92f - Near && Math.Abs(p.Y - 0.46f) < Near);
        var barLoop = crate.Faces[barTop];
        int[][] barEdges = [.. barLoop.Select((v, k) => new[] { v, barLoop[(k + 1) % barLoop.Length] })];
        var barOuter = barLoop.Select(v => crate.Vertices[v]).Where(p => p.X > 1).OrderBy(p => p.Z).ToArray();
        var cornerId = Enumerable.Range(0, crate.Vertices.Length)
            .Single(v => crate.Vertices[v].X > 1 && crate.Vertices[v].Y < 0.4f && crate.Vertices[v].Z > 0.8f);
        var corner = crate.Vertices[cornerId];
        var highlight = new { faces = recess, edges = barEdges, vertices = new[] { cornerId } };

        // ---- the crate, front, wireframe ----
        var wireframe = await driver.CaptureAsync("crate-wireframe-front", new { view = "front", mode = "wireframe" });
        CheckCaption(driver, wireframe, "512x512, view front, wireframe, 72 vertices, 70 faces");
        CheckCorners(driver, wireframe);
        CheckPoints(driver, wireframe, frontPixel, PanelSeenFromFront, Fill, "the lid panel");
        CheckFace(driver, wireframe, frontPixel, crate, plusZBand, Fill, "the +Z frame");
        CheckFace(driver, wireframe, frontPixel, crate, plusXBand, Fill, "the +X frame");

        // The frame's top-front edge at (x, 0.8, 1), and the same on the +Z end of the +X side.
        foreach (var t in new[] { 0.2f, 0.4f, 0.6f, 0.8f })
        {
            CheckLine(driver, wireframe, frontPixel(new Vector3(t, 0.8f, 1)), Edge, $"the top-front edge at x = {t}");
        }

        // Edges in concave corners: where the lid's panel meets its far wall, and the +Z panel its bottom wall.
        foreach (var t in new[] { 0.3f, 0.5f, 0.7f })
        {
            CheckLine(driver, wireframe, frontPixel(new Vector3(t, 0.72f, 0.12f)), Edge, $"the lid panel's far edge at x = {t}");
            CheckLine(driver, wireframe, frontPixel(new Vector3(t, 0.12f, 0.92f)), Edge, $"the +Z panel's bottom edge at x = {t}");
        }

        // The bottom-back edge is behind the whole crate; it falls on the lid's frame band or a side.
        var hidden = new Vector3(0.5f, 0, 0);
        CheckColour(driver, wireframe, Holding(frontPixel(hidden)), Fill, Exact, "the hidden bottom-back edge's projection");

        // ---- the crate, tilted, normals ----
        var normals = await driver.CaptureAsync("crate-normals-dir-0.4-2-0.8", new { direction = new[] { 0.4, 2, 0.8 }, mode = "normals" });
        CheckCaption(driver, normals, "512x512, direction (0.4, 2, 0.8), normals, 72 vertices, 70 faces");
        CheckCorners(driver, normals);
        CheckFace(driver, normals, tiltedPixel, crate, lidPanel, PlusY, "the lid panel");
        CheckFace(driver, normals, tiltedPixel, crate, plusZBand, PlusZ, "the +Z frame");
        CheckPoints(driver, normals, tiltedPixel, PlusXFrameSeenFromAbove, PlusX, "the +X frame");

        // A spike of the lid: on it most of the way up, and past its tip the lid again. Not the panel's own: that
        // stands on the image's centre column, so it runs down a pixel boundary and no pixel of it is whole.
        var spikeFoot = crate.Centre(lidBand);
        var spikeTop = spikeFoot + Vector3.UnitY * 0.1f * (CrateMax - CrateMin).Length() / 2;
        CheckLineAlong(driver, normals, tiltedPixel(Vector3.Lerp(spikeFoot, spikeTop, 0.3f)), tiltedPixel(Vector3.Lerp(spikeFoot, spikeTop, 0.9f)), Edge, "the spike of the lid's +Z band");
        CheckColour(driver, normals, Holding(tiltedPixel(Vector3.Lerp(spikeFoot, spikeTop, 1.5f))), PlusY, Exact, "past the tip of that spike");

        // ---- the crate, front, shaded, highlighted ----
        var picked = await driver.CaptureAsync("crate-highlight-front", new { view = "front", highlight });
        CheckCaption(driver, picked, "512x512, view front, highlight 5 faces, 4 edges, 1 vertex, 72 vertices, 70 faces");
        CheckCorners(driver, picked);
        foreach (var at in PanelSeenFromFront)
        {
            var p = picked.Pixel(Holding(frontPixel(at)).X, Holding(frontPixel(at)).Y);
            if (p.R - p.B < 60)
            {
                driver.Fail($"{picked.Name}: the lid panel at {at} is {p}: R - B should be at least 60");
            }
        }

        foreach (var at in crate.Samples(lidBand))
        {
            var p = picked.Pixel(Holding(frontPixel(at)).X, Holding(frontPixel(at)).Y);
            if (Math.Abs(p.R - p.B) > 6)
            {
                driver.Fail($"{picked.Name}: the frame at {at} is {p}: it is not highlighted, so |R - B| should be at most 6");
            }
        }

        var (barFar, barNear) = (Vector3.Lerp(barOuter[0], barOuter[1], 0.3f), Vector3.Lerp(barOuter[0], barOuter[1], 0.7f));
        CheckLineAlong(driver, picked, frontPixel(barFar), frontPixel(barNear), Orange, "the +X bar's outer top edge");

        // An edge facing the eye that is not asked for is not orange: the frame's top-front edge at (x, 0.8, 1).
        var (frontFrom, frontTo) = (new Vector3(0.2f, 0.8f, 1), new Vector3(0.8f, 0.8f, 1));
        CheckNotLineAlong(driver, picked, frontPixel(frontFrom), frontPixel(frontTo), Orange, "the unselected top-front edge");

        // The star's arm along +X stands clear of the mesh, over the bar's own end: half way out.
        var armLength = 0.02f * (CrateMax - CrateMin).Length() / 2;
        CheckLineAlong(driver, picked, frontPixel(corner + Vector3.UnitX * armLength * 0.3f), frontPixel(corner + Vector3.UnitX * armLength * 0.9f), Orange, "the star's +X arm");

        // ---- the crate, front, wireframe, the same highlight ----
        var pickedWire = await driver.CaptureAsync("crate-highlight-wireframe-front", new { view = "front", mode = "wireframe", highlight });
        CheckCaption(driver, pickedWire, "512x512, view front, wireframe, highlight 5 faces, 4 edges, 1 vertex, 72 vertices, 70 faces");
        CheckCorners(driver, pickedWire);
        CheckPoints(driver, pickedWire, frontPixel, PanelSeenFromFront, Orange, "the lid panel");
        CheckFace(driver, pickedWire, frontPixel, crate, plusZBand, Fill, "an unhighlighted face");
        CheckLineAlong(driver, pickedWire, frontPixel(barFar), frontPixel(barNear), Orange, "the +X bar's outer top edge");
        CheckNotLineAlong(driver, pickedWire, frontPixel(frontFrom), frontPixel(frontTo), Orange, "the unselected top-front edge");

        // ---- errors, and the shaded frame back as it was ----
        await ExpectToolErrorAsync(driver, "render", new { mode = "xray" }, "mode", "xray");
        await ExpectToolErrorAsync(driver, "render", new { highlight = new { faces = new[] { 99 } } }, "faces[99]", "no face 99");
        var shadedAgain = await driver.CaptureAsync("crate-front-after-looks", new { view = "front" });
        driver.AssertBytesEqual("crate front, after the looks and their errors", shadedFront.Pixels, shadedAgain.Pixels);

        // ---- labels and sheets, while the mesh is still the crate ----
        await CrateLabels.RunAsync(driver, crate, shadedFront, shadedTilted);

        // ---- the cube, normals, back ----
        await DefineAsync(driver);
        var cube = await ReadAsync(driver);
        var backPixel = Perspective(Vector3.Zero, Vector3.One, -Front, Size);
        var cubeBack = await driver.CaptureAsync("cube-normals-back", new { view = "back", mode = "normals" });
        CheckCaption(driver, cubeBack, "512x512, view back, normals, 8 vertices, 6 faces");
        CheckCorners(driver, cubeBack);
        CheckFace(driver, cubeBack, backPixel, cube, cube.Find(p => p.Y < 0.5f), MinusY, "the underside");
        CheckFace(driver, cubeBack, backPixel, cube, cube.Find(p => p.Z < 0.5f), MinusZ, "the -Z side");
        CheckFace(driver, cubeBack, backPixel, cube, MinusXFace, MinusX, "the -X side");
    }

    private static async Task<Shape> ReadAsync(ScenarioDriver driver)
    {
        var mesh = await CallJsonAsync(driver, "get_mesh", new { });
        var vertices = mesh.GetProperty("vertices").EnumerateArray().Select(v =>
        {
            var at = v.GetProperty("at");
            return new Vector3(at[0].GetSingle(), at[1].GetSingle(), at[2].GetSingle());
        }).ToArray();
        var faces = mesh.GetProperty("faces").EnumerateArray()
            .Select(f => f.GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
        return new Shape(vertices, faces);
    }

    /// <summary>How a linear colour reads in a frame, each channel 0 to 255.</summary>
    private static (int R, int G, int B) Srgb(double r, double g, double b) => (Encode(r), Encode(g), Encode(b));

    /// <summary>One linear channel as a frame shows it, 0 to 255.</summary>
    internal static int Encode(double c) =>
        (int)Math.Round(255 * (c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055));

    internal static void CheckCaption(ScenarioDriver driver, ScenarioFrame frame, string expected)
    {
        if (frame.Caption != expected)
        {
            driver.Fail($"{frame.Name}: the caption is '{frame.Caption}', expected '{expected}'");
        }
    }

    private static void CheckPoints(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, IEnumerable<Vector3> points, (int R, int G, int B) colour, string what)
    {
        foreach (var at in points)
        {
            CheckColour(driver, frame, Holding(pixel(at)), colour, Exact, $"{what} at {at}");
        }
    }

    private static void CheckFace(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, Shape shape, int face, (int R, int G, int B) colour, string what)
    {
        CheckPoints(driver, frame, pixel, shape.Samples(face), colour, $"{what} (face {face})");
    }

    private static void CheckColour(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, (int R, int G, int B) colour, int within, string what)
    {
        var p = frame.Pixel(at.X, at.Y);
        if (Math.Abs(p.R - colour.R) > within || Math.Abs(p.G - colour.G) > within || Math.Abs(p.B - colour.B) > within)
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is {p}, expected {colour} within {within}");
        }
    }

    /// <summary>
    /// A line is one pixel wide and rarely centred on a pixel, so its coverage is split and on a slope only
    /// some of its pixels are whole. Of the pixels touching the projected stretch of it, the closest in
    /// colour must be the line's, within 10/255.
    /// </summary>
    private static void CheckLineAlong(ScenarioDriver driver, ScenarioFrame frame, Vector2 from, Vector2 to, (int R, int G, int B) colour, string what)
    {
        var best = Closest(frame, from, to, colour);
        if (best.Off > 10)
        {
            driver.Fail($"{frame.Name}: {what}, {from} to {to}: the closest pixel to {colour} is {best.P} at ({best.X}, {best.Y})");
        }
    }

    /// <summary>The opposite of <see cref="CheckLineAlong"/>: no pixel touching the stretch is within 10/255 of <paramref name="colour"/>.</summary>
    private static void CheckNotLineAlong(ScenarioDriver driver, ScenarioFrame frame, Vector2 from, Vector2 to, (int R, int G, int B) colour, string what)
    {
        var best = Closest(frame, from, to, colour);
        if (best.Off <= 10)
        {
            driver.Fail($"{frame.Name}: {what}, {from} to {to}: the pixel at ({best.X}, {best.Y}) is {best.P}, within 10 of {colour}: it should not be drawn in that colour");
        }
    }

    private static (int Off, int X, int Y, (byte R, byte G, byte B, byte A) P) Closest(ScenarioFrame frame, Vector2 from, Vector2 to, (int R, int G, int B) colour)
    {
        const int Steps = 16;
        var best = (Off: int.MaxValue, X: 0, Y: 0, P: default((byte R, byte G, byte B, byte A)));
        for (var step = 0; step <= Steps; step++)
        {
            var (x, y) = Holding(Vector2.Lerp(from, to, (float)step / Steps));
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var p = frame.Pixel(x + dx, y + dy);
                var off = Math.Max(Math.Abs(p.R - colour.R), Math.Max(Math.Abs(p.G - colour.G), Math.Abs(p.B - colour.B)));
                if (off < best.Off)
                {
                    best = (off, x + dx, y + dy, p);
                }
            }
        }

        return best;
    }

    private static void CheckLine(ScenarioDriver driver, ScenarioFrame frame, Vector2 point, (int R, int G, int B) colour, string what) =>
        CheckLineAlong(driver, frame, point, point, colour, what);
}

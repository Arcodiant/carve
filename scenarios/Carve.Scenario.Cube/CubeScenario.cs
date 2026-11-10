using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Cube;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class CubeExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, CubeScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// A cube, an L-block, a long box and a cube with a flipped face, seen from both views, using
/// only <c>define_mesh</c> and <c>render</c>. Frames are in <c>frames/</c> of the run folder.
/// </summary>
public sealed class CubeScenario : IScenario
{
    /// <summary>Faces must differ in the green channel by at least this much, in the expected order.</summary>
    private const int MinimumStep = 10;

    // Fewest non-background pixels each 512 x 512 frame may show, about half its measured count:
    // L-block back 94,688, long box 28,563, flipped cube 53,660. The L-block front's 88,829 gets 56%,
    // since an L-block that lost its side faces draws 49,732 there, and half would let that through.
    private const int LBlockFrontDrawn = 50000;
    private const int LBlockBackDrawn = 47000;
    private const int LongBoxDrawn = 14000;
    private const int FlippedCubeDrawn = 26000;

    // The vertices and faces of the define_mesh description's worked example.
    private static readonly double[][] CubeVertices =
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];

    private static readonly int[][] CubeFaces =
        [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    // Sampled pixels (x, y from the top left) of the unit cube, from T-4: the projected face centres.
    private static readonly (int X, int Y)[] CubeFront = [(256, 133), (172, 302), (367, 288)]; // top, +Z, +X
    private static readonly (int X, int Y)[] CubeBack = [(172, 209), (367, 223), (256, 378)];  // -Z, -X, underside

    // The L-block's notch corner (1.8, 1.8, 1) in front: its sight line never meets the solid.
    private static readonly (int X, int Y) NotchFront = (321, 185);

    private static readonly (int X, int Y) Centre = (256, 256);

    // The L-block from [0, -1, 0], zoom 3 on its corner (0, 0, 0), so the corner is the image centre.
    // Seen from -Y with -Z up, +X points left and +Z points down, so the block fills only the lower
    // left quarter. Its silhouette from -Y is a plain 2 x 1 rectangle, which looks the same upside
    // down or mirrored, so the quarters are what tell the up vector and the sign of the direction:
    // +Z up would put the block in the upper right, a camera at +Y in the lower right. Each quarter
    // is sampled a quarter of the image from the centre both ways (measured from the first run).
    private static readonly (int X, int Y) CornerBlock = (128, 384);
    private static readonly (int X, int Y) CornerAbove = (128, 128);
    private static readonly (int X, int Y) CornerRight = (384, 384);
    private static readonly (int X, int Y) CornerUpperRight = (384, 128);

    // In the zoomed frame, between the cube's far top edges (measured from the first run's frame).
    private static readonly (int X, int Y) ZoomedBackground = (256, 10);

    /// <summary>The part of the frame left clear on each side, as the product frames.</summary>
    private const float Margin = 0.08f;

    /// <summary>In a 512 frame, the first and last pixels inside the margin.</summary>
    private static readonly int MarginLow = (int)MathF.Round(Margin * 512);
    private static readonly int MarginHigh = 511 - MarginLow;

    private static readonly Vector3 CubeMin = Vector3.Zero;
    private static readonly Vector3 CubeMax = Vector3.One;

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The cube, both views.
        await DefineCleanAsync(driver, "cube", CubeVertices, CubeFaces);
        var cubeFront = await driver.CaptureAsync("cube-front", new { view = "front" });
        var cubeBack = await driver.CaptureAsync("cube-back", new { view = "back" });
        CheckCorners(driver, cubeFront);
        CheckCorners(driver, cubeBack);
        CheckOrder(driver, cubeFront, CubeFront, "top", "+Z", "+X");
        CheckOrder(driver, cubeBack, CubeBack, "-Z", "-X", "underside");

        // 2. A repeat of the same view is the same pixels.
        var again = await driver.CaptureAsync("cube-front-again", new { view = "front" });
        driver.AssertBytesEqual("cube front, rendered again", cubeFront.Pixels, again.Pixels);

        // 2b. T-9: the cube from any direction, closer in, and the errors. Each frame is checked for
        // the number of face shades it shows, a sampled pixel on the cube, and the four corners.
        var tilted = await driver.CaptureAsync("cube-dir-0.4-2-0.8", new { direction = new[] { 0.4, 2, 0.8 } });
        CheckCorners(driver, tilted);
        // Three faces are seen, but the light, turned with this camera, reaches only the top: the +Z and +X
        // sides are both ambient-only and share one shade, so two shades show.
        CheckShades(driver, tilted, 2);
        CheckNotBackground(driver, tilted, Centre, "the centre");

        var fromX = await driver.CaptureAsync("cube-dir-1-0-0", new { direction = new[] { 1, 0, 0 } });
        CheckCorners(driver, fromX);
        CheckShades(driver, fromX, 1);
        CheckNotBackground(driver, fromX, Centre, "the centre");

        var fromBelow = await driver.CaptureAsync("cube-dir-0--1-0", new { direction = new[] { 0, -1, 0 } });
        CheckCorners(driver, fromBelow);
        CheckShades(driver, fromBelow, 1);
        CheckNotBackground(driver, fromBelow, Centre, "the centre");

        var zoomed = await driver.CaptureAsync("cube-zoom3-corner", new { view = "front", zoom = 3, target = new[] { 1, 1, 1 } });
        CheckShades(driver, zoomed, 3);
        CheckNotBackground(driver, zoomed, Centre, "the corner (1, 1, 1) at the centre");
        // Zoomed in, the cube fills the lower corners; only the two at the top are background.
        CheckBackground(driver, zoomed, (0, 0), "the top left corner");
        CheckBackground(driver, zoomed, (zoomed.Width - 1, 0), "the top right corner");
        CheckNotBackground(driver, zoomed, (0, zoomed.Height - 1), "the bottom left corner, filled by the cube");
        CheckNotBackground(driver, zoomed, (zoomed.Width - 1, zoomed.Height - 1), "the bottom right corner, filled by the cube");
        CheckBackground(driver, zoomed, ZoomedBackground, "the sky between the cube's top edges");

        await ExpectErrorAsync(driver, "view with direction", new { view = "front", direction = new[] { 1, 0, 0 } });
        await ExpectErrorAsync(driver, "zoom 9", new { zoom = 9 });

        // 2c. T-24: the sizes, the orthographic frames and their errors.
        await CheckSizedFrontAsync(driver, 256);
        await CheckSizedFrontAsync(driver, 1024);

        // +x orthographic: the face fills the margin square. A cube head-on looks the same mirrored or
        // upside down, so the frame after it is what tells the camera's up and handedness.
        var plusX = await driver.CaptureAsync("cube-plusx-ortho", new { view = "+x", projection = "orthographic" });
        CheckCaption(driver, plusX, "512x512, view +x, orthographic, 8 vertices, 6 faces");
        CheckCorners(driver, plusX);
        CheckShades(driver, plusX, 1);
        CheckMarginSquare(driver, plusX);

        // The same view on the corner (1, 1, 1), zoom 3. From +X with +Y up, -Z is on the right and -Y
        // below, so the face fills only the lower right quarter: a flipped camera puts it in the upper
        // right, a mirrored one in the lower left. Zoom 1 would leave the frame's corner background.
        var plusXCorner = await driver.CaptureAsync("cube-plusx-ortho-corner", new { view = "+x", projection = "orthographic", zoom = 3, target = new[] { 1, 1, 1 } });
        CheckNotBackground(driver, plusXCorner, (384, 384), "the lower right quarter (the face, -Z right and -Y down)");
        CheckNotBackground(driver, plusXCorner, (511, 511), "the bottom right corner, filled at zoom 3");
        CheckBackground(driver, plusXCorner, (128, 384), "the lower left quarter (the face would be there mirrored)");
        CheckBackground(driver, plusXCorner, (384, 128), "the upper right quarter (the face would be there flipped)");
        CheckBackground(driver, plusXCorner, (128, 128), "the upper left quarter");

        // [1, 1, 1] orthographic: a regular hexagon about the corner (1, 1, 1). The silhouette corners
        // come from the orthographic matrix; a perspective frame's are 248 px out at the near three and
        // 205 px at the far three, against 215 px here, so it fails both sides of the check.
        var diagonal = new Vector3(1, 1, 1);
        var hexagon = await driver.CaptureAsync("cube-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        CheckCaption(driver, hexagon, "512x512, direction (1, 1, 1), orthographic, 8 vertices, 6 faces");
        CheckCorners(driver, hexagon);
        var hexagonPixel = OrthographicPixels(CubeMin, CubeMax, diagonal, Vector3.UnitY, hexagon.Width);
        Vector3[] silhouette = [new(0, 1, 0), new(0, 1, 1), new(0, 0, 1), new(1, 0, 1), new(1, 0, 0), new(1, 1, 0)];
        CheckOutline(driver, hexagon, silhouette.Select(hexagonPixel).ToArray(), 3);
        // Flipped, the bright top face leaves the top of the hexagon. +Z and +X differ by only 3 of 255
        // here, too little to tell a mirror, so the frame after this one does that.
        var hexagonTop = Rounded(hexagonPixel(new Vector3(0.5f, 1, 0.5f)));
        CheckBrighter(driver, hexagon, hexagonTop, Rounded(hexagonPixel(new Vector3(0.5f, 0.5f, 1))), "the top, over +Z");
        CheckBrighter(driver, hexagon, hexagonTop, Rounded(hexagonPixel(new Vector3(1, 0.5f, 0.5f))), "the top, over +X");

        // The same view, zoom 3 on the corner (1, 1, 0), the hexagon's upper right corner, now the image
        // centre. The cube is the 120 degree wedge between the edges to (0, 1, 0), up and left, and to
        // (1, 0, 0), straight down: 150 to 270 degrees, anticlockwise from image right. Mirrored it is 270
        // to 390, flipped 90 to 210. So 100 px out, 240 degrees is the mesh, 330 is background and only a
        // mirrored camera would fill it, 120 is background and only a flipped one would.
        var hexagonCorner = await driver.CaptureAsync("cube-dir-1-1-1-ortho-corner", new { direction = new[] { 1, 1, 1 }, projection = "orthographic", zoom = 3, target = new[] { 1, 1, 0 } });
        var cornerPixel = OrthographicPixels(CubeMin, CubeMax, diagonal, Vector3.UnitY, hexagonCorner.Width, 3, new Vector3(1, 1, 0));
        var wedgeTip = cornerPixel(new Vector3(1, 1, 0));
        CheckNotBackground(driver, hexagonCorner, Rounded(wedgeTip + AtAngle(240, 100)), "inside the wedge of the cube's corner");
        CheckBackground(driver, hexagonCorner, Rounded(wedgeTip + AtAngle(330, 100)), "where a mirrored camera would put the cube");
        CheckBackground(driver, hexagonCorner, Rounded(wedgeTip + AtAngle(120, 100)), "where a flipped camera would put the cube");

        await ExpectErrorAsync(driver, "size 300", new { size = 300 }, "size: 300 is not a size; use 256, 512 or 1024");
        await ExpectErrorAsync(driver, "projection iso", new { projection = "iso" }, "projection: 'iso' is not a projection");
        await ExpectErrorAsync(driver, "view top", new { view = "top" }, "view: 'top' is not a view");

        // 2c. T-42: smooth shading. The same hexagon view three ways. Flat is the frame above: a face is one shade. Smooth gives each corner
        // its own normal, so the top face (a gradient from the corner at the centre of the hexagon to the far one) is not one shade.
        // Smooth with the top's four edges hard keeps the top one shade and still grades the +Z face. Every edge hard is the flat frame,
        // pixel for pixel. Points are worked from the mesh by the orthographic rule, so a mirrored or flipped camera samples background or another face.
        var topNear = Rounded(hexagonPixel(new Vector3(0.85f, 1, 0.85f)));
        var topFar = Rounded(hexagonPixel(new Vector3(0.15f, 1, 0.15f)));
        var flatTopNear = hexagon.Pixel(topNear.X, topNear.Y).G;
        var flatTopFar = hexagon.Pixel(topFar.X, topFar.Y).G;
        if (flatTopNear != flatTopFar) driver.Fail($"{hexagon.Name}: the flat top face should be one shade, got {flatTopNear} and {flatTopFar}");

        Ok(driver, await driver.CallAsync("set_shading", new { shading = "smooth" }));
        var smooth = await driver.CaptureAsync("cube-smooth-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        CheckOutline(driver, smooth, silhouette.Select(hexagonPixel).ToArray(), 3);
        CheckDiffers(driver, smooth, topNear, topFar, "the smooth top face, near the centre corner and near the far one");
        CheckDiffers(driver, smooth, topNear, Rounded(hexagonPixel(new Vector3(0.15f, 1, 0.85f))), "the smooth top face, along the other diagonal");
        if (smooth.Pixels.AsSpan().SequenceEqual(hexagon.Pixels)) driver.Fail($"{smooth.Name}: smooth shading drew the flat frame");

        Ok(driver, await driver.CallAsync("mark_edges", new { edges = new[] { new[] { 3, 7 }, new[] { 7, 6 }, new[] { 6, 2 }, new[] { 2, 3 } } }));
        var creased = await driver.CaptureAsync("cube-creased-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        CheckOutline(driver, creased, silhouette.Select(hexagonPixel).ToArray(), 3);
        var creasedNear = creased.Pixel(topNear.X, topNear.Y).G;
        var creasedFar = creased.Pixel(topFar.X, topFar.Y).G;
        if (creasedNear != flatTopNear || creasedFar != flatTopFar)
            driver.Fail($"{creased.Name}: the creased top face should keep its flat shade {flatTopNear}, got {creasedNear} and {creasedFar}");
        CheckDiffers(driver, creased, Rounded(hexagonPixel(new Vector3(0.5f, 0.9f, 1))), Rounded(hexagonPixel(new Vector3(0.5f, 0.1f, 1))), "the +Z face, graded from its top edge to its bottom one");

        var every = new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 }, new[] { 3, 0 }, new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 }, new[] { 0, 4 }, new[] { 1, 5 }, new[] { 2, 6 }, new[] { 3, 7 } };
        Ok(driver, await driver.CallAsync("mark_edges", new { edges = every }));
        var allHard = await driver.CaptureAsync("cube-smooth-all-hard-dir-1-1-1-ortho", new { direction = new[] { 1, 1, 1 }, projection = "orthographic" });
        if (!allHard.Pixels.AsSpan().SequenceEqual(hexagon.Pixels)) driver.Fail($"{allHard.Name}: smooth with every edge hard should draw the flat frame pixel for pixel");

        // 3. The L-block: two concave hexagon caps and six quad sides.
        var (lVertices, lFaces) = LBlock();
        await DefineCleanAsync(driver, "L-block", lVertices, lFaces);
        var lFront = await driver.CaptureAsync("lblock-front", new { view = "front" });
        var lBack = await driver.CaptureAsync("lblock-back", new { view = "back" });
        CheckCorners(driver, lFront);
        CheckCorners(driver, lBack);
        CheckDrawn(driver, lFront, LBlockFrontDrawn);
        CheckDrawn(driver, lBack, LBlockBackDrawn);
        CheckBackground(driver, lFront, NotchFront, "the L-block's notch");

        // 3b. T-9: up is -Z. Zoomed on the block's corner, only the lower left quarter is the mesh.
        var lCorner = await driver.CaptureAsync("lblock-dir-0--1-0-corner", new { direction = new[] { 0, -1, 0 }, zoom = 3, target = new[] { 0, 0, 0 } });
        CheckNotBackground(driver, lCorner, CornerBlock, "the lower left quarter (the block, with -Z up and +X left)");
        CheckBackground(driver, lCorner, CornerAbove, "the upper left quarter (above the block's top face)");
        CheckBackground(driver, lCorner, CornerRight, "the lower right quarter (the block would be there from +Y)");
        CheckBackground(driver, lCorner, CornerUpperRight, "the upper right quarter (the block would be there with +Z up)");

        // 3c. T-24: looking up from below, orthographic, -Z up. The L-block above is a plain rectangle
        // from -Y, so this one lies in the XZ plane (the person's decision, 2026-11-07). +X is left and
        // +Z down, so the X arm runs across the top, the Z arm down the right, and the notch is the
        // lower left quarter. A mirrored camera, or one at +Y, puts the notch lower right and fails on
        // the notch pixel, which it fills; a camera with +Z up puts it upper left and fails there too.
        var (fVertices, fFaces) = LBlockLying();
        await DefineCleanAsync(driver, "lying L-block", fVertices, fFaces);
        var lBelow = await driver.CaptureAsync("lblock-lying-minusy-ortho", new { view = "-y", projection = "orthographic" });
        CheckCaption(driver, lBelow, "512x512, view -y, orthographic, 12 vertices, 8 faces");
        CheckCorners(driver, lBelow);
        CheckShades(driver, lBelow, 1);
        var lBelowPixel = OrthographicPixels(Vector3.Zero, new Vector3(2, 1, 2), -Vector3.UnitY, -Vector3.UnitZ, lBelow.Width);
        CheckBackground(driver, lBelow, Rounded(lBelowPixel(new Vector3(1.5f, 0, 1.5f))), "the notch (a mirrored or flipped camera fills it)");
        CheckNotBackground(driver, lBelow, Rounded(lBelowPixel(new Vector3(1.5f, 0, 0.5f))), "the X arm, upper left");
        CheckNotBackground(driver, lBelow, Rounded(lBelowPixel(new Vector3(0.5f, 0, 1.5f))), "the Z arm, lower right");

        // 4. A long box, off the origin: framing has to find it.
        var (bVertices, bFaces) = Box(5, 2, -3, 4, 0.5, 0.5);
        await DefineAsync(driver, "long box", bVertices, bFaces);
        var box = await driver.CaptureAsync("longbox-front", new { view = "front" });
        CheckCorners(driver, box);
        CheckDrawn(driver, box, LongBoxDrawn);
        CheckEdges(driver, box);

        // 5. A cube with one face wound backwards: accepted with a warning, kept as a record of the look.
        var flipped = CubeFaces.Select(f => f.ToArray()).ToArray();
        Array.Reverse(flipped[1]);
        var warned = await DefineAsync(driver, "flipped cube", CubeVertices, flipped);
        if (warned.GetProperty("warnings").GetArrayLength() == 0)
        {
            driver.Fail($"flipped cube: expected a warning, got {warned}");
        }

        var flippedFront = await driver.CaptureAsync("cube-flipped-front", new { view = "front" });
        CheckCorners(driver, flippedFront);
        CheckDrawn(driver, flippedFront, FlippedCubeDrawn);

        // 6. A bad index is a tool error, and the mesh before it still renders.
        var bad = await driver.CallAsync("define_mesh", new { vertices = new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { 0, 1, 0 } }, faces = new[] { new[] { 0, 1, 9 } } });
        if (bad.IsError is not true)
        {
            driver.Fail("a bad index was not a tool error");
        }

        var afterError = await driver.CaptureAsync("after-error-front", new { view = "front" });
        driver.AssertBytesEqual("the mesh before a failed define_mesh", flippedFront.Pixels, afterError.Pixels);
        CheckDrawn(driver, afterError, FlippedCubeDrawn);
    }

    private static async Task<JsonElement> DefineAsync(ScenarioDriver driver, string what, double[][] vertices, int[][] faces)
    {
        var result = await driver.CallAsync("define_mesh", new { vertices, faces });
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError is true)
        {
            driver.Fail($"define_mesh {what}: {text}");
        }

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task DefineCleanAsync(ScenarioDriver driver, string what, double[][] vertices, int[][] faces)
    {
        var report = await DefineAsync(driver, what, vertices, faces);
        if (!report.GetProperty("closed").GetBoolean() || report.GetProperty("warnings").GetArrayLength() != 0)
        {
            driver.Fail($"{what}: expected closed and no warnings, got {report}");
        }
    }

    /// <summary>The L of six corners in the XY plane, one thick in Z.</summary>
    private static (double[][] Vertices, int[][] Faces) LBlock()
    {
        double[][] outline = [[0, 0], [2, 0], [2, 1], [1, 1], [1, 2], [0, 2]]; // counter-clockwise from +Z
        var vertices = outline.Select(p => new[] { p[0], p[1], 0 }).Concat(outline.Select(p => new[] { p[0], p[1], 1 })).ToArray();
        var faces = new List<int[]> { new[] { 5, 4, 3, 2, 1, 0 }, new[] { 6, 7, 8, 9, 10, 11 } }; // -Z cap, +Z cap
        for (var i = 0; i < 6; i++)
        {
            var j = (i + 1) % 6;
            faces.Add([i, j, j + 6, i + 6]);
        }

        return (vertices, faces.ToArray());
    }

    /// <summary>
    /// The same L turned a quarter about X, so its outline is in the XZ plane and it is one thick in Y.
    /// A rotation, so the faces keep their winding.
    /// </summary>
    private static (double[][] Vertices, int[][] Faces) LBlockLying()
    {
        var (vertices, faces) = LBlock();
        return (vertices.Select(v => new[] { v[0], 1 - v[2], v[1] }).ToArray(), faces);
    }

    private static (double[][] Vertices, int[][] Faces) Box(double x, double y, double z, double dx, double dy, double dz)
    {
        double[][] vertices =
        [
            [x, y, z], [x + dx, y, z], [x + dx, y + dy, z], [x, y + dy, z],
            [x, y, z + dz], [x + dx, y, z + dz], [x + dx, y + dy, z + dz], [x, y + dy, z + dz],
        ];
        return (vertices, CubeFaces);
    }

    private static async Task ExpectErrorAsync(ScenarioDriver driver, string what, object arguments, string? mentions = null)
    {
        var result = await driver.CallAsync("render", arguments);
        if (result.IsError is not true)
        {
            driver.Fail($"render with {what} was not a tool error");
        }

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (mentions is not null && !text.Contains(mentions, StringComparison.Ordinal))
        {
            driver.Fail($"render with {what} said '{text}', expected '{mentions}'");
        }
    }

    /// <summary>The cube's front at another size: the 512 frame's checks, its sample points scaled.</summary>
    private static async Task CheckSizedFrontAsync(ScenarioDriver driver, int size)
    {
        var frame = await driver.CaptureAsync($"cube-front-{size}", new { view = "front", size });
        if (frame.Width != size || frame.Height != size)
        {
            driver.Fail($"{frame.Name}: the PNG is {frame.Width}x{frame.Height}, expected {size}x{size}");
        }

        CheckCaption(driver, frame, $"{size}x{size}, view front, 8 vertices, 6 faces");
        CheckCorners(driver, frame);
        CheckNotBackground(driver, frame, (size / 2, size / 2), "the centre");
        CheckOrder(driver, frame, CubeFront.Select(p => (p.X * size / 512, p.Y * size / 512)).ToArray(), "top", "+Z", "+X");
    }

    /// <summary>One pixel at least <see cref="MinimumStep"/> brighter than another, in the green channel.</summary>
    private static void CheckBrighter(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) brighter, (int X, int Y) darker, string what)
    {
        var (high, low) = (frame.Pixel(brighter.X, brighter.Y).G, frame.Pixel(darker.X, darker.Y).G);
        if (high < low + MinimumStep)
        {
            driver.Fail($"{frame.Name}: {what}: {high} should be at least {MinimumStep} above {low}");
        }
    }

    /// <summary>Two pixels at least <see cref="MinimumStep"/> apart in the green channel, either way.</summary>
    private static void CheckDiffers(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) a, (int X, int Y) b, string what)
    {
        var (first, second) = (frame.Pixel(a.X, a.Y).G, frame.Pixel(b.X, b.Y).G);
        if (Math.Abs(first - second) < MinimumStep)
        {
            driver.Fail($"{frame.Name}: {what}: {first} and {second} should differ by at least {MinimumStep}");
        }
    }

    private static void Ok(ScenarioDriver driver, ModelContextProtocol.Protocol.CallToolResult result)
    {
        if (result.IsError == true) driver.Fail("tool error: " + string.Join(" ", result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(t => t.Text)));
    }

    private static void CheckCaption(ScenarioDriver driver, ScenarioFrame frame, string expected)
    {
        if (frame.Caption != expected)
        {
            driver.Fail($"{frame.Name}: the caption is '{frame.Caption}', expected '{expected}'");
        }
    }

    /// <summary>
    /// Worked out here, not asked of the product: where a point of the mesh lands, in pixels from the
    /// top left, under the orthographic framing rule, for the box <paramref name="min"/> to
    /// <paramref name="max"/>. Zoom and target as <c>render</c> takes them; the target defaults to the
    /// centre. Near and far do not move a pixel, so they are any that hold the box.
    /// </summary>
    private static Func<Vector3, Vector2> OrthographicPixels(
        Vector3 min, Vector3 max, Vector3 direction, Vector3 up, int size, float zoom = 1, Vector3? target = null)
    {
        var centre = (min + max) / 2;
        var radius = (max - min).Length() / 2;
        Vector3 Drawn(Vector3 p) => (p - centre) / radius;

        var look = target is { } t ? Drawn(t) : Vector3.Zero;
        var view = Matrix4x4.CreateLookAt(look + Vector3.Normalize(direction) * 10, look, up);
        var reach = 0f;
        foreach (var x in new[] { min.X, max.X })
        foreach (var y in new[] { min.Y, max.Y })
        foreach (var z in new[] { min.Z, max.Z })
        {
            var seen = Vector3.TransformNormal(Drawn(new Vector3(x, y, z)) - look, view);
            reach = Math.Max(reach, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)));
        }

        var height = reach / (1 - 2 * Margin) / zoom;
        var camera = view * Matrix4x4.CreateOrthographic(2 * height, 2 * height, 0.1f, 100f);
        return p =>
        {
            var device = Vector3.Transform(Drawn(p), camera);
            return new Vector2((device.X + 1) / 2 * size, (1 - device.Y) / 2 * size);
        };
    }

    /// <summary>An offset in pixels, by an angle anticlockwise from image right. Image y runs down.</summary>
    private static Vector2 AtAngle(float degrees, float length) =>
        new Vector2(MathF.Cos(degrees * MathF.PI / 180), -MathF.Sin(degrees * MathF.PI / 180)) * length;

    private static (int X, int Y) Rounded(Vector2 pixel) => ((int)MathF.Round(pixel.X), (int)MathF.Round(pixel.Y));

    /// <summary>
    /// Each corner of a convex outline: the mesh <paramref name="by"/> pixels inside it, toward the
    /// outline's centre, and the background as far outside.
    /// </summary>
    private static void CheckOutline(ScenarioDriver driver, ScenarioFrame frame, Vector2[] corners, float by)
    {
        var middle = corners.Aggregate(Vector2.Zero, (sum, c) => sum + c) / corners.Length;
        foreach (var corner in corners)
        {
            var inward = Vector2.Normalize(middle - corner);
            CheckNotBackground(driver, frame, Rounded(corner + inward * by), $"{by} px inside the corner at {corner}");
            CheckBackground(driver, frame, Rounded(corner - inward * by), $"{by} px outside the corner at {corner}");
        }
    }

    /// <summary>
    /// A face filling the margin square of a 512 frame: on three lines each way its edge pixels lie at
    /// <see cref="MarginLow"/> and <see cref="MarginHigh"/> to within 1 px, with background 2 px outside.
    /// </summary>
    private static void CheckMarginSquare(ScenarioDriver driver, ScenarioFrame frame)
    {
        foreach (var line in new[] { 60, 256, 450 })
        {
            CheckNotBackground(driver, frame, (MarginLow + 1, line), "the left edge");
            CheckNotBackground(driver, frame, (MarginHigh - 1, line), "the right edge");
            CheckNotBackground(driver, frame, (line, MarginLow + 1), "the top edge");
            CheckNotBackground(driver, frame, (line, MarginHigh - 1), "the bottom edge");
            CheckBackground(driver, frame, (MarginLow - 2, line), "2 px left of the face");
            CheckBackground(driver, frame, (MarginHigh + 2, line), "2 px right of the face");
            CheckBackground(driver, frame, (line, MarginLow - 2), "2 px above the face");
            CheckBackground(driver, frame, (line, MarginHigh + 2), "2 px below the face");
        }
    }

    private static void CheckNotBackground(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, string what)
    {
        var p = frame.Pixel(at.X, at.Y);
        if (IsBackground(p))
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is the background, expected the mesh");
        }
    }

    /// <summary>
    /// The number of face shades: green values at least <see cref="MinimumStep"/> apart and from the
    /// background, each held by at least 1,500 pixels (a flat face is one value; edges are a few hundred).
    /// </summary>
    private static void CheckShades(ScenarioDriver driver, ScenarioFrame frame, int expected)
    {
        var counts = new int[256];
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                counts[frame.Pixel(x, y).G]++;
            }
        }

        var shades = new List<int>();
        for (var g = 0; g < 256; g++)
        {
            if (counts[g] >= 1500 && !IsBackground((48, (byte)g, 56, 255)) && shades.All(s => Math.Abs(s - g) >= MinimumStep))
            {
                shades.Add(g);
            }
        }

        if (shades.Count != expected)
        {
            driver.Fail($"{frame.Name}: {shades.Count} face shades ({string.Join(", ", shades)}), expected {expected}");
        }
    }

    private static bool IsBackground((byte R, byte G, byte B, byte A) p) =>
        p.R is >= 47 and <= 49 && p.G is >= 51 and <= 53 && p.B is >= 55 and <= 57;

    /// <summary>
    /// A blank frame passes every background check, so a frame must also show the mesh: at least
    /// <paramref name="minimum"/> pixels that are not the background.
    /// </summary>
    private static void CheckDrawn(ScenarioDriver driver, ScenarioFrame frame, int minimum)
    {
        var drawn = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                if (!IsBackground(frame.Pixel(x, y)))
                {
                    drawn++;
                }
            }
        }

        if (drawn < minimum)
        {
            driver.Fail($"{frame.Name}: only {drawn} pixels are not the background, expected at least {minimum}");
        }
    }

    private static void CheckCorners(ScenarioDriver driver, ScenarioFrame frame)
    {
        var (w, h) = (frame.Width - 1, frame.Height - 1);
        foreach (var (x, y) in new[] { (0, 0), (w, 0), (0, h), (w, h) })
        {
            CheckBackground(driver, frame, (x, y), "a corner");
        }
    }

    private static void CheckBackground(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, string what)
    {
        var p = frame.Pixel(at.X, at.Y);
        if (!IsBackground(p))
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is {p}, not the background");
        }
    }

    private static void CheckEdges(ScenarioDriver driver, ScenarioFrame frame)
    {
        for (var i = 0; i < frame.Width; i++)
        {
            CheckBackground(driver, frame, (i, 0), "the top edge");
            CheckBackground(driver, frame, (i, frame.Height - 1), "the bottom edge");
        }

        for (var i = 0; i < frame.Height; i++)
        {
            CheckBackground(driver, frame, (0, i), "the left edge");
            CheckBackground(driver, frame, (frame.Width - 1, i), "the right edge");
        }
    }

    /// <summary>Three faces, brightest first, each at least <see cref="MinimumStep"/> above the next.</summary>
    private static void CheckOrder(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y)[] at, params string[] faces)
    {
        var g = at.Select(a => (int)frame.Pixel(a.X, a.Y).G).ToArray();
        for (var i = 0; i < 2; i++)
        {
            if (g[i] < g[i + 1] + MinimumStep)
            {
                driver.Fail($"{frame.Name}: {faces[i]} ({g[i]}) should be at least {MinimumStep} brighter than {faces[i + 1]} ({g[i + 1]})");
            }
        }

        if (g[2] < 53 + MinimumStep)
        {
            driver.Fail($"{frame.Name}: {faces[2]} ({g[2]}) should be at least {MinimumStep} above the background");
        }
    }
}

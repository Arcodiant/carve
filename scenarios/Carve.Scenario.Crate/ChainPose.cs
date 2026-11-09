using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Crate.CrateLooks;

namespace Carve.Scenario.Crate;

/// <summary>
/// A two-bone chain bent at its joint (T-32), after everything else so that every earlier frame stays as it was.
/// <para>
/// Two boxes, 0.4 m square: <c>mesh</c> (this replaces the crate under its name) from y 0 to 1, and <c>upper</c> from y 1 to 2.
/// Bone <c>base</c> rests at the origin and <c>joint</c> 1 m above it. <c>mesh</c> is all <c>base</c>, set by vertex ids;
/// <c>upper</c> is all <c>joint</c>, set through a face group. The joint is then turned a quarter about +Z, which swings
/// the upper box about (0, 1, 0) to lie along -X: x from -1 to 0, y from 0.8 to 1.2.
/// </para>
/// <para>
/// Every check is at a point on the +Z faces (z = 0.2), seen head-on from +Z, where the camera is at +Z looking toward -Z
/// with +X to the right of the image. The arm must be on the LEFT: a bend turned the other way, or a camera mirrored in X,
/// puts it on the right, where the frame must be background. Points are projected from the mesh by <see cref="CameraPixels"/>
/// under the rule of framing on the REST bounds (D-6), so the same checks show where the posed model sits in that frame.
/// </para>
/// </summary>
internal static class ChainPose
{
    private const float Half = 0.2f;
    private static readonly Vector3 RestMin = new(-Half, 0, -Half);
    private static readonly Vector3 RestMax = new(Half, 2, Half);

    private static readonly int[][] Faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    private static float[][] Box(float y0, float y1) =>
    [
        [-Half, y0, -Half], [Half, y0, -Half], [Half, y1, -Half], [-Half, y1, -Half],
        [-Half, y0, Half], [Half, y0, Half], [Half, y1, Half], [-Half, y1, Half],
    ];

    /// <summary>A quarter turn about +Z as [x, y, z, w].</summary>
    private static readonly float[] QuarterZ = [0, 0, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f)];

    public static async Task RunAsync(ScenarioDriver driver)
    {
        await CallJsonAsync(driver, "define_mesh", new { name = "mesh", vertices = Box(0, 1), faces = Faces });
        await CallJsonAsync(driver, "define_mesh", new { name = "upper", vertices = Box(1, 2), faces = Faces });
        await CallJsonAsync(driver, "define_skeleton", new
        {
            bones = new object[]
            {
                new { name = "base", parent = (string?)null, position = new[] { 0f, 0, 0 }, rotation = new[] { 0f, 0, 0, 1 } },
                new { name = "joint", parent = "base", position = new[] { 0f, 1, 0 }, rotation = new[] { 0f, 0, 0, 1 } },
            },
        });

        // Weights: by vertex ids for one mesh, by group for the other; the result counts what is still unweighted.
        await ExpectToolErrorAsync(driver, "set_weights", new { mesh = "mesh", vertices = new[] { 0 }, weights = new { elbow = 1 } }, "elbow", "'base', 'joint'");
        await ExpectToolErrorAsync(driver, "set_weights", new { mesh = "mesh", vertices = new[] { 99 }, weights = new { @base = 1 } }, "99");
        await ExpectToolErrorAsync(driver, "set_weights", new { mesh = "upper", group = "all", weights = new { joint = 1 } }, "all", "define_group");
        var lower = await CallJsonAsync(driver, "set_weights", new { mesh = "mesh", vertices = new[] { 0, 1, 2, 3 }, weights = new { @base = 3 } });
        if (lower.GetProperty("unweighted").GetInt32() != 4 || lower.GetProperty("set").GetInt32() != 4 || lower.GetProperty("capped").GetInt32() != 0)
        {
            driver.Fail($"set_weights: expected 4 set and 4 unweighted, got {lower}");
        }

        lower = await CallJsonAsync(driver, "set_weights", new { mesh = "mesh", vertices = new[] { 4, 5, 6, 7, 0 }, weights = new { @base = 1 } });
        if (lower.GetProperty("unweighted").GetInt32() != 0 || lower.GetProperty("weights").GetProperty("base").GetDouble() != 1)
        {
            driver.Fail($"set_weights: expected none unweighted and base at 1, got {lower}");
        }

        await CallJsonAsync(driver, "define_group", new { mesh = "upper", name = "all", faces = new[] { 0, 1, 2, 3, 4, 5 } });
        var upper = await CallJsonAsync(driver, "set_weights", new { mesh = "upper", group = "all", weights = new { joint = 1 } });
        if (upper.GetProperty("unweighted").GetInt32() != 0 || upper.GetProperty("set").GetInt32() != 8)
        {
            driver.Fail($"set_weights by group: expected 8 set and none unweighted, got {upper}");
        }

        // The weights half of T-31's rule: the bones may change only in their rest transforms.
        await ExpectToolErrorAsync(driver, "define_skeleton", new
        {
            bones = new object[]
            {
                new { name = "base", position = new[] { 0f, 0, 0 }, rotation = new[] { 0f, 0, 0, 1 } },
                new { name = "elbow", parent = "base", position = new[] { 0f, 1, 0 }, rotation = new[] { 0f, 0, 0, 1 } },
            },
        }, "names and order");

        var restPixel = Perspective(RestMin, RestMax, Vector3.UnitZ, Size);
        var rest = await driver.CaptureAsync("chain-rest-z", new { view = "+z" });
        var empty = await driver.CaptureAsync("chain-pose-empty-z", new { view = "+z", pose = new { } });
        driver.AssertBytesEqual("chain with an empty pose, against no pose", rest.Pixels, empty.Pixels);
        var bent = await driver.CaptureAsync("chain-bent-z", new { view = "+z", pose = new { joint = new { rotation = QuarterZ } } });
        var bentFront = await driver.CaptureAsync("chain-bent-front", new { view = "front", pose = new { joint = new { rotation = QuarterZ } } });
        var restFront = await driver.CaptureAsync("chain-rest-front", new { view = "front" });
        var unposed = await driver.CaptureAsync("chain-rest-z-again", new { view = "+z" });
        driver.AssertBytesEqual("chain at rest, drawn again after a posed render", rest.Pixels, unposed.Pixels);
        if (bentFront.Pixels.SequenceEqual(restFront.Pixels))
        {
            driver.Fail("chain-bent-front: the same as the rest pose");
        }

        // Errors change nothing.
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", pose = new { elbow = new { rotation = QuarterZ } } }, "elbow", "'base', 'joint'");
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", pose = new { joint = new { rotation = new[] { 0f, 0, 0, 2 } } } }, "unit quaternion");
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", pose = new { joint = new { scale = 2 } } }, "scale");

        CheckCorners(driver, rest);
        CheckCorners(driver, bent);

        // The reference: what a lit +Z face of the lower box looks like in the rest frame.
        var reference = PixelAt(rest, restPixel, new Vector3(0, 0.3f, Half));
        // At rest the upper box is straight up: (0, 1.7) is box, and the places the arm will be are background.
        Surface(driver, rest, restPixel, new Vector3(0, 1.7f, Half), "the upper box at rest");
        Background(driver, rest, restPixel, new Vector3(-0.6f, 1.0f, Half), "where the bent arm will be, at rest");
        Background(driver, rest, restPixel, new Vector3(0.6f, 1.0f, Half), "where a reversed bend would put the arm, at rest");

        // Bent: the arm lies along -X, the image's left.
        foreach (var x in new[] { -0.4f, -0.7f, -0.9f })
        {
            SameAs(driver, bent, restPixel, new Vector3(x, 1.0f, Half), reference, $"the arm at x {x}");
        }

        SameAs(driver, bent, restPixel, new Vector3(0, 0.3f, Half), reference, "the lower box, untouched by the bend");
        Background(driver, bent, restPixel, new Vector3(0.4f, 1.0f, Half), "where a bend turned the other way puts the arm (x +0.4)");
        Background(driver, bent, restPixel, new Vector3(0.7f, 1.0f, Half), "where a bend turned the other way puts the arm (x +0.7)");
        Background(driver, bent, restPixel, new Vector3(0, 1.7f, Half), "where the upper box was at rest");
        Background(driver, bent, restPixel, new Vector3(-0.6f, 0.4f, Half), "below the arm");
        Background(driver, bent, restPixel, new Vector3(-0.6f, 1.6f, Half), "above the arm");

        // Framing on the rest bounds: every corner of the posed upper box (1 - y, x + 1, z) is inside the image.
        foreach (var x in new[] { -Half, Half })
        foreach (var y in new[] { 1f, 2f })
        foreach (var z in new[] { -Half, Half })
        {
            var corner = new Vector3(1 - y, x + 1, z);
            var (px, py) = Holding(restPixel(corner));
            if (px < 0 || py < 0 || px >= Size || py >= Size)
            {
                driver.Fail($"chain-bent-z: the posed corner {corner} falls at ({px}, {py}), outside the {Size} pixel image framed on the rest bounds");
            }
        }
    }

    private static (byte R, byte G, byte B, byte A) PixelAt(ScenarioFrame frame, Func<Vector3, Vector2> pixel, Vector3 point)
    {
        var (x, y) = Holding(pixel(point));
        return frame.Pixel(x, y);
    }

    private static void Surface(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, Vector3 point, string what)
    {
        var (x, y) = Holding(pixel(point));
        CheckNotBackground(driver, frame, (x, y), what);
    }

    private static void Background(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, Vector3 point, string what)
    {
        var (x, y) = Holding(pixel(point));
        var p = frame.Pixel(x, y);
        if (!IsBackground(p))
        {
            driver.Fail($"{frame.Name}: {what} at ({x}, {y}) is {p}, expected the background");
        }
    }

    /// <summary>A +Z face seen head-on is one flat colour: within 12 of the reference, per channel, and not the background.</summary>
    private static void SameAs(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, Vector3 point,
        (byte R, byte G, byte B, byte A) reference, string what)
    {
        var (x, y) = Holding(pixel(point));
        var p = frame.Pixel(x, y);
        if (IsBackground(p) || Math.Abs(p.R - reference.R) > 12 || Math.Abs(p.G - reference.G) > 12 || Math.Abs(p.B - reference.B) > 12)
        {
            driver.Fail($"{frame.Name}: {what} at ({x}, {y}) is {p}, expected the lit +Z face colour {reference}");
        }
    }
}

using System.Numerics;
using Carve.Debug;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Crate.CrateLooks;

namespace Carve.Scenario.Crate;

/// <summary>
/// The weights look (T-40), after everything else so that every earlier frame stays as it was. Four flat quads at z = 0 on the
/// chain's skeleton (<c>base</c> at the origin, <c>joint</c> at (0, 1, 0)), seen head-on from +Z and framed on their bounds:
/// <c>mesh</c> x -0.9..-0.5, y 0..0.5, all <c>base</c>; <c>upper</c> x -0.4..0, y 1..2, all <c>joint</c>; <c>mid</c> x 0.1..0.5,
/// y 0..1, half each; <c>stray</c> x 0.6..1, y 0..1, whose first three vertices are all <c>base</c> and whose fourth, the top
/// left, has no weight at all. Each quad but the last carries one weight on all its vertices, so its colour is flat: dark blue
/// at 0 (linear 0, 0, 0.5), green at 0.5, red at 1, as the encoded pixels (sRGB). The looks are not symmetric, so a mirrored
/// camera puts red on the wrong side, and a flipped one puts the magenta corner at the bottom.
/// </summary>
internal static class ChainWeights
{
    private static readonly Vector3 Min = new(-0.9f, 0, 0);
    private static readonly Vector3 Max = new(1.0f, 2, 0);
    private static readonly float[] QuarterZ = [0, 0, MathF.Sqrt(0.5f), MathF.Sqrt(0.5f)];

    private static readonly (int R, int G, int B) Red = (255, 0, 0);
    private static readonly (int R, int G, int B) Green = (0, 255, 0);
    private static readonly (int R, int G, int B) Blue = (0, 0, Encode(0.5));
    private static readonly (int R, int G, int B) Magenta = (255, 0, 255);
    private static readonly (int R, int G, int B) Background = (-1, -1, -1);

    public static async Task RunAsync(ScenarioDriver driver)
    {
        await Quad(driver, "mesh", -0.9f, -0.5f, 0, 0.5f);
        await Quad(driver, "upper", -0.4f, 0, 1, 2);
        await Quad(driver, "mid", 0.1f, 0.5f, 0, 1);
        await Quad(driver, "stray", 0.6f, 1.0f, 0, 1);
        await CallJsonAsync(driver, "set_weights", new { mesh = "mesh", vertices = new[] { 0, 1, 2, 3 }, weights = new { @base = 1 } });
        await CallJsonAsync(driver, "set_weights", new { mesh = "upper", vertices = new[] { 0, 1, 2, 3 }, weights = new { joint = 1 } });
        await CallJsonAsync(driver, "set_weights", new { mesh = "mid", vertices = new[] { 0, 1, 2, 3 }, weights = new { @base = 1, joint = 1 } });
        await CallJsonAsync(driver, "set_weights", new { mesh = "stray", vertices = new[] { 0, 1, 2 }, weights = new { @base = 1 } });

        var pixel = Perspective(Min, Max, Vector3.UnitZ, Size);
        var plain = await driver.CaptureAsync("weights-plain-z", new { view = "+z" });
        var baseFrame = await driver.CaptureAsync("weights-base-z", new { view = "+z", weights = "base" });
        var jointFrame = await driver.CaptureAsync("weights-joint-z", new { view = "+z", weights = "joint" });
        var posed = await driver.CaptureAsync("weights-joint-posed-z", new { view = "+z", weights = "joint", pose = new { joint = new { rotation = QuarterZ } } });
        var again = await driver.CaptureAsync("weights-plain-z-after", new { view = "+z" });
        driver.AssertBytesEqual("chain quads drawn plain, after the weights frames", plain.Pixels, again.Pixels);
        if (baseFrame.Pixels.SequenceEqual(plain.Pixels)) driver.Fail("weights-base-z: the same as the frame without weights");

        await ExpectToolErrorAsync(driver, "render", new { view = "+z", weights = "elbow" }, "elbow", "'base', 'joint'");
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", weights = "base", mode = "wireframe" }, "weights", "wireframe");

        // Weights of base: 1 on the left quad, 0 on the top quad, 0.5 on the middle one.
        Is(driver, baseFrame, pixel, -0.7f, 0.25f, Red, "the left quad, wholly base (weight 1)");
        Is(driver, baseFrame, pixel, -0.2f, 1.5f, Blue, "the top quad, not base at all (weight 0)");
        // The middle quad is the camera guard: a camera mirrored in x (about x = 0.05, the middle of the quads) or flipped in y
        // (about y = 1) puts background at (0.3, 0.5), not green.
        Is(driver, baseFrame, pixel, 0.3f, 0.5f, Green, "the middle quad, half base (weight 0.5); background under a camera mirrored in x or flipped in y");
        // The same bone from the other side: joint is 0 on the left, 1 on top, 0.5 in the middle.
        Is(driver, jointFrame, pixel, -0.7f, 0.25f, Blue, "the left quad, not joint at all");
        Is(driver, jointFrame, pixel, -0.2f, 1.5f, Red, "the top quad, wholly joint");
        Is(driver, jointFrame, pixel, 0.3f, 0.5f, Green, "the middle quad, half joint");

        // The planted unweighted vertex: the top left corner of the stray quad (0.6, 1), magenta there and not at the others.
        // The magenta check is a camera guard too (mirrored in x it lands on background at (-0.52, 0.97), flipped in y at (0.62, 1.03));
        // the red checks at the other three corners test corner order: each corner takes its own vertex's colour.
        Is(driver, baseFrame, pixel, 0.62f, 0.97f, Magenta, "beside the unweighted corner of the stray quad (a camera mirrored in x or flipped in y puts background here)", 40);
        Is(driver, baseFrame, pixel, 0.98f, 0.03f, Red, "beside the stray quad's weighted corner (1, 0)", 70);
        Is(driver, baseFrame, pixel, 0.98f, 0.97f, Red, "beside the stray quad's weighted corner (1, 1), across x from the unweighted one (corner order)", 70);
        Is(driver, baseFrame, pixel, 0.62f, 0.03f, Red, "beside the stray quad's weighted corner (0.6, 0), across y from the unweighted one (corner order)", 70);
        Is(driver, jointFrame, pixel, 0.62f, 0.97f, Magenta, "the unweighted corner, in joint's look as in base's", 40);

        // The look follows the pose: joint turned a quarter about its head (0, 1) carries the top quad to x -1..0, y 0.6..1.
        Is(driver, posed, pixel, -0.5f, 0.8f, Red, "the top quad, carried round joint's head");
        Is(driver, posed, pixel, -0.2f, 1.5f, Background, "where the top quad rested");
        Is(driver, posed, pixel, -0.5f, 1.2f, Background, "where a camera flipped in y puts the top quad");
        Is(driver, posed, pixel, -0.7f, 0.25f, Blue, "the left quad, which joint does not move");
    }

    private static int Encode(double linear) => (int)Math.Round(255 * (linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055));

    private static Task Quad(ScenarioDriver driver, string name, float x0, float x1, float y0, float y1) =>
        CallJsonAsync(driver, "define_mesh", new { name, vertices = new[] { new[] { x0, y0, 0 }, [x1, y0, 0], [x1, y1, 0], [x0, y1, 0] }, faces = new[] { new[] { 0, 1, 2, 3 } } });

    private static void Is(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, float x, float y, (int R, int G, int B) expected, string what, int within = 6)
    {
        var (px, py) = Holding(pixel(new Vector3(x, y, 0)));
        var p = frame.Pixel(px, py);
        if (expected.R < 0)
        {
            if (!IsBackground(p)) driver.Fail($"{frame.Name}: {what} at ({x}, {y}) is {p}, expected background");
            return;
        }

        if (Math.Abs(p.R - expected.R) > within || Math.Abs(p.G - expected.G) > within || Math.Abs(p.B - expected.B) > within)
        {
            driver.Fail($"{frame.Name}: {what} at ({x}, {y}) -> ({px}, {py}) is {p}, expected {expected} within {within}");
        }
    }
}

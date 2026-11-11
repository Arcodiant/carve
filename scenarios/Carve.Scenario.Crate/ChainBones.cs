using System.Numerics;
using Carve.Debug;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Crate.CrateLooks;

namespace Carve.Scenario.Crate;

/// <summary>
/// The skeleton drawn over the two-box chain (T-39), after everything else so that every earlier frame stays as it was.
/// <para>
/// Bones <c>base</c> at the origin and <c>joint</c> at (0, 1, 0), seen head-on from +Z, framed on the rest bounds as every
/// chain frame is. The lines are magenta (1, 0, 1 linear, so 255, 0, 255), 3 pixels wide, drawn through the boxes, which
/// they run inside: at z = 0 the model's own faces are in front of them. At rest: base to joint is a line from y 0 to 1, and
/// the leaf <c>joint</c> a line along it from y 1 to 1.5. Posed, <c>base</c> turned an eighth about +Z carries the joint to
/// (-sin 45, cos 45) and both lines lean up and to the LEFT; a mirrored camera puts them to the right, and a flipped one
/// puts the leaf's far end at the bottom, so each frame names a point that is line and a point that would be line only
/// if it were mirrored or flipped, which must be none.
/// </para>
/// </summary>
internal static class ChainBones
{
    private static readonly Vector3 RestMin = new(-0.2f, 0, -0.2f);
    private static readonly Vector3 RestMax = new(0.2f, 2, 0.2f);
    private static readonly float S = MathF.Sin(MathF.PI / 8);
    private static readonly float C = MathF.Cos(MathF.PI / 8);

    public static async Task RunAsync(ScenarioDriver driver)
    {
        var pixel = Perspective(RestMin, RestMax, Vector3.UnitZ, Size);
        var plain = await driver.CaptureAsync("chain-rest-z-for-bones", new { view = "+z" });
        var rest = await driver.CaptureAsync("chain-bones-rest-z", new { view = "+z", bones = true });
        var named = await driver.CaptureAsync("chain-bones-rest-labelled-z", new { view = "+z", bones = true, bone_labels = true });
        var bent = await driver.CaptureAsync("chain-bones-bent-z", new { view = "+z", bones = true, pose = new { @base = new { rotation = new[] { 0f, 0, S, C } } } });
        var again = await driver.CaptureAsync("chain-rest-z-after-bones", new { view = "+z" });
        driver.AssertBytesEqual("chain at rest, drawn without bones after bones", plain.Pixels, again.Pixels);
        if (rest.Pixels.SequenceEqual(plain.Pixels)) driver.Fail("chain-bones-rest-z: the same as the frame without bones");

        await ExpectToolErrorAsync(driver, "render", new { view = "+z", bone_labels = true }, "bone_labels", "bones");

        // Rest: along the axis, inside the model; not past the leaf's tip, not beside it.
        Line(driver, rest, pixel, new Vector3(0, 0.5f, 0), "base to joint, through the lower box");
        Line(driver, rest, pixel, new Vector3(0, 1.25f, 0), "the leaf, through the upper box");
        Line(driver, rest, pixel, new Vector3(0, 1.0f, 0), "the joint's dot");
        NoLine(driver, rest, pixel, new Vector3(0, 1.75f, 0), "past the leaf's tip (half the parent's length), where a leaf of the parent's full length, or a bone flipped in y, would be");
        NoLine(driver, rest, pixel, new Vector3(0.3f, 0.5f, 0), "beside the lower box");
        NoLine(driver, plain, pixel, new Vector3(0, 0.5f, 0), "the frame without bones");

        // The same names do not change the lines, and add a backing beside the joint.
        Line(driver, named, pixel, new Vector3(0, 0.5f, 0), "base to joint, with names");
        var joint = pixel(new Vector3(0, 1, 0));
        var (bx, by) = Holding(joint + new Vector2(6, -17));
        if (named.Pixel(bx, by).Equals(rest.Pixel(bx, by))) driver.Fail($"{named.Name}: no label backing beside the joint at ({bx}, {by})");

        // Posed: the joint is at (-sin 45, cos 45), the leaf runs on along the same direction for half the length.
        var d = new Vector2(-MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));
        Line(driver, bent, pixel, new Vector3(d.X * 0.5f, d.Y * 0.5f, 0), "base to joint, leaning left");
        Line(driver, bent, pixel, new Vector3(d.X * 1.25f, d.Y * 1.25f, 0), "the leaf, leaning left");
        NoLine(driver, bent, pixel, new Vector3(-d.X * 0.5f, d.Y * 0.5f, 0), "where a mirrored camera or a turn the other way draws base to joint");
        NoLine(driver, bent, pixel, new Vector3(-d.X * 1.25f, d.Y * 1.25f, 0), "where a mirrored camera draws the leaf");
        NoLine(driver, bent, pixel, new Vector3(d.X * 0.5f, RestMin.Y + RestMax.Y - d.Y * 0.5f, 0), "where a camera flipped in y (about the middle of the rest bounds) draws base to joint");
        NoLine(driver, bent, pixel, new Vector3(0, 0.5f, 0), "where the rest pose had base to joint");
    }

    private static bool Magenta((byte R, byte G, byte B, byte A) p) => p.R > 200 && p.B > 200 && p.G < 80;

    /// <summary>Some pixel within 2 of the point's is the line's colour (a line 3 wide, anti-aliased, is hit at its middle).</summary>
    private static bool Near(ScenarioFrame frame, Func<Vector3, Vector2> pixel, Vector3 point)
    {
        var (x, y) = Holding(pixel(point));
        for (var dx = -2; dx <= 2; dx++)
        for (var dy = -2; dy <= 2; dy++)
            if (Magenta(frame.Pixel(x + dx, y + dy))) return true;
        return false;
    }

    private static void Line(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, Vector3 point, string what)
    {
        if (!Near(frame, pixel, point)) driver.Fail($"{frame.Name}: no bone line {what}, at {point}");
    }

    private static void NoLine(ScenarioDriver driver, ScenarioFrame frame, Func<Vector3, Vector2> pixel, Vector3 point, string what)
    {
        if (Near(frame, pixel, point)) driver.Fail($"{frame.Name}: a bone line {what}, at {point}");
    }
}

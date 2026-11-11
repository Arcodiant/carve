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

        await WaveAsync(driver, restPixel, reference, bent);
        await WeighedArmAsync(driver, restPixel, reference);
        await ChainBones.RunAsync(driver);
    }

    /// <summary>A column of <paramref name="rings"/> square rings (y values), 0.4 m square, capped at both ends.</summary>
    private static (float[][] Vertices, int[][] Faces) Column(params float[] rings)
    {
        var vertices = rings.SelectMany(y => new[] { new[] { -Half, y, -Half }, [Half, y, -Half], [Half, y, Half], [-Half, y, Half] }).ToArray();
        var faces = new List<int[]> { new[] { 0, 1, 2, 3 } };
        for (var r = 0; r + 1 < rings.Length; r++)
        {
            for (var k = 0; k < 4; k++)
            {
                int a = r * 4 + k, b = r * 4 + (k + 1) % 4;
                faces.Add([a, a + 4, b + 4, b]);
            }
        }
        var top = (rings.Length - 1) * 4;
        faces.Add([top, top + 3, top + 2, top + 1]);
        return (vertices, faces.ToArray());
    }

    /// <summary>
    /// T-37: the same chain weighted by <c>weigh</c> instead of by hand. Both boxes are defined again as columns with a ring every
    /// 0.5 m (which drops their hand weights), weighed with one call each, and bent a quarter turn. Base's segment is y 0..1;
    /// joint, a leaf, runs on along base's direction for half base's length, y 1..1.5. The ring at the joint is on both and
    /// shares them evenly (an elbow), the rings above lean wholly toward joint (0.94 and 0.92), so the arm lies along -X
    /// from about x -0.1 to -0.93 and nothing is left standing where the upper box rested: a vertex left unweighted, or at
    /// rest, stretches a face up through (0, 1.7).
    /// </summary>
    private static async Task WeighedArmAsync(ScenarioDriver driver, Func<Vector3, Vector2> restPixel, (byte R, byte G, byte B, byte A) reference)
    {
        var lower = Column(0, 0.5f, 1);
        var upper = Column(1, 1.5f, 2);
        await CallJsonAsync(driver, "define_mesh", new { name = "mesh", vertices = lower.Vertices, faces = lower.Faces });
        await CallJsonAsync(driver, "define_mesh", new { name = "upper", vertices = upper.Vertices, faces = upper.Faces });
        await ExpectToolErrorAsync(driver, "weigh", new { }, "'mesh'", "'upper'");
        await ExpectToolErrorAsync(driver, "weigh", new { mesh = "mesh", bones = new[] { "elbow" } }, "elbow", "'base', 'joint'");
        foreach (var m in new[] { "mesh", "upper" })
        {
            var weighed = await CallJsonAsync(driver, "weigh", new { mesh = m });
            if (weighed.GetProperty("weighed").GetInt32() != 12 || weighed.GetProperty("unweighted").GetInt32() != 0)
            {
                driver.Fail($"weigh {m}: expected all 12 vertices weighed and none left, got {weighed}");
            }

            var again = await CallJsonAsync(driver, "weigh", new { mesh = m });
            if (again.GetProperty("weighed").GetInt32() != 0 || again.GetProperty("kept").GetInt32() != 12)
            {
                driver.Fail($"weigh {m} again without replace: expected every weight kept, got {again}");
            }
        }

        var bent = await driver.CaptureAsync("chain-weighed-bent-z", new { view = "+z", pose = new { joint = new { rotation = QuarterZ } } });
        CheckCorners(driver, bent);
        foreach (var x in new[] { -0.3f, -0.7f, -0.85f })
        {
            SameAs(driver, bent, restPixel, new Vector3(x, 1.05f, Half), reference, $"the weighed arm at x {x}");
        }

        SameAs(driver, bent, restPixel, new Vector3(0, 0.3f, Half), reference, "the lower column, almost wholly base's");
        Background(driver, bent, restPixel, new Vector3(0, 1.7f, Half), "where the upper column rested (a vertex left at rest stretches a face here)");
        Background(driver, bent, restPixel, new Vector3(0.4f, 1.0f, Half), "where a bend turned the other way, or a mirrored camera, puts the arm (x +0.4)");
        Background(driver, bent, restPixel, new Vector3(0.7f, 1.0f, Half), "where a bend turned the other way, or a mirrored camera, puts the arm (x +0.7)");
        Background(driver, bent, restPixel, new Vector3(-0.6f, 0.5f, Half), "below the arm");
        Background(driver, bent, restPixel, new Vector3(-0.6f, 1.6f, Half), "above the arm");
    }

    /// <summary>
    /// T-33: the wave clip, define_clip's worked example: the joint turns from rest to <see cref="QuarterZ"/> at 0.5 s and
    /// back by 1 s, LINEAR. At 0.25 s the slerp is half way, an eighth of a turn about +Z, so the upper box leans up and to
    /// the LEFT along (-sin 45, cos 45) from the joint at (0, 1). At 0.5 s the clip is exactly the bent pose of T-32, so
    /// that frame must be byte-identical to <c>chain-bent-z</c>.
    /// </summary>
    private static async Task WaveAsync(ScenarioDriver driver, Func<Vector3, Vector2> restPixel, (byte R, byte G, byte B, byte A) reference, ScenarioFrame bent)
    {
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", clip = "wave", time = 0.25 }, "wave", "no clips");
        await ExpectToolErrorAsync(driver, "define_clip", new
        {
            name = "wave", duration = 1,
            channels = new { joint = new { rotation = new { interpolation = "CUBICSPLINE", keys = new[] { new { time = 0f, value = new[] { 0f, 0, 0, 1 } } } } } },
        }, "LINEAR or STEP");
        var defined = await CallJsonAsync(driver, "define_clip", new
        {
            name = "wave", duration = 1, loop = true,
            channels = new
            {
                joint = new
                {
                    rotation = new
                    {
                        interpolation = "LINEAR",
                        keys = new[]
                        {
                            new { time = 0f, value = new[] { 0f, 0, 0, 1 } },
                            new { time = 0.5f, value = QuarterZ },
                            new { time = 1f, value = new[] { 0f, 0, 0, 1 } },
                        },
                    },
                },
            },
        });
        if (defined.GetProperty("replaced").GetBoolean() || defined.GetProperty("bones")[0].GetString() != "joint")
        {
            driver.Fail($"define_clip: expected a new clip animating joint, got {defined}");
        }

        await ExpectToolErrorAsync(driver, "render", new { view = "+z", clip = "wave", time = 0.25, pose = new { } }, "not with pose");
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", time = 0.25 }, "only with clip");
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", clip = "wave" }, "required with clip");
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", clip = "walk", time = 0.25 }, "'wave'");
        await ExpectToolErrorAsync(driver, "render", new { view = "+z", clip = "wave", time = 1.5 }, "outside");

        var quarter = await driver.CaptureAsync("chain-wave-0.25-z", new { view = "+z", clip = "wave", time = 0.25 });
        var half = await driver.CaptureAsync("chain-wave-0.5-z", new { view = "+z", clip = "wave", time = 0.5 });
        driver.AssertBytesEqual("the wave at 0.5 s, against the bent pose it keys there", bent.Pixels, half.Pixels);

        CheckCorners(driver, quarter);
        var axis = new Vector2(-MathF.Sqrt(0.5f), MathF.Sqrt(0.5f));
        foreach (var d in new[] { 0.5f, 0.8f })
        {
            var on = new Vector3(axis.X * d, 1 + axis.Y * d, Half);
            SameAs(driver, quarter, restPixel, on, reference, $"the arm an eighth turn up-left, {d} m from the joint");
            Background(driver, quarter, restPixel, new Vector3(-on.X, on.Y, Half), $"where a turn the other way, or a camera mirrored in X, puts the arm ({d} m)");
        }

        SameAs(driver, quarter, restPixel, new Vector3(0, 0.3f, Half), reference, "the lower box, untouched by the wave");
        Background(driver, quarter, restPixel, new Vector3(0, 1.7f, Half), "where the upper box was at rest (a STEP or a clip held at 0 puts it there)");
        Background(driver, quarter, restPixel, new Vector3(-0.7f, 1.0f, Half), "where the arm lies at 0.5 s (a quarter turn, not an eighth)");
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

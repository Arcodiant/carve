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

    /// <summary>
    /// Fewest non-background pixels a 512 x 512 frame of a mesh may show. Measured: L-block 88,829
    /// and 94,688, long box 28,563, flipped cube 53,660; this is about half the smallest.
    /// </summary>
    private const int MinimumDrawn = 14000;

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

        // 3. The L-block: two concave hexagon caps and six quad sides.
        var (lVertices, lFaces) = LBlock();
        await DefineCleanAsync(driver, "L-block", lVertices, lFaces);
        var lFront = await driver.CaptureAsync("lblock-front", new { view = "front" });
        var lBack = await driver.CaptureAsync("lblock-back", new { view = "back" });
        CheckCorners(driver, lFront);
        CheckCorners(driver, lBack);
        CheckDrawn(driver, lFront);
        CheckDrawn(driver, lBack);
        CheckBackground(driver, lFront, NotchFront, "the L-block's notch");

        // 4. A long box, off the origin: framing has to find it.
        var (bVertices, bFaces) = Box(5, 2, -3, 4, 0.5, 0.5);
        await DefineAsync(driver, "long box", bVertices, bFaces);
        var box = await driver.CaptureAsync("longbox-front", new { view = "front" });
        CheckCorners(driver, box);
        CheckDrawn(driver, box);
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
        CheckDrawn(driver, flippedFront);

        // 6. A bad index is a tool error, and the mesh before it still renders.
        var bad = await driver.CallAsync("define_mesh", new { vertices = new[] { new[] { 0, 0, 0 }, new[] { 1, 0, 0 }, new[] { 0, 1, 0 } }, faces = new[] { new[] { 0, 1, 9 } } });
        if (bad.IsError is not true)
        {
            driver.Fail("a bad index was not a tool error");
        }

        var afterError = await driver.CaptureAsync("after-error-front", new { view = "front" });
        driver.AssertBytesEqual("the mesh before a failed define_mesh", flippedFront.Pixels, afterError.Pixels);
        CheckDrawn(driver, afterError);
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

    private static (double[][] Vertices, int[][] Faces) Box(double x, double y, double z, double dx, double dy, double dz)
    {
        double[][] vertices =
        [
            [x, y, z], [x + dx, y, z], [x + dx, y + dy, z], [x, y + dy, z],
            [x, y, z + dz], [x + dx, y, z + dz], [x + dx, y + dy, z + dz], [x, y + dy, z + dz],
        ];
        return (vertices, CubeFaces);
    }

    private static bool IsBackground((byte R, byte G, byte B, byte A) p) =>
        p.R is >= 47 and <= 49 && p.G is >= 51 and <= 53 && p.B is >= 55 and <= 57;

    /// <summary>
    /// A blank frame passes every background check, so a frame must also show the mesh: at least
    /// <see cref="MinimumDrawn"/> pixels that are not the background.
    /// </summary>
    private static void CheckDrawn(ScenarioDriver driver, ScenarioFrame frame)
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

        if (drawn < MinimumDrawn)
        {
            driver.Fail($"{frame.Name}: only {drawn} pixels are not the background, expected at least {MinimumDrawn}");
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

using System.Text.Json;
using Carve.Debug;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Crate;

/// <summary>
/// The crate's call sequence and the helpers that check it, shared by the Crate scenario and the Export
/// scenario (which links this file) so the two cannot drift apart. It holds no extension and no scenario.
/// </summary>
internal static class CrateBuild
{
    // The vertices and faces of the define_mesh description's worked example.
    public static readonly double[][] CubeVertices =
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];

    public static readonly int[][] CubeFaces =
        [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    public static readonly int[] AllFaces = [0, 1, 2, 3, 4, 5];

    // Face ids of the example cube, kept by every edit: the +Y face (3), the -X face (4) and the +X face (5).
    public const int TopFace = 3;
    public const int MinusXFace = 4;
    public const int PlusXFace = 5;

    /// <summary>The crate in 12 calls: the example cube, then edit tools only, counts checked after each. Returns the last report.</summary>
    public static async Task<JsonElement> BuildAsync(ScenarioDriver driver)
    {
        await DefineAsync(driver);
        await EditAsync(driver, "move", new { faces = new[] { TopFace }, by = new[] { 0, -0.2, 0 } }, 8, 6);                      // 2
        await EditAsync(driver, "extrude", new { faces = AllFaces, distance = 0, inset = 0.12 }, 32, 30);                          // 3
        await EditAsync(driver, "extrude", new { faces = AllFaces, distance = -0.08 }, 56, 54);                                    // 4

        // -X: the slot's edges drawn together by moving their vertices.
        await EditAsync(driver, "extrude", new { faces = new[] { MinusXFace }, distance = 0, inset = 0.05 }, 60, 58);            // 5
        var (top, bottom) = await TopAndBottomAsync(driver, MinusXFace);
        await EditAsync(driver, "move", new { vertices = new[] { top.A, top.B }, by = new[] { 0, -0.17, 0 } }, 60, 58);          // 6
        await EditAsync(driver, "move", new { vertices = new[] { bottom.A, bottom.B }, by = new[] { 0, 0.17, 0 } }, 60, 58);     // 7
        await EditAsync(driver, "extrude", new { faces = new[] { MinusXFace }, distance = 0.1 }, 64, 62);                         // 8

        // +X: the same by moving the edges.
        await EditAsync(driver, "extrude", new { faces = new[] { PlusXFace }, distance = 0, inset = 0.05 }, 68, 66);              // 9
        (top, bottom) = await TopAndBottomAsync(driver, PlusXFace);
        await EditAsync(driver, "move", new { edges = new[] { new[] { top.A, top.B } }, by = new[] { 0, -0.17, 0 } }, 68, 66);   // 10
        await EditAsync(driver, "move", new { edges = new[] { new[] { bottom.A, bottom.B } }, by = new[] { 0, 0.17, 0 } }, 68, 66); // 11
        var crate = await EditAsync(driver, "extrude", new { faces = new[] { PlusXFace }, distance = 0.1 }, 72, 70);              // 12

        if (!crate.GetProperty("closed").GetBoolean() || crate.GetProperty("warnings").GetArrayLength() != 0)
        {
            driver.Fail($"crate: expected closed and no warnings, got {crate}");
        }

        CheckBounds(driver, crate, [-0.02, 0, 0], [1.02, 0.8, 1]);
        return crate;
    }

    public static async Task<JsonElement> DefineAsync(ScenarioDriver driver)
    {
        var report = await CallJsonAsync(driver, "define_mesh", new { vertices = CubeVertices, faces = CubeFaces });
        CheckCounts(driver, "define_mesh", report, 8, 6);
        return report;
    }

    /// <summary>One edit call: no tool error, the expected counts, still closed.</summary>
    public static async Task<JsonElement> EditAsync(ScenarioDriver driver, string tool, object arguments, int vertices, int faces)
    {
        var report = await CallJsonAsync(driver, tool, arguments);
        CheckCounts(driver, tool, report, vertices, faces);
        return report;
    }

    public static async Task<JsonElement> CallJsonAsync(ScenarioDriver driver, string tool, object arguments) =>
        JsonDocument.Parse(await CallTextAsync(driver, tool, arguments)).RootElement.Clone();

    /// <summary>A call that must succeed; its first text block.</summary>
    public static async Task<string> CallTextAsync(ScenarioDriver driver, string tool, object arguments)
    {
        var result = await driver.CallAsync(tool, arguments);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError is true)
        {
            driver.Fail($"{tool}: {text}");
        }

        return text;
    }

    public static void CheckCounts(ScenarioDriver driver, string tool, JsonElement report, int vertices, int faces)
    {
        var (v, f) = (report.GetProperty("vertices").GetInt32(), report.GetProperty("faces").GetInt32());
        if (v != vertices || f != faces || !report.GetProperty("closed").GetBoolean())
        {
            driver.Fail($"{tool}: expected {vertices} vertices, {faces} faces, closed; got {report}");
        }
    }

    public static void CheckBounds(ScenarioDriver driver, JsonElement report, double[] min, double[] max)
    {
        var bounds = report.GetProperty("bounds");
        for (var i = 0; i < 3; i++)
        {
            var (lo, hi) = (bounds.GetProperty("min")[i].GetDouble(), bounds.GetProperty("max")[i].GetDouble());
            if (Math.Abs(lo - min[i]) > 1e-4 || Math.Abs(hi - max[i]) > 1e-4)
            {
                driver.Fail($"crate: bounds expected ({string.Join(", ", min)}) to ({string.Join(", ", max)}), got {bounds}");
            }
        }
    }

    /// <summary>The face's highest and lowest edges in Y, as vertex pairs, read from get_mesh.</summary>
    public static async Task<((int A, int B) Top, (int A, int B) Bottom)> TopAndBottomAsync(ScenarioDriver driver, int face)
    {
        var mesh = await CallJsonAsync(driver, "get_mesh", new { });
        var y = mesh.GetProperty("vertices").EnumerateArray().Select(v => v.GetProperty("at")[1].GetDouble()).ToArray();
        var loop = mesh.GetProperty("faces")[face].GetProperty("vertices").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var level = Enumerable.Range(0, loop.Length)
            .Select(k => (A: loop[k], B: loop[(k + 1) % loop.Length]))
            .Where(e => Math.Abs(y[e.A] - y[e.B]) < 1e-6)
            .OrderBy(e => y[e.A])
            .ToArray();
        if (level.Length != 2)
        {
            driver.Fail($"face {face}: expected two level edges, found {level.Length}");
        }

        return (level[1], level[0]);
    }

    /// <summary>A call that must be a tool error whose text names each of <paramref name="mentions"/>. Returns the text.</summary>
    public static async Task<string> ExpectToolErrorAsync(ScenarioDriver driver, string tool, object arguments, params string[] mentions)
    {
        var result = await driver.CallAsync(tool, arguments);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError is not true)
        {
            driver.Fail($"{tool} {JsonSerializer.Serialize(arguments)} was not a tool error: {text}");
        }

        foreach (var m in mentions)
        {
            if (!text.Contains(m, StringComparison.OrdinalIgnoreCase))
            {
                driver.Fail($"{tool} error should name '{m}': {text}");
            }
        }

        return text;
    }

    public static bool IsBackground((byte R, byte G, byte B, byte A) p) =>
        p.R is >= 47 and <= 49 && p.G is >= 51 and <= 53 && p.B is >= 55 and <= 57;

    public static void CheckCorners(ScenarioDriver driver, ScenarioFrame frame)
    {
        var (w, h) = (frame.Width - 1, frame.Height - 1);
        foreach (var (x, y) in new[] { (0, 0), (w, 0), (0, h), (w, h) })
        {
            var p = frame.Pixel(x, y);
            if (!IsBackground(p))
            {
                driver.Fail($"{frame.Name}: a corner at ({x}, {y}) is {p}, not the background");
            }
        }
    }

    /// <summary>Faces must differ in the green channel by at least this much (10/255, as in M-1).</summary>
    public const int MinimumStep = 10;

    // front: the top, +Z and +X frame faces in M-1's order (brightest first), each in the middle of its
    // 0.12 frame band: the top frame's left band, the +Z frame's left band, the +X frame's near band.
    // Then the +X bar's top face (216, lit like the lid) and the +X panel above it (171, the recess floor
    // facing +X): they differ only because the bar stands there.
    public static readonly (int X, int Y) FrontTop = (78, 155);
    public static readonly (int X, int Y) FrontPlusZ = (71, 300);
    public static readonly (int X, int Y) FrontPlusX = (307, 300);
    public static readonly (int X, int Y) FrontBarTop = (350, 288);
    public static readonly (int X, int Y) FrontPanelBehindBar = (350, 240);

    /// <summary>
    /// What every native front frame of the crate must show: the corners are background, the centre is the mesh, the
    /// top, +Z and +X frame faces are in order (brightest first) at their points, and the +X bar's top differs from the
    /// panel behind it. A mirrored camera puts +X where +Z is, and an up-flipped one the lid at the bottom, so either fails.
    /// </summary>
    public static void CheckFront(ScenarioDriver driver, ScenarioFrame frame)
    {
        CheckCorners(driver, frame);
        CheckNotBackground(driver, frame, (256, 256), "the centre");
        CheckOrder(driver, frame, [FrontTop, FrontPlusZ, FrontPlusX], "top", "+Z", "+X");
        CheckDiffers(driver, frame, FrontBarTop, FrontPanelBehindBar, "the +X bar's top", "the +X panel behind it");
    }

    public static void CheckNotBackground(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, string what)
    {
        if (IsBackground(frame.Pixel(at.X, at.Y)))
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is the background, expected the mesh");
        }
    }


    /// <summary>Two sampled pixels, both on the mesh, whose green values differ by at least <see cref="MinimumStep"/>.</summary>
    public static void CheckDiffers(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) a, (int X, int Y) b, string whatA, string whatB)
    {
        CheckNotBackground(driver, frame, a, whatA);
        CheckNotBackground(driver, frame, b, whatB);
        var (ga, gb) = (frame.Pixel(a.X, a.Y).G, frame.Pixel(b.X, b.Y).G);
        if (Math.Abs(ga - gb) < MinimumStep)
        {
            driver.Fail($"{frame.Name}: {whatA} at ({a.X}, {a.Y}) is {ga} and {whatB} at ({b.X}, {b.Y}) is {gb}; they should differ by at least {MinimumStep}");
        }
    }

    /// <summary>Three faces, brightest first, each at least <see cref="MinimumStep"/> above the next.</summary>
    public static void CheckOrder(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y)[] at, params string[] faces)
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

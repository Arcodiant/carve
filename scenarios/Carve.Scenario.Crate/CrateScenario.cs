using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Crate;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class CrateExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, CrateScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The milestone's sample: a 1 x 0.8 x 1 crate (frame 0.12, panels recessed 0.08, a handle bar on each
/// X side standing 0.02 proud) built from the example cube with <c>move</c> and <c>extrude</c> alone,
/// then seen from five views, then undone and mis-called. Frames are in <c>frames/</c> of the run folder.
/// </summary>
public sealed class CrateScenario : IScenario
{
    /// <summary>Faces must differ in the green channel by at least this much (10/255, as in M-1).</summary>
    private const int MinimumStep = 10;

    // ---- sampled pixels (x, y from the top left of a 512 x 512 frame), picked from the first good run ----
    // Each sits well inside one flat face (at least 8 pixels from an edge), so it reads one shade; the shades
    // in that run: top 216, +Z 190, +X 171, background 53 (green channel).

    // front: the top, +Z and +X frame faces in M-1's order (brightest first), each in the middle of its
    // 0.12 frame band: the top frame's left band, the +Z frame's left band, the +X frame's near band.
    // Then the +X bar's top face (216, lit like the lid) and the +X panel above it (171, the recess floor
    // facing +X): they differ only because the bar stands there.
    private static readonly (int X, int Y) FrontTop = (78, 155);
    private static readonly (int X, int Y) FrontPlusZ = (71, 300);
    private static readonly (int X, int Y) FrontPlusX = (307, 300);
    private static readonly (int X, int Y) FrontBarTop = (350, 288);
    private static readonly (int X, int Y) FrontPanelBehindBar = (350, 240);

    // back, seen from below, so the -X bar's top is hidden: its underside (118, unlit) is the face that shows,
    // against the -X face just above it (196: the panel and the bar's outer face, one shade).
    private static readonly (int X, int Y) BackBarUnderside = (370, 238);
    private static readonly (int X, int Y) BackPanelBehindBar = (370, 190);

    // [-2.2, 1.2, 1.6]: the camera on the -X side and above, so the -X bar's top is seen (216) against
    // the -X panel above it (190).
    private static readonly (int X, int Y) LeftBarTop = (180, 291);
    private static readonly (int X, int Y) LeftPanelBehindBar = (180, 240);

    // [0.4, 2, 0.8]: the lid's recess wall along the far edge (118, in ambient light only) against the lid
    // panel it surrounds (234). A head-on top view shows no wall, which is why this view was chosen.
    private static readonly (int X, int Y) TiltedRecessWall = (300, 124);
    private static readonly (int X, int Y) TiltedLidPanel = (300, 180);

    // zoom 3 on the +X bar: the bar's top (216) and its outer +X face (193), both near the centre.
    private static readonly (int X, int Y) ZoomBarTop = (250, 204);
    private static readonly (int X, int Y) ZoomBarFront = (250, 300);

    private static readonly (int X, int Y) Centre = (256, 256);

    public async Task RunAsync(ScenarioDriver driver)
    {
        // 1. The crate in 12 calls (CrateBuild), counts checked after each.
        var crate = await CrateBuild.BuildAsync(driver);

        // 2. The five agreed views, then a repeat of the first.
        var front = await driver.CaptureAsync("crate-front", new { view = "front" });
        var back = await driver.CaptureAsync("crate-back", new { view = "back" });
        var left = await driver.CaptureAsync("crate-dir-m2.2-1.2-1.6", new { direction = new[] { -2.2, 1.2, 1.6 } });
        var tilted = await driver.CaptureAsync("crate-dir-0.4-2-0.8", new { direction = new[] { 0.4, 2, 0.8 } });
        var zoom = await driver.CaptureAsync("crate-dir-2-1-1-zoom3", new { direction = new[] { 2, 1, 1 }, zoom = 3, target = new[] { 1.02, 0.4, 0.5 } });
        var frontAgain = await driver.CaptureAsync("crate-front-again", new { view = "front" });
        driver.AssertBytesEqual("crate front, rendered again", front.Pixels, frontAgain.Pixels);

        // Every whole-mesh frame: the four corners are background.
        foreach (var frame in new[] { front, back, left, tilted })
        {
            CheckCorners(driver, frame);
            CheckNotBackground(driver, frame, Centre, "the centre");
        }

        CheckOrder(driver, front, [FrontTop, FrontPlusZ, FrontPlusX], "top", "+Z", "+X");
        CheckDiffers(driver, front, FrontBarTop, FrontPanelBehindBar, "the +X bar's top", "the +X panel behind it");
        CheckDiffers(driver, back, BackBarUnderside, BackPanelBehindBar, "the -X bar's underside", "the -X panel behind it");
        CheckDiffers(driver, left, LeftBarTop, LeftPanelBehindBar, "the -X bar's top", "the -X panel behind it");
        CheckDiffers(driver, tilted, TiltedRecessWall, TiltedLidPanel, "the lid's recess wall", "the lid panel");
        CheckNotBackground(driver, zoom, Centre, "the mesh at the centre");
        CheckDiffers(driver, zoom, ZoomBarTop, ZoomBarFront, "the bar's top", "the bar's front");

        // 3. Undo the last extrude: the +X bar's standing part is gone; repeating it brings the frame back.
        var undone = await CallJsonAsync(driver, "undo", new { });
        if (undone.GetProperty("undone").GetString() != "extrude" || undone.GetProperty("faces").GetInt32() != 66)
        {
            driver.Fail($"undo: expected to undo an extrude back to 66 faces, got {undone}");
        }

        var withoutBar = await driver.CaptureAsync("crate-front-undone", new { view = "front" });
        if (withoutBar.Pixels.SequenceEqual(front.Pixels))
        {
            driver.Fail("crate-front-undone: the frame is the same as with the +X bar standing");
        }

        // The bar's top is gone from the sampled point: it now shows the panel's shade, not the bar's.
        var barGone = Math.Abs(withoutBar.Pixel(FrontBarTop.X, FrontBarTop.Y).G - front.Pixel(FrontPanelBehindBar.X, FrontPanelBehindBar.Y).G);
        if (barGone >= MinimumStep)
        {
            driver.Fail($"crate-front-undone: the bar's sampled point still differs from the panel by {barGone}/255");
        }

        await EditAsync(driver, "extrude", new { faces = new[] { PlusXFace }, distance = 0.1 }, 72, 70);
        var redone = await driver.CaptureAsync("crate-front-redone", new { view = "front" });
        driver.AssertBytesEqual("crate front, after undo and the same extrude again", front.Pixels, redone.Pixels);

        // 4. Error path: each is a tool error naming the face, and the next render is the same as the last.
        await ExpectToolErrorAsync(driver, "extrude", new { faces = new[] { TopFace }, distance = 0, inset = 0.6 }, "inset", $"{TopFace}");
        await ExpectToolErrorAsync(driver, "extrude", new { faces = new[] { 99 }, distance = 0.1 }, "99");
        var afterErrors = await driver.CaptureAsync("crate-front-after-errors", new { view = "front" });
        driver.AssertBytesEqual("crate front, after two failed extrudes", redone.Pixels, afterErrors.Pixels);
    }

    private static void CheckNotBackground(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) at, string what)
    {
        if (IsBackground(frame.Pixel(at.X, at.Y)))
        {
            driver.Fail($"{frame.Name}: {what} at ({at.X}, {at.Y}) is the background, expected the mesh");
        }
    }


    /// <summary>Two sampled pixels, both on the mesh, whose green values differ by at least <see cref="MinimumStep"/>.</summary>
    private static void CheckDiffers(ScenarioDriver driver, ScenarioFrame frame, (int X, int Y) a, (int X, int Y) b, string whatA, string whatB)
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

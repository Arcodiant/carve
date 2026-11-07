using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Shed.Checks;

namespace Carve.Scenario.Shed;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class ShedExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, ShedScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// The leaky shed, opened from the sample file: every planted fault found with <c>check</c> and <c>select</c>, a selection
/// moved, and the shed shown in every look. Each pixel check is worked from the mesh itself (<see cref="Camera"/> and
/// <see cref="Shape.FirstHit"/>): what a pixel must show is what a ray through it meets first among the faces that face it,
/// so a mirrored or flipped camera, or a face drawn the wrong way round, puts the wrong colour at the wrong place.
/// </summary>
public sealed class ShedScenario : IScenario
{
    private static readonly Vector3 Min = Vector3.Zero;
    private static readonly Vector3 Max = new(4, 3.9f, 3);
    private static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f);

    public async Task RunAsync(ScenarioDriver driver)
    {
        var path = Path.Combine(Path.GetDirectoryName(typeof(ShedScenario).Assembly.Location) ?? AppContext.BaseDirectory, "leaky-shed.carve");
        if (!File.Exists(path))
        {
            driver.Fail($"the sample is not beside the scenario: {path}");
        }

        // 1. Open.
        var opened = await CallJsonAsync(driver, "open", new { path });
        Equal("open: vertices", 25, opened.GetProperty("vertices").GetInt32());
        Equal("open: faces", 18, opened.GetProperty("faces").GetInt32());
        Equal("open: edges", 38, opened.GetProperty("edges").GetInt32());
        Equal("open: closed", false, opened.GetProperty("closed").GetBoolean());
        SameList("open: warnings", ShedFacts.OpenWarnings, opened.GetProperty("warnings").EnumerateArray().Select(w => w.GetString() ?? ""));
        var shed = await Shape.ReadAsync(driver);
        var camera = new Camera(Min, Max, Front, 512);
        var shaded = await driver.CaptureAsync("shed-front", new { view = "front" });
        Caption(shaded, "512x512, view front, 25 vertices, 18 faces");
        ShadedFrame(shaded, camera, shed);

        // 2. Check.
        var check = await CallJsonAsync(driver, "check", new { });
        ShedFacts.CheckReport(check);
        var problems = check.GetProperty("problems").GetRawText();

        // 3. Select.
        var unattached = await SelectAsync(driver, "vertices", "unattached");
        Ids("vertices where unattached", [24], unattached, "vertices");
        var open = await SelectAsync(driver, "edges", "open");
        var openEdges = open.GetProperty("edges").EnumerateArray().Select(e => new[] { e[0].GetInt32(), e[1].GetInt32() }).ToArray();
        SameList("edges where open", ["4-5", "4-10", "5-6", "6-7", "7-8", "8-9", "9-11", "10-11"], openEdges.Select(e => $"{e[0]}-{e[1]}"));
        Ids("faces where normal.y > 0.5 (the slope, and the inside-out chimney's underside)", [11, 14], await SelectAsync(driver, "faces", "normal.y > 0.5"), "faces");
        var chimney = await SelectAsync(driver, "faces", "min.y >= 3.6");
        Ids("faces where min.y >= 3.6", [12, 13, 14, 15, 16, 17], chimney, "faces");
        var none = await SelectAsync(driver, "faces", "y > 10");
        Ids("faces where y > 10", [], none, "faces");
        Equal("faces where y > 10: count", 0, none.GetProperty("count").GetInt32());
        await ExpectToolErrorAsync(driver, "select", new { kind = "faces", where = "y >> 10" }, "at character 3", "'>>' is not an operator", "use <, <=, ==, !=, >= or >");

        // 5. Looks, on the shed as it was opened.
        await ShedLooks.RunAsync(driver, shed, camera, shaded, openEdges);

        // 4. A selection feeds an edit.
        var chimneyFaces = chimney.GetProperty("faces").EnumerateArray().Select(f => f.GetInt32()).ToArray();
        var moved = await CallJsonAsync(driver, "move", new { faces = chimneyFaces, by = new[] { 0.0, 0.5, 0.0 } });
        SameList("move: moved", Enumerable.Range(16, 8).Select(i => $"{i}"), moved.GetProperty("moved").EnumerateArray().Select(v => $"{v.GetInt32()}"));
        var after = await CallJsonAsync(driver, "check", new { });
        Equal("check after the move: shells", 2, after.GetProperty("stats").GetProperty("shells").GetInt32());
        Equal("check after the move: the problems", problems, after.GetProperty("problems").GetRawText());
        var movedShed = await Shape.ReadAsync(driver);
        var chimneyBottom = Enumerable.Range(16, 8).Min(v => movedShed.Vertices[v].Y);
        if (Math.Abs(chimneyBottom - 4.1f) > 1e-4)
        {
            Note($"get_mesh: the chimney's min.y is {chimneyBottom}, expected 4.1 (3.6 + 0.5)");
        }

        var movedMax = new Vector3(4, 4.4f, 3);
        var movedCamera = new Camera(Min, movedMax, Front, 512);
        var movedFrame = await driver.CaptureAsync("shed-front-after-move", new { view = "front" });
        Caption(movedFrame, "512x512, view front, 25 vertices, 18 faces");
        ShadedFrame(movedFrame, movedCamera, movedShed);
        ChimneyRose(shaded, camera, shed, movedFrame, movedCamera, movedShed);

        // 6. Error path: a pair that is not an edge, and the next render is the one before.
        await ExpectToolErrorAsync(driver, "move", new { edges = new[] { new[] { 3, 9 } }, by = new[] { 0.0, 0.5, 0.0 } }, "3", "9");
        var afterError = await driver.CaptureAsync("shed-front-after-error", new { view = "front" });
        driver.AssertBytesEqual("shed-front after the failed move, and after the move", movedFrame.Pixels, afterError.Pixels);

        if (Failures.Count > 0)
        {
            driver.Fail($"{Failures.Count} check(s) failed:\n" + string.Join("\n", Failures.Select(f => "  " + f)));
        }
    }

    private static async Task<JsonElement> SelectAsync(ScenarioDriver driver, string kind, string where) =>
        await CallJsonAsync(driver, "select", new { kind, where });

    private static void Ids(string what, int[] expected, JsonElement result, string kind)
    {
        SameList(what, expected.Select(i => $"{i}"), result.GetProperty(kind).EnumerateArray().Select(i => $"{i.GetInt32()}"));
        Equal($"{what}: count", expected.Length, result.GetProperty("count").GetInt32());
    }

    /// <summary>A plain shaded frame: where a ray meets a face that faces it the frame shows the mesh, elsewhere the background.</summary>
    internal static void ShadedFrame(ScenarioFrame frame, Camera camera, Shape shape)
    {
        Corners(frame);
        var tested = Samples(frame, camera, shape, (0, 0), frame.Width, _ => null, background: true, "shaded");
        if (tested < 20)
        {
            Note($"{frame.Name}: only {tested} samples were surely a face or the background");
        }
    }

    /// <summary>The chimney's rise: sky where it was, and chimney above that.</summary>
    private static void ChimneyRose(ScenarioFrame before, Camera beforeCamera, Shape beforeShape, ScenarioFrame after, Camera afterCamera, Shape afterShape)
    {
        var chimneyBefore = Enumerable.Range(12, 6).Where(f => f < beforeShape.Faces.Length && f < afterShape.Faces.Length).ToHashSet();
        var wasSky = 0;
        var nowChimney = 0;
        foreach (var face in chimneyBefore)
        foreach (var point in beforeShape.Samples(face).Concat(afterShape.Samples(face)))
        {
            var (bx, by) = beforeCamera.Pixel(point);
            var (ax, ay) = afterCamera.Pixel(point);
            var (seenBefore, seenAfter) = (beforeCamera.Seen(beforeShape, bx, by), afterCamera.Seen(afterShape, ax, ay));

            // A pixel that was chimney is sky now (in the frame after, at the same place).
            var (sx, sy) = (bx, by);
            if (seenBefore >= 12 && sx > 4 && sy > 4 && sx < 508 && sy < 508 && afterCamera.Seen(afterShape, sx, sy) == -1)
            {
                wasSky++;
                Background(after, (sx, sy), $"the chimney's pixel ({sx}, {sy}) before the move");
                NotBackground(before, (sx, sy), "the same pixel before the move");
            }

            // A pixel that was sky above it is chimney now.
            if (seenAfter >= 12 && beforeCamera.Seen(beforeShape, ax, ay) == -1)
            {
                nowChimney++;
                NotBackground(after, (ax, ay), "a pixel that is the chimney after the move");
                Background(before, (ax, ay), "the same pixel before the move");
            }
        }

        if (wasSky == 0 || nowChimney == 0)
        {
            Note($"after-move: {wasSky} pixels that were chimney and are sky, {nowChimney} that were sky and are chimney: expected some of each");
        }
    }
}

using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Morph;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class MorphExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, MorphScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// Morph targets seen (M-21, T-103). The first stage is the M-18 hard-rimmed cylinder (8 sides, 3 rings, both rims hard, one side
/// edge hard) made 2 m tall so that the framing, which stays on the rest bounds, holds its bulge. A <c>Bulge</c> target scales its middle ring
/// 1.4x; it is drawn from +z at weights 0, 0.5 and 1, once in the normals look at 1, and once posed by a two-bone skeleton. A
/// <c>Pinch</c> target pulls the middle ring in and lifts it above the top rim, so <c>check</c> names a flip and the render shows it.
/// Every pixel check is worked from the geometry: the silhouette's half-width at a height, from <c>get_mesh</c>'s morphed positions
/// of the target and the framing rule of the rest bounds. T-105 adds the arm as later stages.
/// </summary>
public sealed class MorphScenario : IScenario
{
    private const int Size = 512;
    private const int Sides = 8;
    private const float Radius = 0.5f;
    private const float RingHeight = 1f;
    private const float Margin = 0.08f;

    // Rest bounds: the frame never moves with the weights (D-6).
    private static readonly Vector3 Min = new(-Radius, 0, -Radius);
    private static readonly Vector3 Max = new(Radius, 2 * RingHeight, Radius);

    private static readonly object FrontView = new { view = "+z", projection = "orthographic" };

    public async Task RunAsync(ScenarioDriver driver)
    {
        // The arm with dials (T-105) first, on the empty model, so that its samples hold the arm alone; the cylinder then replaces its mesh.
        await ArmStages.RunAsync(driver);

        await DefineCylinderAsync(driver);

        // Bulge: the middle ring (vertices 8 to 15) 1.4x about the axis.
        var ring = Enumerable.Range(Sides, Sides).ToArray();
        await CallJsonAsync(driver, "define_target", new { name = "Bulge", positions = Positions(ring, 1.4f) });
        var drawn = Ring(driver, await CallJsonAsync(driver, "get_mesh", new { target = "Bulge" }), "Bulge");
        var bulge = drawn.Max(p => Math.Abs(p.X));
        if (MathF.Abs(bulge - 1.4f * Radius) > 1e-5f) driver.Fail($"get_mesh: Bulge's middle ring reaches x {bulge}, expected {1.4f * Radius}");

        // check: Bulge alone breaks nothing, at rest and at weights.
        await ExpectCleanAsync(driver, new { });
        await ExpectCleanAsync(driver, new { targets = W("Bulge", 0.5) });

        // ---- the frames ----
        var rest = await driver.CaptureAsync("morph-rest-z", FrontView);
        var zero = await driver.CaptureAsync("morph-bulge-0-z", new { view = "+z", projection = "orthographic", targets = W("Bulge", 0) });
        driver.AssertBytesEqual("Bulge at weight 0 is the rest mesh", rest.Pixels, zero.Pixels);
        CheckSilhouette(driver, rest, bulge, 0, true);

        var half = await driver.CaptureAsync("morph-bulge-half-z", new { view = "+z", projection = "orthographic", targets = W("Bulge", 0.5) });
        CheckSilhouette(driver, half, bulge, 0.5f, true);
        CheckDiffers(driver, rest, half, "the rest", "Bulge at 0.5");

        var one = await driver.CaptureAsync("morph-bulge-1-z", new { view = "+z", projection = "orthographic", targets = W("Bulge", 1) });
        CheckSilhouette(driver, one, bulge, 1, true);
        CheckDiffers(driver, half, one, "Bulge at 0.5", "Bulge at 1");

        var normals = await driver.CaptureAsync("morph-bulge-1-normals-z", new { view = "+z", projection = "orthographic", mode = "normals", targets = W("Bulge", 1) });
        CheckSilhouette(driver, normals, bulge, 1, false);
        var restNormals = await driver.CaptureAsync("morph-rest-normals-z", new { view = "+z", projection = "orthographic", mode = "normals" });
        CheckDiffers(driver, restNormals, normals, "the rest normals", "Bulge's normals at 1");

        // A sheet and the wireframe take the same weights.
        var sheet = await driver.CaptureAsync("morph-bulge-1-wire-sheet", new { views = new[] { "+z", "+x" }, projection = "orthographic", mode = "wireframe", size = 256, targets = W("Bulge", 1) });
        if (sheet.Width != 2 * 256 + 2) driver.Fail($"{sheet.Name}: expected a sheet 514 wide, got {sheet.Width}");

        // Posed: a two-bone arm of the cylinder, bent at the middle ring, with the bulge before the skin.
        await CallJsonAsync(driver, "define_skeleton", new
        {
            bones = new object[]
            {
                new { name = "Base", parent = (string?)null, position = new[] { 0, 0, 0 }, rotation = new[] { 0, 0, 0, 1 } },
                new { name = "Top", parent = "Base", position = new[] { 0, 1, 0 }, rotation = new[] { 0, 0, 0, 1 } },
            },
        });
        await CallJsonAsync(driver, "weigh", new { });
        var s = MathF.Sin(MathF.PI / 8);
        var c = MathF.Cos(MathF.PI / 8);
        var bent = new Dictionary<string, object> { ["Top"] = new { rotation = new[] { 0, 0, s, c } } };
        var posed = await driver.CaptureAsync("morph-posed-z", new { view = "+z", projection = "orthographic", pose = bent });
        var posedBulge = await driver.CaptureAsync("morph-posed-bulge-1-z", new { view = "+z", projection = "orthographic", pose = bent, targets = W("Bulge", 1) });
        CheckDiffers(driver, posed, posedBulge, "the pose", "the pose with Bulge at 1");
        CheckDiffers(driver, one, posedBulge, "Bulge at 1 at rest", "Bulge at 1 posed");
        var posedZero = await driver.CaptureAsync("morph-posed-bulge-0-z", new { view = "+z", projection = "orthographic", pose = bent, targets = W("Bulge", 0) });
        driver.AssertBytesEqual("posed with Bulge at weight 0 is the pose alone", posed.Pixels, posedZero.Pixels);

        // The range: 0 to 1, named.
        await ExpectToolErrorAsync(driver, "render", new { targets = W("Bulge", 1.5) }, "targets.Bulge", "outside 0 to 1");
        await ExpectToolErrorAsync(driver, "check", new { targets = W("Bulge", -0.1) }, "targets.Bulge", "outside 0 to 1");
        await ExpectToolErrorAsync(driver, "render", new { targets = W("Bulgy", 1) }, "Bulgy", "'Bulge'");

        // Pinch: the middle ring pulled in to half and lifted to y 2.4, above the top rim, so the eight faces between it and the top rim turn inside out. (A radial move alone, even through the axis, cannot turn a face: its Newell normal keeps its outward part.)
        await CallJsonAsync(driver, "define_target", new { name = "Pinch", positions = Positions(ring, 0.5f, 2.4f) });
        var check = await CallJsonAsync(driver, "check", new { });
        var named = check.GetProperty("problems").EnumerateArray()
            .Where(p => p.GetProperty("check").GetString() == "target-flips-face").ToArray();
        if (named.Length != 1 || named[0].GetProperty("element").GetString() != "targets['Pinch']")
            driver.Fail($"check: expected one target-flips-face naming targets['Pinch'], got {check.GetProperty("problems")}");
        else if (!named[0].GetProperty("message").GetString()!.Contains("8 faces turn"))
            driver.Fail($"check: Pinch should turn the 8 upper side faces, got '{named[0].GetProperty("message").GetString()}'");
        if (check.GetProperty("problems").EnumerateArray().Any(p => p.GetProperty("element").GetString()!.Contains("Bulge")))
            driver.Fail("check: Bulge must not be named");
        var pinchNormals = await driver.CaptureAsync("morph-pinch-1-normals-z", new { view = "+z", projection = "orthographic", mode = "normals", targets = W("Pinch", 1) });
        CheckDiffers(driver, restNormals, pinchNormals, "the rest normals", "Pinch's normals at 1");
        var pinch = await driver.CaptureAsync("morph-pinch-1-z", new { view = "+z", projection = "orthographic", targets = W("Pinch", 1) });
        CheckDiffers(driver, rest, pinch, "the rest", "Pinch at 1");
    }

    private static async Task DefineCylinderAsync(ScenarioDriver driver)
    {
        var vertices = new List<double[]>();
        for (var i = 0; i < 3; i++)
            for (var k = 0; k < Sides; k++)
            {
                var a = 2 * Math.PI * k / Sides;
                vertices.Add([Math.Round(Radius * Math.Cos(a), 6), RingHeight * i, Math.Round(-Radius * Math.Sin(a), 6)]);
            }
        var faces = new List<int[]>();
        for (var i = 0; i < 2; i++)
            for (var k = 0; k < Sides; k++)
                faces.Add([Sides * i + k, Sides * i + (k + 1) % Sides, Sides * (i + 1) + (k + 1) % Sides, Sides * (i + 1) + k]);
        faces.Add(Enumerable.Range(0, Sides).Reverse().ToArray());
        faces.Add(Enumerable.Range(2 * Sides, Sides).ToArray());
        await CallJsonAsync(driver, "define_mesh", new { vertices, faces, drop_targets = true });
        await CallJsonAsync(driver, "set_shading", new { shading = "smooth" });
        var hard = Enumerable.Range(0, Sides)
            .SelectMany(k => new[] { new[] { k, (k + 1) % Sides }, new[] { 2 * Sides + k, 2 * Sides + (k + 1) % Sides } })
            .Concat([[0, Sides], [Sides, 2 * Sides]]).ToArray();
        await CallJsonAsync(driver, "mark_edges", new { edges = hard });
    }

    /// <summary>Absolute positions of the ring's vertices with x and z scaled by <paramref name="scale"/> about the axis.</summary>
    private static Dictionary<string, double[]> Positions(int[] ring, float scale, float y = RingHeight)
    {
        var rest = new Dictionary<string, double[]>();
        for (var j = 0; j < ring.Length; j++)
        {
            var a = 2 * Math.PI * j / Sides;
            rest[ring[j].ToString()] = [Math.Round(Radius * Math.Cos(a) * scale, 6), y, Math.Round(-Radius * Math.Sin(a) * scale, 6)];
        }
        return rest;
    }

    private static Vector3[] Ring(ScenarioDriver driver, JsonElement mesh, string target)
    {
        var positions = mesh.GetProperty("target").GetProperty("positions");
        if (positions.EnumerateObject().Count() != Sides) driver.Fail($"get_mesh {target}: expected {Sides} moved vertices, got {positions}");
        return positions.EnumerateObject()
            .Select(p => new Vector3(p.Value[0].GetSingle(), p.Value[1].GetSingle(), p.Value[2].GetSingle())).ToArray();
    }

    private static async Task ExpectCleanAsync(ScenarioDriver driver, object arguments)
    {
        var check = await CallJsonAsync(driver, "check", arguments);
        if (!check.GetProperty("ok").GetBoolean()) driver.Fail($"check {JsonSerializer.Serialize(arguments)}: expected ok, got {check.GetProperty("problems")} {check.GetProperty("warnings")}");
    }

    /// <summary>
    /// Where a point lands, in pixels from the top left, in an orthographic frame from +z of the rest bounds (<c>render</c>'s framing rule:
    /// the drawn space puts the box's centre at the origin and its half-diagonal at 1; the frame holds the largest half-extent in x or y,
    /// with 8% margin).
    /// </summary>
    private static Func<Vector3, Vector2> FrontPixels()
    {
        var centre = (Min + Max) / 2;
        var radius = (Max - Min).Length() / 2;
        var reach = MathF.Max((Max.X - Min.X) / 2, (Max.Y - Min.Y) / 2) / radius;
        var half = reach / (1 - 2 * Margin);
        return p =>
        {
            var d = (p - centre) / radius;
            return new Vector2((d.X / half + 1) / 2 * Size, (1 - d.Y / half) / 2 * Size);
        };
    }

    /// <summary>
    /// The silhouette from +z at <paramref name="weight"/>: the half-width at height y is the rest radius, rising to the bulge's at the
    /// middle ring (the ring's vertex on the x axis, from <c>get_mesh</c>) and back, linear between rings. A point 0.03 m inside is drawn
    /// and, for a look with nothing beyond the surface, one 0.03 m outside is background. Sampled at the middle ring and near each rim's corner.
    /// </summary>
    private static void CheckSilhouette(ScenarioDriver driver, ScenarioFrame frame, float bulgeRadius, float weight, bool outside)
    {
        var pixel = FrontPixels();
        var middle = Radius + (bulgeRadius - Radius) * weight;
        float HalfWidth(float y) => Radius + (middle - Radius) * (1 - MathF.Abs(y - RingHeight) / RingHeight);
        foreach (var y in new[] { 1f, 0.5f, 1.5f, 0.08f, 1.92f })
        {
            foreach (var side in new[] { -1, 1 })
            {
                var edge = HalfWidth(y);
                var inside = Pixel(pixel(new Vector3(side * (edge - 0.03f), y, 0)));
                if (IsBackground(frame.Pixel(inside.X, inside.Y)))
                    driver.Fail($"{frame.Name}: the surface should be drawn at x {side * (edge - 0.03f):0.###}, y {y} (pixel {inside}), the silhouette's half-width being {edge:0.###}");
                if (!outside) continue;
                var beyond = Pixel(pixel(new Vector3(side * (edge + 0.03f), y, 0)));
                if (!IsBackground(frame.Pixel(beyond.X, beyond.Y)))
                    driver.Fail($"{frame.Name}: the background should show at x {side * (edge + 0.03f):0.###}, y {y} (pixel {beyond}), the silhouette's half-width being {edge:0.###}");
            }
        }
    }

    /// <summary>The <c>targets</c> argument: one name and weight, with the name's case as given (an anonymous object is camel-cased).</summary>
    private static Dictionary<string, double> W(string name, double weight) => new() { [name] = weight };

    private static (int X, int Y) Pixel(Vector2 p) => ((int)MathF.Round(p.X), (int)MathF.Round(p.Y));

    private static void CheckDiffers(ScenarioDriver driver, ScenarioFrame a, ScenarioFrame b, string whatA, string whatB)
    {
        if (a.Pixels.AsSpan().SequenceEqual(b.Pixels)) driver.Fail($"{b.Name}: {whatB} draws the same as {whatA}, pixel for pixel");
    }
}

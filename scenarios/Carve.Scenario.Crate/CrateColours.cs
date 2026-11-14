using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using static Carve.Scenario.Crate.CameraPixels;
using static Carve.Scenario.Crate.CrateBuild;
using static Carve.Scenario.Crate.CrateLooks;

namespace Carve.Scenario.Crate;

/// <summary>
/// The crate with a coloured frame and blue panels (T-30), after everything else so that every earlier frame stays as it
/// was: built again, its faces put in two groups and coloured, then the front and tilted frames beside the grey ones.
/// <para>
/// Each check is at a point of the mesh projected by <see cref="CameraPixels"/>, so a mirrored or flipped camera puts it
/// on the wrong face. The surface is lit, and every channel of a lit pixel is the surface colour times one light factor,
/// so the same pixel in the grey frame of the same view, whose surface is (0.72, 0.72, 0.70), gives that factor: the
/// coloured pixel, in linear light, must be the grey one times colour / grey in each channel.
/// </para>
/// </summary>
internal static class CrateColours
{
    private const string Wood = "#C8503C";
    private const string Blue = "#3C78C8";
    /// <summary>How far a channel, in linear light, may be from the one predicted, as a fraction of the prediction (8 bit steps and the pixel's own blending).</summary>
    private const double Relative = 0.08;

    /// <summary>Linear values below this are within one 8 bit step of black and are not judged.</summary>
    private const double Floor = 0.004;

    public static async Task RunAsync(ScenarioDriver driver, ScenarioFrame greyFront, ScenarioFrame greyTilted)
    {
        await BuildAsync(driver);
        var crate = await ReadAsync(driver);
        var frontPixel = Perspective(CrateMin, CrateMax, Front, Size);
        var tiltedPixel = Perspective(CrateMin, CrateMax, Tilted, Size);

        // The six faces of the cube the crate was built from keep their ids and are its panels; everything the extrudes
        // added is the frame (the walls round each panel, the bar and the lid's band).
        var panels = AllFaces;
        var frame = Enumerable.Range(0, crate.Faces.Length).Except(panels).ToArray();
        await CallJsonAsync(driver, "define_group", new { name = "panels", faces = panels });
        var grouped = await CallJsonAsync(driver, "define_group", new { name = "frame", faces = frame });
        if (grouped.GetProperty("groups").GetArrayLength() != 2)
        {
            driver.Fail($"define_group: expected two groups, got {grouped}");
        }

        // Refusals change nothing.
        await ExpectToolErrorAsync(driver, "set_colour", new { group = "panels", colour = "blue" }, "colour", "#RRGGBB");
        await ExpectToolErrorAsync(driver, "set_colour", new { group = "lid", colour = Blue }, "lid", "'panels', 'frame'");
        await ExpectToolErrorAsync(driver, "set_colour", new { faces = new[] { 99 }, colour = Blue }, "99");
        await ExpectToolErrorAsync(driver, "set_colour", new { group = "panels", faces = new[] { 0 }, colour = Blue }, "not both");

        await CallJsonAsync(driver, "set_colour", new { group = "panels", colour = Blue });
        await CallJsonAsync(driver, "set_colour", new { group = "frame", colour = Wood });

        var mesh = await CallJsonAsync(driver, "get_mesh", new { });
        var reported = mesh.GetProperty("colours").EnumerateArray()
            .ToDictionary(c => c.GetProperty("colour").GetString()!, c => c.GetProperty("faces").GetArrayLength());
        if (reported.Count != 2 || reported[Blue] != panels.Length || reported[Wood] != frame.Length || mesh.GetProperty("groups").GetArrayLength() != 2)
        {
            driver.Fail($"get_mesh: expected {panels.Length} blue faces, {frame.Length} wood faces and two groups, got {mesh.GetProperty("colours")}");
        }

        var front = await driver.CaptureAsync("crate-coloured-front", new { view = "front" });
        var tilted = await driver.CaptureAsync("crate-coloured-dir-0.4-2-0.8", new { direction = new[] { 0.4, 2, 0.8 } });
        var again = await driver.CaptureAsync("crate-coloured-front-again", new { view = "front" });
        driver.AssertBytesEqual("coloured crate front, rendered again", front.Pixels, again.Pixels);

        // The lid's panel is face 3 and the +Z band and the +X band belong to the frame; from the front all three are seen.
        const float Near = 0.001f;
        var plusZBand = crate.Find(p => p.Z > 1 - Near && p.Y > 0.68f - Near);
        var plusXBand = crate.Find(p => Math.Abs(p.X - 1) < Near && p.Y > 0.68f - Near);
        CheckSurface(driver, front, greyFront, frontPixel, Front, Vector3.UnitY, PanelSeenFromFront, Blue, "the lid panel, blue");
        CheckSurface(driver, front, greyFront, frontPixel, Front, Vector3.UnitZ, crate.Samples(plusZBand), Wood, "the +Z band, wood");
        CheckSurface(driver, front, greyFront, frontPixel, Front, Vector3.UnitX, crate.Samples(plusXBand), Wood, "the +X band, wood");

        // From above the lid's panel and the +Z band are both in plain view.
        CheckSurface(driver, tilted, greyTilted, tiltedPixel, Tilted, Vector3.UnitY, crate.Samples(TopFace).Where(p => p.X is > 0.2f and < 0.8f && p.Z is > 0.3f and < 0.8f), Blue, "the lid panel from above, blue");
        CheckSurface(driver, tilted, greyTilted, tiltedPixel, Tilted, Vector3.UnitY, [new Vector3(0.5f, 0.8f, 0.98f), new Vector3(0.3f, 0.8f, 0.98f)], Wood, "the lid's front rim from above, wood");

        // A frame this colour is not the grey one anywhere the mesh is, and the corners are still background.
        CheckCorners(driver, front);
        CheckCorners(driver, tilted);
        if (front.Pixels.SequenceEqual(greyFront.Pixels))
        {
            driver.Fail("crate-coloured-front: the same as the grey crate");
        }
    }

    /// <summary>
    /// Each point's pixel, in linear light, is predicted from the grey frame's pixel there by <see cref="ColourRule.Predict"/>:
    /// T-82's diffuse times the colour over the grey surface, plus the PBR specular of a face of <paramref name="normal"/>
    /// seen along <paramref name="direction"/>.
    /// </summary>
    private static void CheckSurface(ScenarioDriver driver, ScenarioFrame coloured, ScenarioFrame grey, Func<Vector3, Vector2> pixel,
        Vector3 direction, Vector3 normal, IEnumerable<Vector3> points, string hex, string what)
    {
        var scale = ColourRule.Scale(hex);
        var specular = ColourRule.Specular(normal, direction);
        var checkedPoints = 0;
        foreach (var point in points)
        {
            var (x, y) = Holding(pixel(point));
            var c = coloured.Pixel(x, y);
            var k = grey.Pixel(x, y);
            double[] got = [Linear(c.R), Linear(c.G), Linear(c.B)];
            double[] lit = [Linear(k.R), Linear(k.G), Linear(k.B)];
            var predicted = ColourRule.Predict(lit, hex, normal, direction);
            for (var ch = 0; ch < 3; ch++)
            {
                var expected = predicted[ch];
                if (expected < Floor && got[ch] < Floor) continue;
                if (Math.Abs(got[ch] - expected) > Relative * expected + Floor)
                {
                    driver.Fail($"{coloured.Name}: {what} at ({x}, {y}) channel {"RGB"[ch]} is {got[ch]:F4} in linear light, expected {expected:F4} "
                        + $"((the grey frame's {lit[ch]:F4} - specular {specular:F4}) x {scale[ch]:F3} + specular); pixel {c}, grey pixel {k}");
                }
            }

            checkedPoints++;
        }

        if (checkedPoints == 0)
        {
            driver.Fail($"{coloured.Name}: {what}: no point to check");
        }
    }

    private static double Linear(int channel) => ColourRule.Linear(channel);
}

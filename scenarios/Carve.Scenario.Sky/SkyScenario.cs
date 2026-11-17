using System.Globalization;
using System.Numerics;
using System.Text;
using Carve.Debug;
using Carve.Extensibility;
using Carve.Scenario.Crate;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Carve.Scenario.Sky;

/// <summary>Registers the scenario, as every scenario extension does.</summary>
public sealed class SkyExtension : ICarveExtension
{
    public void ConfigureHost(CarveHostBuilderContext context) => context.Services.AddSingleton<IScenario, SkyScenario>();

    public void OnStarted(IServiceProvider services)
    {
    }
}

/// <summary>
/// M-19: what the fixed sky (M-7's <c>ProceduralSky.Default</c>) adds to a colour face, which the engine docs give no
/// numbers for. One unit quad at the origin, facing each of 26 world normals (the axes, the edge and the corner
/// diagonals), drawn grey (no colour), black and white, from <c>front</c>, <c>back</c> and straight on; the frame centre
/// is the quad's centre. A black face is specular alone (its albedo is 0), so grey less black is the diffuse, and the
/// diffuse over the albedo less T-82's ambient and directional parts is the sky's irradiance. Writes <c>sky.tsv</c>, the
/// measurements <see cref="ColourRule.SkyLight"/> and <see cref="ColourRule.SkySpecular"/> were fitted to, and checks
/// every pixel against ColourRule's model.
/// </summary>
public sealed class SkyScenario : IScenario
{
    private static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f);

    public async Task RunAsync(ScenarioDriver driver)
    {
        var table = new StringBuilder("normal\tview\tnv\tnl\tlight\tspecular\tgrey\tblack\twhite\n");
        foreach (var n in Normals())
        {
            var (vertices, faces) = Quad(n);
            var views = new List<(string View, Vector3 Direction, object Args)>
            {
                ("front", Front, new { view = "front" }),
                ("back", -Front, new { view = "back" }),
                ("own", n, new { direction = new[] { n.X, n.Y, n.Z } }),
            };
            var seen = views.Where(v => Vector3.Dot(Vector3.Normalize(n), Vector3.Normalize(v.Direction)) > 0.15f).ToList();
            var pixels = new Dictionary<(string, string), (byte R, byte G, byte B, byte A)>();
            foreach (var colour in new[] { "grey", "black", "white" })
            {
                await CallAsync(driver, "define_mesh", new { name = "probe", vertices, faces });
                if (colour != "grey")
                {
                    await CallAsync(driver, "set_colour", new { mesh = "probe", colour = colour == "black" ? "#000000" : "#FFFFFF", faces = new[] { 0 } });
                }

                foreach (var (view, _, args) in seen)
                {
                    var frame = await driver.CaptureAsync($"n{Key(n)}-{colour}-{view}", args);
                    pixels[(colour, view)] = frame.Pixel(frame.Width / 2, frame.Height / 2);
                }
            }

            foreach (var (view, direction, _) in seen)
            {
                var nn = Vector3.Normalize(n);
                var nv = Vector3.Dot(nn, Vector3.Normalize(direction));
                var l = ColourRule.ToLight(direction);
                var nl = Vector3.Dot(nn, l);
                var s = ColourRule.Specular(nn, direction);
                Expect(driver, $"n{Key(n)}-grey-{view}", pixels[("grey", view)], ColourRule.Grey, nn, direction, nl, 2);
                Expect(driver, $"n{Key(n)}-white-{view}", pixels[("white", view)], Vector3.One, nn, direction, nl, 2);
                Expect(driver, $"n{Key(n)}-black-{view}", pixels[("black", view)], Vector3.Zero, nn, direction, nl, 3);
                table.Append(CultureInfo.InvariantCulture, $"{n.X},{n.Y},{n.Z}\t{view}\t{nv:0.0000}\t{nl:0.0000}\t{l.X:0.0000},{l.Y:0.0000},{l.Z:0.0000}\t{s:0.000000}\t{Rgb(pixels[("grey", view)])}\t{Rgb(pixels[("black", view)])}\t{Rgb(pixels[("white", view)])}\n");
            }

        }

        await File.WriteAllTextAsync(Path.Combine(driver.RunFolder, "sky.tsv"), table.ToString());
    }

    /// <summary>
    /// Fails unless the pixel is within <paramref name="tolerance"/> levels in each channel of <see cref="ColourRule"/>'s
    /// model: albedo x (ambient 0.25 + 0.9 n.l / pi + the sky's light) + the light's and the sky's specular. A grey or
    /// white face is held to 2, ColourRule's tolerance; a black one, all specular, to 3, as the sky's specular is fitted.
    /// </summary>
    private static void Expect(ScenarioDriver driver, string what, (byte R, byte G, byte B, byte A) got, Vector3 albedo, Vector3 n, Vector3 direction, float nl, int tolerance)
    {
        var diffuse = 0.25 + (ColourRule.Light * Math.Max(0, nl) / Math.PI);
        var sky = ColourRule.SkyLight(n);
        var s = ColourRule.Reflected(n, direction);
        int[] want = [Srgb((albedo.X * (diffuse + sky[0])) + s[0]), Srgb((albedo.Y * (diffuse + sky[1])) + s[1]), Srgb((albedo.Z * (diffuse + sky[2])) + s[2])];
        int[] seen = [got.R, got.G, got.B];
        if (Enumerable.Range(0, 3).Any(c => Math.Abs(seen[c] - want[c]) > tolerance))
        {
            driver.Fail($"{what}: ({got.R}, {got.G}, {got.B}) against the model's ({want[0]}, {want[1]}, {want[2]}), tolerance {tolerance}");
        }
    }

    private static int Srgb(double linear)
    {
        var x = Math.Clamp(linear, 0, 1);
        return (int)Math.Round(255 * (x <= 0.0031308 ? 12.92 * x : (1.055 * Math.Pow(x, 1 / 2.4)) - 0.055));
    }

    private static string Key(Vector3 n) =>
        string.Concat(new[] { n.X, n.Y, n.Z }.Select(c => c > 0 ? "p" : c < 0 ? "m" : "0"));

    private static string Rgb((byte R, byte G, byte B, byte A) p) => $"{p.R},{p.G},{p.B}";

    /// <summary>The 26 directions of the axes, the edge diagonals and the corner diagonals (not normalised).</summary>
    private static IEnumerable<Vector3> Normals()
    {
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                for (var z = -1; z <= 1; z++)
                {
                    if (x != 0 || y != 0 || z != 0)
                    {
                        yield return new Vector3(x, y, z);
                    }
                }
            }
        }
    }

    /// <summary>A unit quad centred on the origin, wound counter-clockwise seen from <paramref name="n"/>.</summary>
    private static (double[][] Vertices, int[][] Faces) Quad(Vector3 n)
    {
        var normal = Vector3.Normalize(n);
        var helper = MathF.Abs(normal.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
        var u = Vector3.Normalize(Vector3.Cross(helper, normal));
        var w = Vector3.Cross(normal, u);
        Vector3[] corners = [(-u - w) / 2, (u - w) / 2, (u + w) / 2, (-u + w) / 2];
        return (corners.Select(c => new double[] { c.X, c.Y, c.Z }).ToArray(), [[0, 1, 2, 3]]);
    }

    private static async Task CallAsync(ScenarioDriver driver, string tool, object args)
    {
        var result = await driver.CallAsync(tool, args);
        if (result.IsError is true)
        {
            driver.Fail($"{tool}: {result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text}");
        }
    }
}

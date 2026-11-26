using System.Numerics;
using System.Text.Json;
using Carve.Debug;
using Carve.Scenario.Shed;
using static Carve.Scenario.Shed.Checks;
using static Carve.Scenario.Cuts.CutsChecks;

namespace Carve.Scenario.Cuts;

/// <summary>
/// T-223, displace: the rough boulder (samples/boulder.carve). define_mesh's unit cube subdivided 3 levels and seen,
/// then displace's worked example (seeded noise) and set_shading smooth, seen from the front and the back, then undone
/// and seen again, byte-equal to the frame before it. The silhouette of every frame is worked from get_mesh's geometry
/// by a ray test on triangles: a displaced quad is not flat, and the renderer draws it as one of its two triangle pairs,
/// so a probe counts only where both pairs, at the pixel and three pixels round it, agree; there it is the background
/// exactly where a ray meets nothing.
/// </summary>
internal sealed class DisplaceStage : ICutsStage
{
    private static readonly double[] Front = [0.6, 0.35, 1];
    private static readonly double[] Back = [-0.6, 0.35, -1];

    private static readonly double[][] Vertices =
        [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]];

    private static readonly int[][] Faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]];

    /// <summary>displace's worked example, as its description gives it.</summary>
    private static readonly object Rough = new { source = new { noise = new { seed = 7, scale = 0.35, octaves = 5 } }, amount = 0.6 };

    public async Task RunAsync(ScenarioDriver driver)
    {
        await CallJsonAsync(driver, "define_mesh", new { vertices = Vertices, faces = Faces });
        var smooth = await CallJsonAsync(driver, "subdivide", new { levels = 3 });
        await CallTextAsync(driver, "set_shading", new { shading = "smooth" });
        var before = await SeeAsync(driver, "boulder-smooth", Front, smooth, "the subdivided cube before displace");

        var rough = await CallJsonAsync(driver, "displace", Rough);
        Summary("displace", rough, 386, 384, true);
        Equal("displace: vertices moved", 386, rough.GetProperty("moved").GetArrayLength());
        await SeeAsync(driver, "boulder-front", Front, rough, "the boulder");
        await SeeAsync(driver, "boulder-back", Back, rough, "the boulder from behind");

        await CallJsonAsync(driver, "undo", new { });
        var undone = await SeeAsync(driver, "boulder-undone", Front, smooth, "the boulder undone");
        driver.AssertBytesEqual("the frame after undo against the frame before displace", before.Pixels, undone.Pixels);
    }

    private static async Task<ScenarioFrame> SeeAsync(ScenarioDriver driver, string name, double[] direction, JsonElement report, string what)
    {
        var bounds = report.GetProperty("bounds");
        Vector3 Corner(string key) => new(bounds.GetProperty(key)[0].GetSingle(), bounds.GetProperty(key)[1].GetSingle(), bounds.GetProperty(key)[2].GetSingle());
        var (min, max) = (Corner("min"), Corner("max"));
        var camera = new Camera(min, max, ToVector(direction), 512);
        var solid = await Solid.ReadAsync(driver);
        var frame = await driver.CaptureAsync(name, new { direction });
        Caption(frame, $"512x512, direction ({string.Join(", ", direction)}), 386 vertices, 384 faces");
        Size(frame, 512, 512);
        Corners(frame);
        var tested = 0;
        foreach (var (_, at) in Probes(min, max, ToVector(direction)))
        {
            var (x, y) = camera.Pixel(at);
            if (Hit(solid, camera, x, y) is not { } hit) continue;
            tested++;
            if (hit) NotBackground(frame, (x, y), $"{what}: pixel ({x}, {y}), on the boulder");
            else Background(frame, (x, y), $"{what}: pixel ({x}, {y}), which a ray meets nowhere");
        }
        if (tested < 200) Note($"{frame.Name}: only {tested} probes were surely the boulder or the background");
        return frame;
    }

    /// <summary>Whether the pixel shows the mesh, or null when either triangle pair or a pixel three round says otherwise.</summary>
    private static bool? Hit(Solid solid, Camera camera, int x, int y)
    {
        if (x < 4 || y < 4 || x >= 508 || y >= 508) return null;
        bool? seen = null;
        foreach (var (dx, dy) in new[] { (0, 0), (3, 0), (-3, 0), (0, 3), (0, -3), (2, 2), (-2, -2), (2, -2), (-2, 2) })
        {
            var (o, d) = camera.Ray(x + dx, y + dy);
            foreach (var diagonal in new[] { 0, 1 })
            {
                var hit = solid.Faces.Where(f => f.Loop.Length > 0).Any(f => Triangles(f.Loop, diagonal).Any(t =>
                    Crosses(o, d, solid.Vertices[t.A], solid.Vertices[t.B], solid.Vertices[t.C])));
                if (seen is { } s && s != hit) return null;
                seen = hit;
            }
        }
        return seen;
    }

    /// <summary>A face's fan of triangles from corner <paramref name="start"/>: for a quad, its two diagonals.</summary>
    private static IEnumerable<(int A, int B, int C)> Triangles(int[] loop, int start)
    {
        for (var k = 1; k < loop.Length - 1; k++)
            yield return (loop[start % loop.Length], loop[(start + k) % loop.Length], loop[(start + k + 1) % loop.Length]);
    }

    /// <summary>Möller-Trumbore, either side.</summary>
    private static bool Crosses(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
        var det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-12f) return false;
        var t = o - a;
        var u = Vector3.Dot(t, p) / det;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(t, e1);
        var v = Vector3.Dot(d, q) / det;
        return v >= 0 && u + v <= 1 && Vector3.Dot(e2, q) / det > 0;
    }

    /// <summary>A 24 x 24 grid on the plane through the bounds' centre facing the eye, reaching past the silhouette on every side.</summary>
    private static (string, Vector3)[] Probes(Vector3 min, Vector3 max, Vector3 toEye)
    {
        var centre = (min + max) / 2;
        var radius = (max - min).Length() / 2;
        var n = Vector3.Normalize(toEye);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, n));
        var up = Vector3.Cross(n, right);
        var steps = Enumerable.Range(0, 24).Select(i => (i / 23f * 2 - 1) * radius).ToArray();
        return [.. Grid("the boulder's plane", steps, (u, v) => centre + right * u + up * v)];
    }
}

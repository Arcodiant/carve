using System.Globalization;
using System.Numerics;
using System.Text;
using Carve.Debug;

namespace Carve.Scenario.Patina;

/// <summary>
/// What the cube view must show, worked from the geometry alone (T-157), restated from swatch's description: a cube of edge
/// 0.7 centred on the origin, turned about y by 35 degrees plus the angle and then leaned 25 degrees about x (the top toward the
/// camera), seen from the sphere's fixed camera; each face's UV 0..1, u to the right and v down as the face is seen from outside
/// (the top has -z at its top edge). For a pixel it finds the face its ray meets first and the (u, v) there, as
/// <see cref="SwatchSphere"/> does for the sphere, so <see cref="SwatchTiling"/> can predict a texel from them.
/// </summary>
internal sealed class SwatchCubeRays
{
    private const float Edge = 0.7f;
    private static readonly Vector3 Eye = new(0, 0, 2);
    private static readonly float HalfField = MathF.Tan(0.3f);

    /// <summary>Face, outward normal, the corner at (0, 0) of the unit cube, and the directions of u and v.</summary>
    private static readonly (string Name, Vector3 N, Vector3 O, Vector3 U, Vector3 V)[] Faces =
    [
        ("front", new(0, 0, 1), new(-0.5f, 0.5f, 0.5f), new(1, 0, 0), new(0, -1, 0)),
        ("back", new(0, 0, -1), new(0.5f, 0.5f, -0.5f), new(-1, 0, 0), new(0, -1, 0)),
        ("right", new(1, 0, 0), new(0.5f, 0.5f, 0.5f), new(0, 0, -1), new(0, -1, 0)),
        ("left", new(-1, 0, 0), new(-0.5f, 0.5f, -0.5f), new(0, 0, 1), new(0, -1, 0)),
        ("top", new(0, 1, 0), new(-0.5f, 0.5f, -0.5f), new(1, 0, 0), new(0, 0, 1)),
        ("bottom", new(0, -1, 0), new(-0.5f, -0.5f, 0.5f), new(1, 0, 0), new(0, 0, -1)),
    ];

    public static string NameOf(int face) => Faces[face].Name;

    private readonly Quaternion _inverse;

    public SwatchCubeRays(float angleDegrees)
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (35 + angleDegrees) * MathF.PI / 180);
        var lean = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 25 * MathF.PI / 180);
        // Turned first, then leaned: a point's world position is lean(turn(p)).
        _inverse = Quaternion.Inverse(Quaternion.Concatenate(turn, lean));
    }

    /// <summary>The hit of a pixel (x + 0.5 is its centre) and the face it is on; null for the background.</summary>
    public (SwatchSphere.Hit Hit, int Face)? Trace(float px, float py)
    {
        var direction = Vector3.Normalize(new Vector3((px / 512 * 2 - 1) * HalfField, (1 - py / 512 * 2) * HalfField, -1));
        var o = Vector3.Transform(Eye, _inverse) / Edge;
        var d = Vector3.Transform(direction, _inverse);
        (SwatchSphere.Hit, int)? best = null;
        for (var f = 0; f < Faces.Length; f++)
        {
            var (_, n, origin, u, v) = Faces[f];
            var denominator = Vector3.Dot(n, d);
            if (denominator >= -1e-6f) continue;
            var t = (0.5f - Vector3.Dot(n, o)) / denominator;
            if (t <= 0 || (best is { } b && t >= b.Item1.T)) continue;
            var p = o + t * d;
            var offset = p - origin;
            var fu = Vector3.Dot(offset, u);
            var fv = Vector3.Dot(offset, v);
            if (fu < -1e-5f || fu > 1 + 1e-5f || fv < -1e-5f || fv > 1 + 1e-5f) continue;
            best = (new SwatchSphere.Hit(t, Math.Clamp(fu, 0, 1), Math.Clamp(fv, 0, 1), -denominator), f);
        }
        return best;
    }
}

/// <summary>
/// The cube view judged as the sphere's tiling is (T-157): the stripe grid's texels exported at 64, the cube drawn at tiling 1
/// and 3 and predicted from <see cref="SwatchCubeRays"/>; every one of the three visible faces must have judged pixels, so a
/// fault on one face cannot hide in the others. The breaks, all made here on the prediction (a face mirrored, tiling ignored
/// on one face, a face's UVs turned) must each be caught by the same check.
/// </summary>
internal static class SwatchCubeCheck
{
    private const int N = SwatchSphere.Size;

    /// <summary>The cube's faces are lit less than the sphere's front, so a white texel is lit-white at luma 80 or more (measured below).</summary>
    private const float Facing = 0.3f;

    private sealed record Grid((SwatchSphere.Hit Hit, int Face)?[,] Hits, SwatchSphere.Hit?[,] Plain);

    private static Grid Trace(float angle, Func<int, SwatchSphere.Hit, SwatchSphere.Hit>? flaw = null)
    {
        var rays = new SwatchCubeRays(angle);
        var hits = new (SwatchSphere.Hit, int)?[N + 1, N + 1];
        var plain = new SwatchSphere.Hit?[N + 1, N + 1];
        for (var y = 0; y <= N; y++)
        {
            for (var x = 0; x <= N; x++)
            {
                if (rays.Trace(x + 0.5f, y + 0.5f) is not { } found) continue;
                hits[x, y] = found;
                plain[x, y] = flaw is null ? found.Hit : flaw(found.Face, found.Hit);
            }
        }
        return new Grid(hits, plain);
    }

    /// <summary>
    /// A texture with no mirror symmetry: diagonal stripes (2) times stripes across (4). The sphere's grid of stripes is symmetric
    /// in u and in v, so a mirrored face would read the same texels; a diagonal flips when mirrored or turned.
    /// </summary>
    public const string Asymmetric = """
        {"graph":{"version":1,"size":64,"seed":1,"nodes":{
          "across":{"type":"stripes","params":{"count":4,"axis":"u","width":0.5}},
          "slant":{"type":"stripes","params":{"count":2,"axis":"diagonal","width":0.5}},
          "both":{"type":"blend","inputs":{"a":"across","b":"slant"},"params":{"mode":"multiply","opacity":1}},
          "out":{"type":"output","inputs":{"in":"both"},"params":{"role":"base_colour"}}}}}
        """;

    /// <summary>
    /// A frame judged face by face: the pixels predicted white that read below <see cref="SwatchTiling.WhiteMin"/> or predicted black
    /// that read above <see cref="SwatchTiling.BlackMax"/>, and the pixels judged, per face. A face's wrong pixels are held to 0.5 %
    /// of its own judged pixels, so a fault on the top face, which a third of the frame's whole could hide, still fails.
    /// </summary>
    private static (int[] Wrong, int[] Judged) ByFace(ScenarioFrame frame, byte[] texels, Grid grid, int tiling)
    {
        var predicted = SwatchTiling.Predict(texels, 0, tiling, tiling, grid.Plain, Facing);
        var wrong = new int[6];
        var judged = new int[6];
        for (var at = 0; at < predicted.Length; at++)
        {
            if (predicted[at] == 0) continue;
            var face = grid.Hits[at % N, at / N]!.Value.Face;
            var luma = 0.2126 * frame.Pixels[at * 4] + 0.7152 * frame.Pixels[at * 4 + 1] + 0.0722 * frame.Pixels[at * 4 + 2];
            judged[face]++;
            if (predicted[at] > 0 ? luma < SwatchTiling.WhiteMin : luma > SwatchTiling.BlackMax) wrong[face]++;
        }
        return (wrong, judged);
    }

    private static bool FaceWrong((int[] Wrong, int[] Judged) r) => Enumerable.Range(0, 6).Any(f => r.Wrong[f] > r.Judged[f] / 200);

    public static async Task RunAsync(ScenarioDriver driver, PatinaChecks checks, StringBuilder measured, StringBuilder log)
    {
        await checks.CallAsync("define_graph", Asymmetric);
        var folder = Path.Combine(driver.RunFolder, "swatch-cube-texels");
        await checks.CallAsync("export", System.Text.Json.JsonSerializer.Serialize(new { folder, size = 64 }));
        var texels = SwatchTiling.Decode(Path.Combine(folder, "base_colour.png"));
        var frames = new Dictionary<int, ScenarioFrame>();
        var grids = new Dictionary<int, Grid>();
        foreach (var tiling in new[] { 1, 3 })
        {
            var frame = await driver.CaptureToolAsync("swatch", $"swatch-cube-grid-tiling-{tiling}", new { view = "cube", tiling });
            frames[tiling] = frame;
            var grid = grids[tiling] = Trace(0);
            SwatchTiling.Check(checks, $"cube at {tiling}", frame, texels, 0, tiling, tiling, measured, grid.Plain, Facing);

            var faces = ByFace(frame, texels, grid, tiling);
            measured.Append(CultureInfo.InvariantCulture, $"cube at {tiling} wrong pixels by face	{string.Join("\t", Enumerable.Range(0, 6).Select(f => SwatchCubeRays.NameOf(f) + " " + faces.Wrong[f] + "/" + faces.Judged[f]))}\n");
            checks.Check(!FaceWrong(faces), $"swatch: cube at {tiling}: a face has more than 0.5 % of its judged pixels wrong ({string.Join(", ", Enumerable.Range(0, 6).Select(f => SwatchCubeRays.NameOf(f) + " " + faces.Wrong[f] + "/" + faces.Judged[f]))})");

            // Every visible face (front, left, top) has judged pixels.
            var judged = SwatchTiling.Predict(texels, 0, tiling, tiling, grid.Plain, Facing);
            var perFace = new int[6];
            for (var at = 0; at < judged.Length; at++)
                if (judged[at] != 0) perFace[grid.Hits[at % N, at / N]!.Value.Face]++;
            measured.Append(CultureInfo.InvariantCulture, $"cube at {tiling} judged pixels by face\t{string.Join("\t", Enumerable.Range(0, 6).Select(f => SwatchCubeRays.NameOf(f) + " " + perFace[f]))}\n");
            foreach (var face in new[] { 0, 3, 4 })
                checks.Check(perFace[face] > 1500, $"swatch: cube at {tiling}: only {perFace[face]} pixels of the {SwatchCubeRays.NameOf(face)} face were judged");
            foreach (var face in new[] { 1, 2, 5 })
                checks.Check(perFace[face] == 0, $"swatch: cube at {tiling}: {perFace[face]} pixels of the hidden {SwatchCubeRays.NameOf(face)} face were judged");
        }

        // The angle turns the cube: 20 degrees on is a different frame, and still predicted.
        var turned = await driver.CaptureToolAsync("swatch", "swatch-cube-grid-angle-20-tiling-3", new { view = "cube", tiling = 3, angle = 20 });
        checks.Check(!turned.Pixels.AsSpan().SequenceEqual(frames[3].Pixels), "swatch: the cube at angle 20 is the cube at angle 0");
        SwatchTiling.Check(checks, "cube at angle 20, tiling 3", turned, texels, 20, 3, 3, measured, Trace(20).Plain, Facing);
        var turnedFaces = ByFace(turned, texels, Trace(20), 3);
        measured.Append(CultureInfo.InvariantCulture, $"cube at angle 20 wrong pixels by face	{string.Join("\t", Enumerable.Range(0, 6).Select(f => SwatchCubeRays.NameOf(f) + " " + turnedFaces.Wrong[f] + "/" + turnedFaces.Judged[f]))}\n");
        checks.Check(!FaceWrong(turnedFaces), "swatch: cube at angle 20: a face has more than 0.5 % of its judged pixels wrong");

        // The same frame with no tiling given is tiling 1; and the worked example as written.
        var plainFrame = await driver.CaptureToolAsync("swatch", "swatch-cube-grid-default", new { view = "cube" });
        checks.Check(plainFrame.Pixels.AsSpan().SequenceEqual(frames[1].Pixels), "swatch: the cube at tiling 1 is not the cube with no tiling given");
        var worked = await checks.CallAsync("swatch", """{"view":"cube","tiling":3}""");
        checks.Check(worked.Contains("on a cube, each face 0 to 1 in u and v, angle 0, tiling 3, 512 x 512 px", StringComparison.Ordinal), "swatch: the cube's worked example said " + worked);
        await checks.CallAsync("swatch", """{"view":"cube","tiling":9}""", refused: true);
        // The sphere is still the sphere after a cube was drawn (the entities share one material).
        var sphereAfter = await driver.CaptureToolAsync("swatch", "swatch-sphere-grid-after-cube", new { view = "sphere", tiling = 3 });
        SwatchTiling.Check(checks, "sphere after the cube", sphereAfter, texels, 0, 3, 3);

        // The breaks, on one face each (the product has no switch for any of them), against the cube drawn at tiling 3.
        void Caught(string name, Func<int, SwatchSphere.Hit, SwatchSphere.Hit> flaw, int drawn = 3)
        {
            var probe = new PatinaChecks(driver);
            var wrong = ByFace(frames[drawn], texels, Trace(0, flaw), 3);
            probe.Check(!FaceWrong(wrong), "white-predicted: " + string.Join(", ", Enumerable.Range(0, 6).Select(f => SwatchCubeRays.NameOf(f) + " " + wrong.Wrong[f] + "/" + wrong.Judged[f])));
            log.AppendLine("== cube " + name);
            foreach (var failure in probe.Failures) log.AppendLine("  " + failure);
            checks.Check(probe.Failures.Any(f => f.Contains("white-predicted", StringComparison.Ordinal)), $"swatch: the cube break {name} was not caught");
        }
        Caught("front-face-mirrored", (face, h) => face == 0 ? h with { U = 1 - h.U } : h);
        Caught("left-face-uvs-turned", (face, h) => face == 3 ? h with { U = 1 - h.V, V = h.U } : h);
        Caught("top-face-tiling-ignored", (face, h) => face == 4 ? h with { U = h.U / 3, V = h.V / 3 } : h);
        Caught("top-face-mirrored-in-v", (face, h) => face == 4 ? h with { V = 1 - h.V } : h);
        Caught("tiling-1-drawn-judged-as-3", (face, h) => h, drawn: 1);
    }
}

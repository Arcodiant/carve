using System.Globalization;
using System.Numerics;
using System.Text;
using Carve.Scenario.Crate;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Weathered;

/// <summary>
/// The Scope's research, through the tools: the layout stage 1 made and a plain <c>unwrap</c> measured with <c>check</c>'s answer, and the
/// <c>wear</c> and <c>grime</c> texels at 512 of four sets with the lit colour each gives. Written to <c>research.txt</c>; it checks nothing.
/// </summary>
internal sealed class ResearchStage : IWeatheredStage
{
    private const int UvFrame = 1024, Shaded = 512;
    private static readonly Vector3 Front = new(1.6f, 1.2f, 2.2f), Back = -Front;
    private readonly StringBuilder log = new();

    private void Line(FormattableString s) => log.AppendLine(s.ToString(CultureInfo.InvariantCulture));

    public async Task RunAsync(WeatheredRun run)
    {
        var driver = run.Driver;
        var mesh = await Scene.ReadOneAsync(driver, "mesh");
        Line($"faces {mesh.Faces.Length}");
        foreach (var f in Enumerable.Range(0, mesh.Faces.Length))
        {
            var ys = mesh.Faces[f].Select(v => mesh.Vertices[v].Y).ToArray();
            var n = Vector3.Normalize(mesh.Normal(f));
            var c = mesh.Centre(f);
            Line($"face {f}: {mesh.Faces[f].Length} verts, y {ys.Min():F4}..{ys.Max():F4}, radius {MathF.Sqrt(c.X * c.X + c.Z * c.Z):F4}, normal ({n.X:F2},{n.Y:F2},{n.Z:F2})");
        }

        Layout("preferred (staves projected into the band, the rest unwrapped)", mesh, await CallTextAsync(driver, "check", new { }));
        await driver.CaptureAsync("research-uvs-preferred", new { mode = "uvs", size = Shaded });

        var scene = await Scene.ReadAsync(driver, "mesh");
        var sets = Sets(mesh);
        foreach (var (graph, output) in new[] { ("weathered-iron", "wear"), ("weathered-staves", "grime") })
        {
            var frame = await driver.CaptureAsync($"research-uvs-{output}", new { mode = "uvs", image = new { graph = Barrel.Graph(run, graph), output, size = WeatheredRun.BakeSize }, size = UvFrame });
            Line($"{output} at {WeatheredRun.BakeSize}, read from a {UvFrame} uvs frame ({frame.Caption}):");
            foreach (var (name, texels) in sets)
            {
                var values = texels.Select(t => frame.Pixel(t.Px, t.Py).R / 255.0).ToArray();
                Line($"  {name,-22} n {values.Length,6}  min {values.DefaultIfEmpty().Min():F3}  mean {values.DefaultIfEmpty().Average():F3}  max {values.DefaultIfEmpty().Max():F3}  p5 {Pct(values, 0.05):F3}  p95 {Pct(values, 0.95):F3}");
            }

            if (output == "grime")
            {
                var all = sets.First(s => s.Name == "stave all").Texels.Select(t => frame.Pixel(t.Px, t.Py).R / 255.0).ToArray();
                Line($"  stave texels with grime > 0.02: {all.Count(v => v > 0.02) / (double)Math.Max(1, all.Length):P1}; > 0.1: {all.Count(v => v > 0.1) / (double)Math.Max(1, all.Length):P1}");
            }
        }

        // The lit colour of each set, from front, back and close on a rim facing the front.
        var (min, max) = (mesh.BoundsMin, mesh.BoundsMax);
        var rimFront = sets.First(s => s.Name == "hoop rim").Texels.MaxBy(t => Vector3.Dot(Vector3.Normalize(new Vector3(t.P.X, 0, t.P.Z)), Vector3.Normalize(new Vector3(Front.X, 0, Front.Z))));
        var closeDir = Vector3.Normalize(new Vector3(rimFront.P.X, 0, rimFront.P.Z) + new Vector3(0, 0.3f, 0));
        foreach (var (view, args, dir, zoom, target) in new (string, object, Vector3, float, Vector3?)[]
        {
            ("front", new { view = "front", size = Shaded, mode = "shaded" }, Front, 1, null),
            ("back", new { view = "back", size = Shaded, mode = "shaded" }, Back, 1, null),
            ("close", new { direction = new[] { closeDir.X, closeDir.Y, closeDir.Z }, zoom = 4.0, target = new[] { rimFront.P.X, rimFront.P.Y, rimFront.P.Z }, size = Shaded, mode = "shaded" }, closeDir, 4, rimFront.P),
        })
        {
            var frame = await driver.CaptureAsync($"research-{view}", args);
            var project = CameraPixels.Perspective(min, max, dir, Shaded, zoom, target);
            var eye = CameraPixels.Eye(min, max, dir, target);
            Line($"lit colour from {view}:");
            foreach (var (name, texels) in sets)
            {
                var seen = texels.Where((t, i) => i % 3 == 0 && Vector3.Dot(mesh.Normal(t.Face), eye - t.P) > 0 && Geo.InSight(scene, mesh, t.Face, eye, t.P))
                    .Select(t => CameraPixels.Holding(project(t.P))).Where(p => p.X >= 0 && p.Y >= 0 && p.X < frame.Width && p.Y < frame.Height)
                    .Select(p => frame.Pixel(p.X, p.Y)).ToArray();
                if (seen.Length == 0) { Line($"  {name,-22} not seen"); continue; }
                Line($"  {name,-22} n {seen.Length,6}  mean rgb ({seen.Average(p => p.R):F0}, {seen.Average(p => p.G):F0}, {seen.Average(p => p.B):F0})  luma mean {seen.Average(Luma):F1} min {seen.Min(Luma):F1} max {seen.Max(Luma):F1}");
            }
        }

        // The plain unwrap, for comparison: every face, one call.
        await CallJsonAsync(driver, "unwrap", new { });
        Layout("plain unwrap", await Scene.ReadOneAsync(driver, "mesh"), await CallTextAsync(driver, "check", new { }));
        await driver.CaptureAsync("research-uvs-plain", new { mode = "uvs", size = Shaded });
        await CallJsonAsync(driver, "undo", new { });
        File.WriteAllText(run.InRun("research.txt"), log.ToString());
    }

    private static double Luma((byte R, byte G, byte B, byte A) p) => 0.2126 * p.R + 0.7152 * p.G + 0.0722 * p.B;

    private static double Pct(double[] v, double q) => v.Length == 0 ? 0 : v.Order().ElementAt((int)Math.Min(v.Length - 1, q * v.Length));

    private void Layout(string name, SceneMesh mesh, string check)
    {
        Line($"layout {name}: islands {Scene.Islands(mesh)}");
        foreach (var f in Barrel.Staves)
        {
            if (!mesh.Uvs.TryGetValue(f, out var uv)) { Line($"  stave {f}: no UVs"); continue; }
            Line($"  stave {f}: u {uv.Min(t => t.X):F4}..{uv.Max(t => t.X):F4} (width {uv.Max(t => t.X) - uv.Min(t => t.X):F4}), v {uv.Min(t => t.Y):F4}..{uv.Max(t => t.Y):F4}");
        }

        Line($"  check: {check.ReplaceLineEndings(" | ")}");
    }

    private readonly record struct Texel(int Face, Vector3 P, int Px, int Py);

    /// <summary>
    /// The 512 texels whose centres lie inside a face's UVs, at least 2 frame pixels from that face's UV edges (the look draws its edges there),
    /// sorted into the Scope's sets by where on the face the texel's point lies.
    /// </summary>
    private static (string Name, Texel[] Texels)[] Sets(SceneMesh mesh)
    {
        var staveY = Barrel.Staves.SelectMany(f => mesh.Faces[f]).Select(v => mesh.Vertices[v].Y).ToArray();
        var (y0, y1) = (staveY.Min(), staveY.Max());
        var named = new Dictionary<string, List<Texel>> { ["hoop rim"] = [], ["hoop top middle"] = [], ["stave beside hoop"] = [], ["stave middle"] = [], ["stave all"] = [] };
        const int n = WeatheredRun.BakeSize;
        var scale = UvFrame * 7 / 8.0 / n;
        foreach (var f in Barrel.Hoops.Concat(Barrel.Staves))
        {
            if (!mesh.Uvs.TryGetValue(f, out var uv)) continue;
            var loop = mesh.Faces[f];
            var ys = loop.Select(v => mesh.Vertices[v].Y).ToArray();
            var (fy0, fy1) = (ys.Min(), ys.Max());
            var (lo, hi) = (uv.Aggregate(Vector2.Min), uv.Aggregate(Vector2.Max));
            for (var ty = Math.Max(0, (int)(lo.Y * n)); ty <= Math.Min(n - 1, (int)(hi.Y * n)); ty++)
            {
                for (var tx = Math.Max(0, (int)(lo.X * n)); tx <= Math.Min(n - 1, (int)(hi.X * n)); tx++)
                {
                    var c = new Vector2((tx + 0.5f) / n, (ty + 0.5f) / n);
                    if (Enumerable.Range(0, uv.Length).Any(k => Geo.DistanceToSegment(c, uv[k], uv[(k + 1) % uv.Length]) * n * scale < 2)) continue;
                    Vector3? p = null;
                    for (var k = 1; k < loop.Length - 1 && p is null; k++)
                    {
                        var b = Bary(c, uv[0], uv[k], uv[k + 1]);
                        if (b.X >= 0 && b.Y >= 0 && b.Z >= 0) p = mesh.Vertices[loop[0]] * b.X + mesh.Vertices[loop[k]] * b.Y + mesh.Vertices[loop[k + 1]] * b.Z;
                    }

                    if (p is not { } q) continue;
                    var (px, py) = ((int)(UvFrame / 16.0 + c.X * scale * n), (int)(UvFrame / 16.0 + c.Y * scale * n)); // the uvs look's square, as the bakes' UvPixel
                    var texel = new Texel(f, q, px, py);
                    if (Barrel.Hoops.Contains(f))
                    {
                        var t = (q.Y - fy0) / (fy1 - fy0);
                        if (t < 0.15f || t > 0.85f) named["hoop rim"].Add(texel);
                        else if (t > 0.4f && t < 0.6f) named["hoop top middle"].Add(texel);
                        continue;
                    }

                    named["stave all"].Add(texel);
                    if (Math.Min(q.Y - y0, y1 - q.Y) < 0.02f) named["stave beside hoop"].Add(texel);
                    else if (Math.Abs(q.Y - (y0 + y1) / 2) < 0.03f) named["stave middle"].Add(texel);
                }
            }
        }

        return [.. named.Select(e => (e.Key, e.Value.ToArray()))];
    }

    private static Vector3 Bary(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        if (MathF.Abs(det) < 1e-12f) return new Vector3(-1);
        var l0 = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
        var l1 = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
        return new Vector3(l0, l1, 1 - l0 - l1);
    }
}

using System.Numerics;
using Carve.Debug;
using Carve.Scenario.Uvs;
using static Carve.Scenario.Bakes.BakesChecks;
using static Carve.Scenario.Crate.CrateBuild;

namespace Carve.Scenario.Bakes;

/// <summary>
/// T-165: <c>render mode=uvs image={graph, output, size}</c> on the wrench. A graph of one mesh node into a named output, written
/// into the run folder, seen at 1024 in the <c>uvs</c> look must equal, byte for byte, the look with the matching bake of M-24 under
/// it (curvature width 0.003, occlusion 32 rays reach 0.035, the <c>steel</c> mask, each padded 8): the node and the bake are one
/// computation. <c>mesh_position</c> has no bake, so its frame is held to T-121's block rule against texels worked here from
/// <c>get_mesh</c>'s positions and UVs, and its red must rise along the wrench's x. Breaks (<c>CARVE_BAKES_BREAK</c>):
/// <c>graph-width</c> doubles the curvature width in the graph, <c>graph-mask</c> names <c>rubber</c> for the mask, <c>graph-pos</c>
/// swaps x and z in the position prediction.
/// </summary>
internal sealed class GraphImageStage : IBakesStage
{
    private const int Size = 1024;

    public async Task RunAsync(ScenarioDriver driver)
    {
        var scene = await Scene.ReadAsync(driver, "wrench");
        var mesh = scene["wrench"];
        var textures = Path.Combine(driver.RunFolder, "graph-image");
        Directory.CreateDirectory(textures);

        var width = Break == "graph-width" ? 0.006 : 0.003;
        var maskGroup = Break == "graph-mask" ? "rubber" : "steel";
        var bake = await CallJsonAsync(driver, "bake", new
        {
            mesh = "wrench", folder = textures, size = Size, padding = 8, maps = new[] { "curvature", "occlusion", "masks" },
            curvature = new { width = 0.003 }, occlusion = new { rays = 32, reach = 0.035 },
        });
        string PathOf(string name) => bake.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()!).Single(p => Path.GetFileName(p) == name);

        await SameAsBakeAsync(driver, "curvature", $"\"width\": {width.ToString(System.Globalization.CultureInfo.InvariantCulture)}", "mesh_curvature", PathOf("wrench.curvature.png"));
        await SameAsBakeAsync(driver, "occlusion", "\"rays\": 32, \"reach\": 0.035", "mesh_occlusion", PathOf("wrench.occlusion.png"));
        await SameAsBakeAsync(driver, "steel", $"\"group\": \"{maskGroup}\"", "mesh_mask", PathOf("wrench.mask.steel.png"));
        await PositionAsync(driver, mesh);
    }

    private static string GraphFile(ScenarioDriver driver, string name, string node, string parameters)
    {
        var path = Path.Combine(driver.RunFolder, "graph-image", name + ".texgraph");
        File.WriteAllText(path, $$"""{ "version": 2, "size": {{Size}}, "seed": 1, "nodes": { "n": { "type": "{{node}}", "params": { {{parameters}} } }, "o": { "type": "output", "inputs": { "in": "n" }, "params": { "name": "{{name}}_map" } } } }""");
        return path;
    }

    /// <summary>The look with the graph's output, against the look with the bake's PNG: every pixel the same.</summary>
    private static async Task SameAsBakeAsync(ScenarioDriver driver, string name, string parameters, string node, string pngPath)
    {
        var reference = await driver.CaptureAsync($"wrench-uvs-{name}-png", new { mode = "uvs", image = pngPath, size = Size });
        var graph = GraphFile(driver, name, node, parameters);
        var frame = await driver.CaptureAsync($"wrench-uvs-graph-{name}", new { mode = "uvs", image = new { graph, output = name + "_map", size = Size }, size = Size });
        if (frame.Width != reference.Width || frame.Height != reference.Height) { Note($"graph image {name}: frame is {frame.Width}x{frame.Height}, the bake's is {reference.Width}x{reference.Height}"); return; }
        if (!frame.Pixels.AsSpan().SequenceEqual(reference.Pixels)) Note($"graph image {name}: {Differences(frame, reference)}; the node and the bake are one computation, so the frame must equal the bake's byte for byte");
    }

    private static string Differences(ScenarioFrame a, ScenarioFrame b)
    {
        int count = 0, x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
        for (var i = 0; i < a.Pixels.Length; i += 4)
        {
            if (a.Pixels.AsSpan(i, 4).SequenceEqual(b.Pixels.AsSpan(i, 4))) continue;
            var (x, y) = (i / 4 % a.Width, i / 4 / a.Width);
            (count, x0, y0, x1, y1) = (count + 1, Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
        }

        return $"{count} pixels differ, within x {x0}..{x1}, y {y0}..{y1}";
    }

    private static async Task PositionAsync(ScenarioDriver driver, SceneMesh mesh)
    {
        var graph = GraphFile(driver, "position", "mesh_position", "");
        var frame = await driver.CaptureAsync("wrench-uvs-graph-position", new { mode = "uvs", image = new { graph, output = "position_map", size = Size }, size = Size });

        var (min, max) = (mesh.Vertices.Aggregate(Vector3.Min), mesh.Vertices.Aggregate(Vector3.Max));
        var rgba = new byte[Size * Size * 4];
        float Axis(float v, float lo, float hi) => hi > lo ? (v - lo) / (hi - lo) : 0.5f;
        foreach (var (f, uv) in mesh.Uvs)
        {
            var loop = mesh.Faces[f];
            for (var k = 1; k < loop.Length - 1; k++)
            {
                Vector2 a = uv[0] * Size, b = uv[k] * Size, c = uv[k + 1] * Size;
                var (lo, hi) = (Vector2.Min(a, Vector2.Min(b, c)), Vector2.Max(a, Vector2.Max(b, c)));
                var det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
                if (Math.Abs(det) < 1e-9) continue;
                for (var y = Math.Max(0, (int)lo.Y); y <= Math.Min(Size - 1, (int)hi.Y); y++)
                {
                    for (var x = Math.Max(0, (int)lo.X); x <= Math.Min(Size - 1, (int)hi.X); x++)
                    {
                        var (px, py) = (x + 0.5f - c.X, y + 0.5f - c.Y);
                        var l0 = ((b.Y - c.Y) * px + (c.X - b.X) * py) / det;
                        var l1 = ((c.Y - a.Y) * px + (a.X - c.X) * py) / det;
                        var l2 = 1 - l0 - l1;
                        if (l0 < -1e-4 || l1 < -1e-4 || l2 < -1e-4) continue;
                        var p = mesh.Vertices[loop[0]] * l0 + mesh.Vertices[loop[k]] * l1 + mesh.Vertices[loop[k + 1]] * l2;
                        var (r, g, bl) = (Axis(p.X, min.X, max.X), Axis(p.Y, min.Y, max.Y), Axis(p.Z, min.Z, max.Z));
                        if (Break == "graph-pos") (r, bl) = (Axis(p.Z, min.Z, max.Z), Axis(p.X, min.X, max.X));
                        var i = (y * Size + x) * 4;
                        (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(bl * 255), 255);
                    }
                }
            }
        }

        CheckUvImage("wrench-uvs-graph-position", frame, mesh, new Texels(Size, Size, rgba), blend: 1, insideOnly: true);

        // Its red rises along x: over the faces' UV centres, the frame's red against the centre's x.
        var points = mesh.Uvs.Select(e =>
        {
            var (px, py) = UvPixel(e.Value.Aggregate(Vector2.Zero, (s, p) => s + p) / e.Value.Length, Size);
            return (X: (double)mesh.Centre(e.Key).X, R: (double)frame.Pixel(px, py).R);
        }).ToArray();
        var (mx, mr) = (points.Average(p => p.X), points.Average(p => p.R));
        var corr = points.Sum(p => (p.X - mx) * (p.R - mr)) / Math.Sqrt(points.Sum(p => (p.X - mx) * (p.X - mx)) * points.Sum(p => (p.R - mr) * (p.R - mr)));
        if (!(corr >= 0.97)) Note($"graph image position: red against x over {points.Length} faces correlates {corr:F3}, under 0.97");
    }
}

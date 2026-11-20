using System.Numerics;
using Carve.Core.Baking;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-125's seam check, by the person's ruling (2026-11-20) a Core test on <c>samples/wrench.carve</c> beside
/// <see cref="WrenchSampleTests"/>, with T-120's layout frames: the wrench's normal map baked at 1024 with width 0.0016, and on
/// each hard edge that is also a UV seam, the texels beside it on both of its faces (within a quarter width of it, and within
/// width of no other break edge) decode, through their own frame, to within <see cref="SeamAngle"/> of the edge's mean normal.
/// Its break, the map baked at width 0.00001, must fail it.
/// </summary>
[Trait("Tier", "unit")]
public class WrenchBevelSeamTests(ITestOutputHelper output)
{
    private const int Size = 1024;

    private const double Width = 0.0016, ThinWidth = 0.00001;

    /// <summary>
    /// The largest angle, in degrees, between a decoded texel beside a seam edge and the edge's mean normal. PENDING THE PERSON'S
    /// RULING (T-125's hand-off): probed (Opus 5.5, 2026-11-20) on 212 seam edges read on both sides, largest 8.33 degrees; 9
    /// is that rounded up. The break's least, over every side, is 32.4 degrees.
    /// </summary>
    internal const double SeamAngle = 9;

    private static PolygonMesh Wrench()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "wrench.carve"))) dir = dir.Parent!;
        var read = CarveFile.Read(File.ReadAllText(Path.Combine(dir.FullName, "samples", "wrench.carve")));
        Assert.True(read.Succeeded);
        return read.Mesh!;
    }

    /// <summary>Per seam edge and face beside it: the largest angle, in degrees, of its texels from the edge's mean normal, and how many texels.</summary>
    private static Dictionary<(int Lo, int Hi, int Face), (double Angle, int Count)> Readings(BakeLayout layout, double width)
    {
        var mesh = layout.Mesh;
        var seams = mesh.Seams.Intersect(mesh.HardEdges).ToHashSet();
        Assert.NotEmpty(seams);
        var image = Bake.BevelNormal(layout, width);
        var readings = new Dictionary<(int, int, int), (double, int)>();
        foreach (var texel in layout.Texels())
        {
            // Reached at the bake's width, so the break's thin map is read at the same texels as the good one.
            var near = layout.NearEdges(texel, Width).Where(e => e.Distance < Width).ToList();
            if (near.Count != 1 || !seams.Contains((near[0].Lo, near[0].Hi)) || !(near[0].Distance < Width / 4)) continue;
            var edge = near[0];
            // The PNG's bytes, as bake quantises them.
            var bytes = new Vector3(Byte(image[texel.X, texel.Y, 0]), Byte(image[texel.X, texel.Y, 1]), Byte(image[texel.X, texel.Y, 2]));
            var world = texel.Frame.FromTangentSpace(bytes);
            var mean = Vector3.Normalize(Vector3.Normalize(texel.FaceNormal) + Vector3.Normalize(edge.OtherNormal));
            var angle = Math.Acos(Math.Clamp(Vector3.Dot(world, mean), -1, 1)) * 180 / Math.PI;
            var key = (edge.Lo, edge.Hi, texel.Face);
            var (max, count) = readings.GetValueOrDefault(key);
            readings[key] = (Math.Max(max, angle), count + 1);
        }
        return readings;
    }

    private static float Byte(float value) => (float)Math.Round(Math.Clamp(value, 0, 1) * 255);

    [Fact]
    public void Texels_either_side_of_a_hard_edge_across_a_uv_seam_decode_near_the_edges_mean_normal_and_a_thin_bevel_fails()
    {
        var layout = BakeLayout.Create(Wrench(), Size).Layout!;
        var good = Readings(layout, Width);
        var edges = good.Keys.GroupBy(k => (k.Lo, k.Hi)).Where(g => g.Count() == 2).Select(g => g.Key).ToList();
        foreach (var ((lo, hi, face), (angle, count)) in good.OrderBy(k => k.Key))
            output.WriteLine($"({lo}, {hi}) face {face}: {count} texels, largest {angle:F2} degrees");
        var largest = good.Values.Max(r => r.Angle);
        output.WriteLine($"seam edges read on both sides: {edges.Count}; largest angle {largest:F2} degrees");

        Assert.True(edges.Count >= 4, $"only {edges.Count} seam edges had texels read on both sides");
        Assert.True(largest <= SeamAngle, $"a texel beside a seam edge decodes {largest:F2} degrees from the edge's mean normal, more than {SeamAngle}");

        // The break: at width 0.00001 the same texels hold N, half the bend from the mean.
        var thin = Readings(layout, ThinWidth);
        var thinLeast = edges.SelectMany(e => thin.Where(k => (k.Key.Lo, k.Key.Hi) == e)).Min(k => k.Value.Angle);
        var thinLargest = thin.Values.Max(r => r.Angle);
        output.WriteLine($"thin: largest {thinLargest:F2} degrees; least of a side's largest {thinLeast:F2}");
        Assert.True(thinLeast > SeamAngle, $"a side of a seam edge in the thin bevel decodes within {SeamAngle} of the mean (its largest {thinLeast:F2}), so the check would not catch it there");
    }
}

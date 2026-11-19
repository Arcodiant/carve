using Carve.Core.Baking;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-122: <c>samples/wrench.carve</c> is fps-arms' file, unchanged (main at 1535e3e). It opens clean, its UVs check
/// clean, T-120's layout finds no overlap at 1024, and its masks cover in proportion to the UV area of each group.
/// </summary>
[Trait("Tier", "unit")]
public class WrenchSampleTests
{
    private static (PolygonMesh Mesh, CarveFileResult Read) Wrench()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "wrench.carve"))) dir = dir.Parent!;
        var read = CarveFile.Read(File.ReadAllText(Path.Combine(dir.FullName, "samples", "wrench.carve")));
        Assert.True(read.Succeeded);
        return (read.Mesh!, read);
    }

    [Fact]
    public void The_wrench_opens_with_no_error_or_warning_and_its_uvs_check_clean()
    {
        var (mesh, read) = Wrench();
        Assert.Empty(read.Errors);
        Assert.Empty(read.Warnings);
        Assert.Equal(120, mesh.LiveFaceCount);
        Assert.Equal(120, mesh.Uvs!.MappedFaces.Count);
        Assert.Empty(MeshUvs.Check(mesh, mesh.Uvs));
        Assert.Equal(
            [("rubber", 14), ("steel", 58), ("dark", 41), ("red", 6), ("black", 1), ("all", 120)],
            mesh.Style!.Groups.Select(g => (g.Name, g.Faces.Count)));
        Assert.Empty(mesh.Style.Colours);
    }

    [Fact]
    public void The_wrench_lays_out_at_1024_with_no_overlap_and_every_face_mapped()
    {
        var (mesh, _) = Wrench();
        var result = BakeLayout.Create(mesh, 1024);
        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(result.Unmapped);
        Assert.Equal(120, result.Layout!.Faces.Count);
    }

    [Fact]
    public void The_wrench_masks_at_256_sum_to_the_layouts_coverage_and_each_groups_is_in_proportion_to_its_uv_area()
    {
        var (mesh, _) = Wrench();
        var layout = BakeLayout.Create(mesh, 256).Layout!;
        var masks = Bake.Masks(layout, mesh).ToDictionary(m => m.Name, m => m.Image.Values.ToArray().Count(v => v > 0.5f));

        // The groups but "all" partition the faces, so their texels are the layout's; "all" is the layout's.
        Assert.Equal(layout.CoveredCount, masks["all"]);
        Assert.Equal(layout.CoveredCount, new[] { "rubber", "steel", "dark", "red", "black" }.Sum(g => masks[g]));

        foreach (var (name, faces) in mesh.Style!.Groups)
        {
            double area = 0, perimeter = 0;
            foreach (var f in faces)
            {
                var uv = mesh.Uvs!.Of(f)!;
                double twice = 0;
                for (var i = 0; i < uv.Count; i++)
                {
                    var a = uv[i];
                    var b = uv[(i + 1) % uv.Count];
                    twice += (double)a.X * b.Y - (double)b.X * a.Y;
                    perimeter += (b - a).Length() * 256;
                }
                area += Math.Abs(twice) / 2 * 256 * 256;
            }
            // A texel is taken by its centre (or the clamp), so the count is the area give or take up to a
            // texel along each edge (the clamp reaches past it for a face thinner than a texel).
            Assert.True(Math.Abs(masks[name] - area) <= perimeter + 1, $"{name}: {masks[name]} texels for {area:F1} of uv area (perimeter {perimeter:F1})");
        }
    }
}

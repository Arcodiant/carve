using Carve.Core.Baking;
using Xunit;
using static Carve.Graphs.Tests.MeshNodesTests;

namespace Carve.Graphs.Tests;

/// <summary>
/// The broken-copies table (T-161): a source wrapping <see cref="MeshBakeSource"/> that gets one thing wrong, and the check
/// that catches it. Every row is caught by the texel-for-texel comparison with the <see cref="Bake"/> call; the mask row is
/// caught by <see cref="MaskCheck"/> too and the curvature-for-occlusion row on the plain barrel by <see cref="AllOpen"/>.
/// </summary>
[Trait("Tier", "unit")]
public class BrokenCopiesTests
{
    private const int Size = 256;

    private sealed class Broken(MeshBakeSource real, string what, BakeLayout layout) : IMeshMaps
    {
        public BakeImage Curvature(int size, double width) =>
            what == "padding left out" ? Unpadded(real.Curvature(size, width), MeshCurvatureNode.Neutral) : real.Curvature(size, width);

        public BakeImage Occlusion(int size, int rays, float reach) =>
            what == "curvature read as occlusion" ? real.Curvature(size, 0.003) : real.Occlusion(size, rays, reach);

        public BakeImage Mask(int size, MaskSource source, string name)
        {
            var image = real.Mask(size, source, name);
            if (what != "mask inverted") return image;
            image = image.Clone();
            foreach (ref var v in image.Values) v = 1 - v;
            return image;
        }

        public BakeImage Position(int size)
        {
            var image = real.Position(size);
            if (what != "position's y flipped") return image;
            image = image.Clone();
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++) image[x, y, 1] = 1 - image[x, y, 1];
            return image;
        }

        private BakeImage Unpadded(BakeImage image, float neutral)
        {
            image = image.Clone();
            for (var y = 0; y < layout.Size; y++)
                for (var x = 0; x < layout.Size; x++)
                    if (!layout.IsCovered(x, y)) image.Texel(x, y).Fill(neutral);
            return image;
        }
    }

    public static TheoryData<string> Rows => ["mask inverted", "curvature read as occlusion", "position's y flipped", "padding left out"];

    [Theory]
    [MemberData(nameof(Rows))]
    public void The_comparison_with_the_bake_catches_it(string what)
    {
        var model = Wrench();
        var mesh = model.Meshes[0].Mesh;
        var layout = BakeLayout.Create(mesh, Size).Layout!;
        var broken = new Broken(new MeshBakeSource(model, model.Meshes[0].Name), what, layout);
        var (node, channels, expected) = what switch
        {
            "mask inverted" => (Node(MeshMaskNode.TypeName, "\"group\": \"steel\""), 1,
                Expected(Bake.Masks(layout, mesh).Single(m => m.Source == MaskSource.Group && m.Name == "steel").Image, layout, 1f)),
            "curvature read as occlusion" => (Node(MeshOcclusionNode.TypeName, "\"rays\": 32, \"reach\": 0.035"), 1,
                Expected(Bake.Occlusion(layout, new RayCaster([.. model.Meshes.Select(m => m.Mesh)]), 0, 32, 0.035f).Image, layout, 1f)),
            "position's y flipped" => (Node(MeshPositionNode.TypeName), 3, Expected(PositionImage(mesh, layout), layout, 128f / 255f)),
            _ => (Node(MeshCurvatureNode.TypeName, "\"width\": 0.003"), 1, Expected(Bake.Curvature(layout, 0.003), layout, 0.5f)),
        };
        var bytes = Run(broken, node, Size);
        Assert.NotNull(FirstDifference(expected, bytes, channels, Size));
        if (what == "mask inverted") Assert.NotNull(MaskCheck(bytes, mesh, layout, "steel"));
    }

    [Fact]
    public void All_open_catches_curvature_read_as_occlusion_on_the_plain_barrel()
    {
        var barrel = PlainBarrel();
        var real = new MeshBakeSource(Carve.Core.Model.Of("barrel", barrel), "barrel");
        var broken = new Broken(real, "curvature read as occlusion", BakeLayout.Create(barrel, Size).Layout!);
        var node = Node(MeshOcclusionNode.TypeName, "\"rays\": 32, \"reach\": 0.15");
        Assert.Null(AllOpen(Run(real, node, Size)));
        Assert.NotNull(AllOpen(Run(broken, node, Size)));
    }
}

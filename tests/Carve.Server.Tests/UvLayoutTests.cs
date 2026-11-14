using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;
using Face = Carve.Core.Face;

namespace Carve.Server.Tests;

/// <summary>T-58: the <c>uvs</c> look's CPU side: the window rule, the composed layout, the lines and the face-label point.</summary>
[Trait("Tier", "unit")]
public sealed class UvLayoutTests
{
    private const double Eps = 1e-6;

    /// <summary>Two quads side by side in z = 0, sharing the edge 1-2.</summary>
    private static PolygonMesh Pair() =>
        TestMeshes.Make([new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(2, 0, 0), new(2, 1, 0)], [[0, 1, 2, 3], [1, 4, 5, 2]]);

    private static readonly Vector2[] Left = [new(0, 1), new(0.5f, 1), new(0.5f, 0), new(0, 0)];
    private static readonly Vector2[] Right = [new(0.5f, 1), new(1, 1), new(1, 0), new(0.5f, 0)];

    private static ElementWindow All(PolygonMesh mesh) => new(0, 0, mesh.Faces.Count);

    private static void Window(UvWindow w, double cu, double cv, double side)
    {
        Assert.Equal(cu, w.CentreU, Eps);
        Assert.Equal(cv, w.CentreV, Eps);
        Assert.Equal(side, w.Side, Eps);
    }

    [Fact]
    public void The_window_is_the_unit_square_with_a_sixteenth_margin_when_every_uv_is_inside()
    {
        var mesh = Pair().WithUvs(MeshUvs.None.WithFace(0, Left).WithFace(1, Right));
        Window(UvLayout.Window(mesh, All(mesh)), 0.5, 0.5, 8.0 / 7);
        Window(UvWindow.Of(mesh, []), 0.5, 0.5, 8.0 / 7);
        Assert.Equal(UvWindow.Unit, UvWindow.Of(Pair(), [0, 1]));

        // At 512 the square spans px 32..480, a cell 56 px; at 256 16..224; at 1024 64..960.
        var w = UvWindow.Unit;
        foreach (var (size, lo, hi) in new[] { (512, 32f, 480f), (256, 16f, 240f), (1024, 64f, 960f) })
        {
            Assert.Equal(lo, w.Pixel(Vector2.Zero, size).X, 3);
            Assert.Equal(lo, w.Pixel(Vector2.Zero, size).Y, 3);
            Assert.Equal(hi, w.Pixel(Vector2.One, size).X, 3);
            Assert.Equal(hi, w.Pixel(Vector2.One, size).Y, 3);
        }
    }

    [Fact]
    public void The_window_grows_each_way_kept_square_and_centred()
    {
        Vector2[] wide = [new(0, 1), new(1.5f, 1), new(1.5f, 0), new(0, 0)];
        var mesh = Pair().WithUvs(MeshUvs.None.WithFace(0, wide));
        var grown = UvLayout.Window(mesh, All(mesh));
        Window(grown, 0.75, 0.5, 1.5 * 8 / 7);
        // The grown 512 frame: the square's top-left at (32, 106.7).
        Assert.Equal(32f, grown.Pixel(Vector2.Zero, 512).X, 3);
        Assert.Equal(106.667f, grown.Pixel(Vector2.Zero, 512).Y, 2);

        Vector2[] up = [new(0, 1), new(1, 1), new(1, -0.5f), new(0, -0.5f)];
        Window(UvWindow.Of(Pair().WithUvs(MeshUvs.None.WithFace(0, up)), [0]), 0.5, 0.25, 1.5 * 8 / 7);

        Vector2[] left = [new(-2, 1), new(1, 1), new(1, 0), new(-2, 0)];
        Window(UvWindow.Of(Pair().WithUvs(MeshUvs.None.WithFace(0, left)), [0]), -0.5, 0.5, 3.0 * 8 / 7);

        Vector2[] down = [new(0, 3), new(1, 3), new(1, 0), new(0, 0)];
        Window(UvWindow.Of(Pair().WithUvs(MeshUvs.None.WithFace(0, down)), [0]), 0.5, 1.5, 3.0 * 8 / 7);

        // Only the faces asked for count.
        var both = Pair().WithUvs(MeshUvs.None.WithFace(0, Left).WithFace(1, down));
        Window(UvWindow.Of(both, [0]), 0.5, 0.5, 8.0 / 7);
        Window(UvLayout.Window(both, new ElementWindow(0, 0, 1)), 0.5, 0.5, 8.0 / 7);
    }

    [Fact]
    public void A_hole_s_uvs_count_toward_the_window()
    {
        Vector3[] v =
        [
            new(-2, -2, 0), new(2, -2, 0), new(2, 2, 0), new(-2, 2, 0),
            new(-1, -1, 0), new(-1, 1, 0), new(1, 1, 0), new(1, -1, 0),
        ];
        var r = PolygonMesh.Create(v, [new Face([0, 1, 2, 3], [[4, 5, 6, 7]])]);
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        Vector2[] corners = [new(0, 1), new(1, 1), new(1, 0), new(0, 0), new(-0.75f, 0.75f), new(0.25f, 0.75f), new(0.25f, 0.25f), new(-0.75f, 0.25f)];
        var mesh = r.Mesh!.WithUvs(MeshUvs.None.WithFace(0, corners));
        Window(UvWindow.Of(mesh, [0]), 0.125, 0.5, 1.75 * 8 / 7);

        // The hole's edges are drawn too: four outer, four inner.
        Assert.Equal(8, UvLayout.Lines(mesh, [0]).Edges.Count);
    }

    [Fact]
    public void The_layout_lays_each_face_at_its_uvs_wound_toward_the_camera_over_a_ground_at_the_window()
    {
        Vector2[] flipped = [new(1, 1), new(0.5f, 1), new(0.5f, 0), new(1, 0)];
        var mesh = Pair().WithUvs(MeshUvs.None.WithFace(0, Left).WithFace(1, flipped));
        var scene = UvLayout.Compose(mesh, All(mesh), Highlight.None);
        var w = scene.Window;
        var vertices = scene.Fill.Vertices!.ToArray();

        // The ground: two triangles whose texcoords are the window's corners, dimmed.
        Assert.Equal(6 + 2 * 2 * 3, vertices.Length);
        float lo = (float)(0.5 - w.Side / 2), hi = (float)(0.5 + w.Side / 2);
        var ground = vertices[..6].Select(p => p.TexCoord).ToHashSet();
        Assert.Equal(new HashSet<Vector2> { new(lo, lo), new(hi, lo), new(hi, hi), new(lo, hi) }, ground);
        Assert.All(scene.Colours[..6], c => Assert.Equal(UvLayout.GroundVertex, c));
        Assert.Equal(34, UvLayout.GroundVertex.R);
        Assert.All(vertices[..6], p => Assert.Equal(UvLayout.GroundZ, p.Position.Z));

        // The faces: each corner at x = u - cu, y = -(v - cv), its texcoord its UV, white; every triangle (the flipped face's
        // too) clockwise in x, y, as the engine is given triangles facing +Z.
        for (var t = 2; t < vertices.Length / 3; t++)
        {
            var tri = vertices[(t * 3)..(t * 3 + 3)];
            foreach (var p in tri)
            {
                Assert.Equal(UvLayout.FaceZ, p.Position.Z);
                Assert.Equal(p.TexCoord.X - 0.5f, p.Position.X, 5);
                Assert.Equal(-(p.TexCoord.Y - 0.5f), p.Position.Y, 5);
            }

            var cross = (tri[1].Position.X - tri[0].Position.X) * (tri[2].Position.Y - tri[0].Position.Y)
                - (tri[1].Position.Y - tri[0].Position.Y) * (tri[2].Position.X - tri[0].Position.X);
            Assert.True(cross < 0, $"triangle {t} winds {cross}");
            Assert.All(scene.Colours[(t * 3)..(t * 3 + 3)], c => Assert.Equal(Rgba8.White, c));
        }

        // Face 0's texcoords cover its UVs; face 1's (flipped) likewise.
        Assert.Equal(Left.ToHashSet(), vertices[6..12].Select(p => p.TexCoord).ToHashSet());
        Assert.Equal(flipped.ToHashSet(), vertices[12..18].Select(p => p.TexCoord).ToHashSet());
    }

    [Fact]
    public void A_highlighted_face_samples_the_white_cell_in_orange_and_an_unmapped_face_is_absent()
    {
        var mesh = Pair().WithUvs(MeshUvs.None.WithFace(1, Right));
        Assert.Equal(new[] { 1 }, UvLayout.Mapped(mesh, All(mesh)));
        var scene = UvLayout.Compose(mesh, All(mesh), Highlight.Of([], [], [1]));
        var vertices = scene.Fill.Vertices!.ToArray();
        Assert.Equal(6 + 6, vertices.Length);
        Assert.All(vertices[6..], p => Assert.Equal(CheckerTexture.WhiteCentre, p.TexCoord));
        var orange = Rgba8.FromVector(new Vector3(LookMeshes.Orange.X, LookMeshes.Orange.Y, LookMeshes.Orange.Z));
        Assert.All(scene.Colours[6..], c => Assert.Equal(orange, c));
    }

    [Fact]
    public void Every_edge_is_white_on_each_side_with_the_border_and_seams_yellow_on_each_side()
    {
        // The two quads cut apart along 1-2: the right one laid away from the left.
        Vector2[] apart = [new(0.75f, 1), new(1, 1), new(1, 0), new(0.75f, 0)];
        var mesh = Pair().WithUvs(MeshUvs.None.WithFace(0, Left).WithFace(1, apart).WithSeams([(2, 1)], true));
        var (edges, seams) = UvLayout.Lines(mesh, [0, 1]);
        Assert.Equal(8, edges.Count);
        Assert.Equal(2, seams.Count);
        Assert.Contains((new Vector2(0.5f, 1), new Vector2(0.5f, 0)), seams);
        Assert.Contains((new Vector2(0.75f, 0), new Vector2(0.75f, 1)), seams);

        var scene = UvLayout.Compose(mesh, All(mesh), Highlight.None);
        Assert.Equal((8 + 4) * 2, scene.Edges.Vertices!.Length);
        Assert.Equal(Empyrean.MeshTopology.LineList, scene.Edges.Topology);
        Assert.All(scene.Edges.Vertices!.ToArray(), p => Assert.Equal(UvLayout.EdgeZ, p.Position.Z));
        // The border's last side, (0,1) to (0,0), drawn at x = -0.5, y from -0.5 to 0.5.
        var ends = scene.Edges.Vertices!.ToArray()[^2..].Select(p => p.Position).ToArray();
        Assert.Equal(new[] { new Vector3(-0.5f, -0.5f, UvLayout.EdgeZ), new Vector3(-0.5f, 0.5f, UvLayout.EdgeZ) }, ends);
        Assert.Equal(4, scene.Seams!.Value.Vertices!.Length);
        Assert.All(scene.Seams!.Value.Vertices!.ToArray(), p => Assert.Equal(UvLayout.SeamZ, p.Position.Z));

        Assert.Null(UvLayout.Compose(mesh.WithUvs(mesh.Uvs!.WithSeams([(2, 1)], false)), All(mesh), Highlight.None).Seams);
        Assert.Equal(new Vector4(1, 0.6867f, 0, 1).Y, UvLayout.Seam.Y, 3);
    }

    [Fact]
    public void A_face_id_sits_at_the_area_centroid_of_its_outer_uv_loop()
    {
        // An L whose centroid is not the corners' mean.
        Vector2[] l = [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)];
        var p = UvLayout.LabelPoint(l);
        Assert.Equal(5 / 6f, p.X, 5);
        Assert.Equal(5 / 6f, p.Y, 5);
        // Either winding gives the same point.
        var back = UvLayout.LabelPoint(l.Reverse().ToArray());
        Assert.Equal(p.X, back.X, 5);
        Assert.Equal(p.Y, back.Y, 5);
        // No area: the corners' mean.
        Assert.Equal(new Vector2(0.5f, 0), UvLayout.LabelPoint([new(0, 0), new(1, 0), new(0.5f, 0)]));

        var mesh = Pair().WithUvs(MeshUvs.None.WithFace(0, Left).WithFace(1, Right));
        var labels = UvLayout.FaceLabels(mesh, new ElementWindow(0, 1, 1)).ToList();
        Assert.Single(labels);
        Assert.Equal("0", labels[0].Text);
        Assert.Equal(0.75f, labels[0].Uv.X, 5);
        Assert.Equal(0.5f, labels[0].Uv.Y, 5);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void The_corner_labels_are_whole_on_the_image_in_the_margin_off_their_corners(int size)
    {
        // About the server font's extent of "(0,0)": wider than the 16 or 32 px margin, so each must slide inward.
        var labels = LabelLayout.UvLabels(UvWindow.Unit, size, [], text => new Vector2(7 * text.Length, 12));
        Assert.Equal(["(0,0)", "(1,0)", "(0,1)", "(1,1)"], labels.Select(l => l.Text));
        var square = (Low: UvWindow.Unit.Pixel(new Vector2(0, 0), size), High: UvWindow.Unit.Pixel(new Vector2(1, 1), size));
        foreach (var l in labels)
        {
            var b = l.Backing;
            Assert.True(b.X >= 0 && b.Y >= 0 && b.X + b.Width <= size && b.Y + b.Height <= size, $"{l.Text} at {b} runs off a {size} frame");
            // Its row is in the margin above or below the square, never over it.
            var top = l.Text.EndsWith("0)");
            Assert.True(top ? b.Y + b.Height <= square.Low.Y : b.Y >= square.High.Y, $"{l.Text} at {b} overlaps the square's rows");
        }
    }
}

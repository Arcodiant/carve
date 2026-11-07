using System.Numerics;
using Carve.Core;
using Carve.Core.Tests;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>Which labels a frame has and where, with no device; the font is the engine's 8x16.</summary>
[Trait("Tier", "unit")]
public class LabelLayoutTests
{
    private static Vector2 Measure(string text) => new(8 * text.Length, 16);

    private static PolygonMesh Cube() => TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces);

    private static IReadOnlyList<OverlayLabel> Plan(PolygonMesh mesh, CameraSpec camera, Projection projection, LabelSet labels, int size = 512, Func<string, Vector2>? measure = null)
    {
        var report = MeshReport.Create(mesh);
        var space = DrawnSpace.Of(report.BoundsMin, report.BoundsMax);
        var frame = ViewFraming.Frame(space.Apply(report.BoundsMax), camera with { Target = camera.Target is { } t ? space.Apply(t) : null }, projection);
        return LabelLayout.Plan(mesh, space, frame, camera.Direction, projection, labels, size, measure ?? Measure);
    }

    private static string[] Ids(IEnumerable<OverlayLabel> labels) => labels.Select(l => l.Text).ToArray();

    private static string[] FacesToward(PolygonMesh mesh, Func<Vector3, bool> pointing) =>
        Enumerable.Range(0, mesh.Faces.Count).Where(f => pointing(mesh.FaceNormal(f))).Select(f => f.ToString()).ToArray();

    [Fact]
    public void None_plans_nothing() =>
        Assert.Empty(Plan(Cube(), new CameraSpec(ViewFraming.NamedViews["front"]), Projection.Perspective, LabelSet.None));

    [Fact]
    public void The_front_view_labels_the_three_faces_turned_to_it_and_their_seven_vertices()
    {
        var mesh = Cube();
        var plan = Plan(mesh, new CameraSpec(ViewFraming.NamedViews["front"]), Projection.Perspective, LabelSet.Both);

        Assert.Equal(FacesToward(mesh, n => n.X + n.Y + n.Z > 0), Ids(plan.Where(l => l.Dot is null)));
        var vertices = Ids(plan.Where(l => l.Dot is not null));
        Assert.Equal(7, vertices.Length);

        // The corner opposite the eye is on no face turned to it.
        var far = Enumerable.Range(0, 8).Single(v => mesh.Vertices[v] == Vector3.Zero).ToString();
        Assert.DoesNotContain(far, vertices);
    }

    [Fact]
    public void Faces_only_and_vertices_only_leave_out_the_other()
    {
        var camera = new CameraSpec(ViewFraming.NamedViews["front"]);
        Assert.All(Plan(Cube(), camera, Projection.Perspective, LabelSet.Faces), l => Assert.Null(l.Dot));
        Assert.All(Plan(Cube(), camera, Projection.Perspective, LabelSet.Vertices), l => Assert.NotNull(l.Dot));
    }

    [Fact]
    public void Faces_come_before_vertices_each_ascending()
    {
        var plan = Plan(Cube(), new CameraSpec(ViewFraming.NamedViews["front"]), Projection.Perspective, LabelSet.Both);
        var faces = plan.TakeWhile(l => l.Dot is null).Select(l => int.Parse(l.Text)).ToArray();
        var vertices = plan.SkipWhile(l => l.Dot is null).Select(l => int.Parse(l.Text)).ToArray();

        Assert.Equal(faces.Order(), faces);
        Assert.Equal(vertices.Order(), vertices);
        Assert.Equal(plan.Count, faces.Length + vertices.Length);
    }

    /// <summary>A floor and a ceiling seen from the side: the eye is between their planes, the direction along them.</summary>
    [Fact]
    public void Perspective_goes_by_the_eye_and_orthographic_by_the_direction()
    {
        var mesh = TestMeshes.Make(
            [new(0, 0, 0), new(0, 0, 1), new(1, 0, 1), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 1, 1), new(0, 1, 1)],
            [[0, 1, 2, 3], [4, 5, 6, 7]]);
        var camera = new CameraSpec(Vector3.UnitX);

        Assert.Equal(["0", "1"], Ids(Plan(mesh, camera, Projection.Perspective, LabelSet.Faces)));
        Assert.Empty(Plan(mesh, camera, Projection.Orthographic, LabelSet.Faces));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_face_label_is_centred_with_a_backing_two_pixels_round_it(int digits)
    {
        var label = Plan(Cube(), new CameraSpec(Vector3.UnitX), Projection.Orthographic, LabelSet.Faces, measure: _ => new Vector2(8 * digits, 16)).Single();

        Assert.Equal(new Vector2(256 - 4 * digits, 248), label.TextAt);
        Assert.Equal(new Empyrean.ScreenRect(254 - 4 * digits, 246, 8 * digits + 4, 20), label.Backing);
    }

    [Fact]
    public void A_vertex_has_its_dot_on_the_point_and_its_id_up_and_to_the_right()
    {
        var label = Plan(Cube(), new CameraSpec(Vector3.UnitX), Projection.Orthographic, LabelSet.Vertices)[0];

        var dot = label.Dot!.Value;
        Assert.Equal(new Vector2(MathF.Round(dot.X + 5), MathF.Round(dot.Y - 18)), label.TextAt);
        Assert.Equal(label.TextAt.X - 2, label.Backing.X);
        Assert.Equal(label.TextAt.Y - 2, label.Backing.Y);
    }

    /// <summary>Zoomed on a corner, the face's centre and the other corners are far off; only the corner looked at stays.</summary>
    [Fact]
    public void A_label_wholly_off_the_image_is_skipped()
    {
        var plan = Plan(Cube(), new CameraSpec(Vector3.UnitX, 8, new Vector3(1, 1, 1)), Projection.Orthographic, LabelSet.Both);

        var dot = Assert.Single(plan).Dot;
        Assert.NotNull(dot);
        Assert.True(Vector2.Distance(new Vector2(256, 256), dot.Value) < 0.01f, dot.ToString());
    }

    /// <summary>Only 20 pixels of the image are left past this face's centre, and its box is well inside.</summary>
    [Fact]
    public void A_box_on_the_image_is_kept()
    {
        var label = Assert.Single(Plan(Cube(), new CameraSpec(Vector3.UnitX, 2, new Vector3(1, 0.5f, 0)), Projection.Orthographic, LabelSet.Faces, 256));
        Assert.InRange(label.Backing.X, 0, 40);
    }

    /// <summary>The (1, 1, 0) corner about three pixels inside the right edge: its id starts past the edge, its dot does not.</summary>
    [Fact]
    public void A_dot_on_the_image_keeps_its_label_though_the_id_runs_off()
    {
        var plan = Plan(Cube(), new CameraSpec(Vector3.UnitX, 8, new Vector3(1, 0.99f, 0.1455f)), Projection.Orthographic, LabelSet.Vertices);

        var label = Assert.Single(plan);
        Assert.InRange(label.Dot!.Value.X, 500, 512);
        Assert.True(label.Backing.X >= 512, label.ToString());
    }

    [Fact]
    public void A_tile_caption_starts_at_four_four_on_its_backing()
    {
        var caption = LabelLayout.Caption("+x", Measure);
        Assert.Equal(new Vector2(4, 4), caption.TextAt);
        Assert.Equal(new Empyrean.ScreenRect(2, 2, 20, 20), caption.Backing);
        Assert.Null(caption.Dot);
    }
}

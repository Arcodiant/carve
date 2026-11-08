using System.Numerics;

namespace Carve.Server.Tests;

[Trait("Tier", "unit")]
public class ViewFramingTests
{
    /// <summary>The frame's extent in normalised device coordinates once 8% is left on each side.</summary>
    private const float Inner = 1 - 2 * 0.08f;

    private static IEnumerable<Vector3> Corners(Vector3 min, Vector3 max)
    {
        foreach (var x in new[] { min.X, max.X })
        foreach (var y in new[] { min.Y, max.Y })
        foreach (var z in new[] { min.Z, max.Z })
        {
            yield return new Vector3(x, y, z);
        }
    }

    private static Vector3 Device(Vector3 drawn, ViewFrame frame)
    {
        var clip = Vector4.Transform(new Vector4(drawn, 1), frame.View * frame.Projection);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    private static readonly (Vector3 Min, Vector3 Max)[] Shapes =
    [
        (Vector3.Zero, Vector3.One), // A cube.
        (new Vector3(-5, -0.5f, -0.5f), new Vector3(5, 0.5f, 0.5f)), // A long box.
        (Vector3.Zero, new Vector3(1, 1, 0)), // A flat quad.
    ];

    /// <summary>The eight named views, then the two directions the scenario frames.</summary>
    private static Vector3[] Directions() =>
        [.. ViewFraming.NamedViews.Values, new Vector3(1, 1, 1), new Vector3(0.4f, 2, 0.8f)];

    private static ViewFrame Orthographic(Vector3 half, Vector3 direction, float zoom = 1) =>
        ViewFraming.Frame(half, new CameraSpec(direction, zoom), Projection.Orthographic);

    private static float[] Elements(Matrix4x4 m) =>
        [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

    [Fact]
    public void There_are_eight_named_views_and_the_axes_are_head_on()
    {
        Assert.Equal(["front", "back", "+x", "-x", "+y", "-y", "+z", "-z"], ViewFraming.NamedViews.Keys);
        Assert.Equal(new Vector3(0, -1, 0), ViewFraming.NamedViews["-y"]);
        Assert.Equal(CameraSpec.ForView(RenderView.Front).Direction, ViewFraming.NamedViews["front"]);
        Assert.Equal(CameraSpec.ForView(RenderView.Back).Direction, ViewFraming.NamedViews["back"]);
    }

    [Fact]
    public void The_orthographic_frame_holds_every_corner_inside_the_margin_and_touches_it()
    {
        foreach (var (min, max) in Shapes)
        foreach (var direction in Directions())
        {
            var space = DrawnSpace.Of(min, max);
            var half = space.Apply(max);
            var frame = Orthographic(half, direction);
            var widest = 0f;
            foreach (var corner in Corners(-half, half))
            {
                var device = Device(corner, frame);
                widest = Math.Max(widest, Math.Max(Math.Abs(device.X), Math.Abs(device.Y)));
                Assert.InRange(device.Z, 0f, 1f);
            }

            Assert.Equal(Inner, widest, 4);
        }
    }

    [Fact]
    public void The_orthographic_frame_keeps_the_perspective_eye_and_light()
    {
        foreach (var (min, max) in Shapes)
        foreach (var direction in Directions())
        {
            var half = DrawnSpace.Of(min, max).Apply(max);
            var perspective = ViewFraming.Frame(half, new CameraSpec(direction));
            var orthographic = Orthographic(half, direction);
            Assert.Equal(perspective.Eye, orthographic.Eye);
            Assert.Equal(perspective.World, orthographic.World);
            Assert.Equal(perspective.LightOrientation, orthographic.LightOrientation);
        }
    }

    [Fact]
    public void The_projected_images_of_parallel_edges_stay_parallel()
    {
        var half = DrawnSpace.Of(Vector3.Zero, Vector3.One).Apply(Vector3.One);
        var along = new[] { new Vector3(2 * half.X, 0, 0), new Vector3(0, 2 * half.Y, 0), new Vector3(0, 0, 2 * half.Z) };
        foreach (var direction in Directions())
        {
            var frame = Orthographic(half, direction);
            foreach (var edge in along)
            {
                // Two edges of the cube along this axis, at opposite ends of it.
                var one = Device(-half + edge, frame) - Device(-half, frame);
                var other = Device(half, frame) - Device(half - edge, frame);
                Assert.Equal(0, one.X * other.Y - one.Y * other.X, 4);
                Assert.Equal(one.X, other.X, 4);
                Assert.Equal(one.Y, other.Y, 4);
            }
        }
    }

    [Fact]
    public void Zoom_3_divides_the_half_height_by_3_so_the_box_comes_out_3_times_larger_and_the_eye_stays()
    {
        foreach (var (min, max) in Shapes)
        foreach (var direction in Directions())
        {
            var half = DrawnSpace.Of(min, max).Apply(max);
            var whole = Orthographic(half, direction);
            var zoomed = Orthographic(half, direction, 3);
            Assert.Equal(whole.Eye, zoomed.Eye);
            Assert.Equal(whole.Projection.M11 * 3, zoomed.Projection.M11, 3);
            Assert.Equal(whole.Projection.M22 * 3, zoomed.Projection.M22, 3);

            var span = Device(half, whole) - Device(-half, whole);
            var zoomedSpan = Device(half, zoomed) - Device(-half, zoomed);
            Assert.Equal(span.X * 3, zoomedSpan.X, 3);
            Assert.Equal(span.Y * 3, zoomedSpan.Y, 3);
        }
    }

    [Fact]
    public void No_matrix_holds_a_NaN_for_either_projection()
    {
        foreach (var (min, max) in Shapes)
        foreach (var direction in Directions())
        foreach (var projection in new[] { Projection.Perspective, Projection.Orthographic })
        {
            var frame = ViewFraming.Frame(DrawnSpace.Of(min, max).Apply(max), new CameraSpec(direction), projection);
            Assert.All(Elements(frame.Projection).Concat(Elements(frame.View)).Concat(Elements(frame.World)), v => Assert.True(float.IsFinite(v)));
        }
    }

    /// <summary>The perspective frame as it stood before orthographic, written out: front and back must not move a bit.</summary>
    [Theory]
    [InlineData(RenderView.Front)]
    [InlineData(RenderView.Back)]
    public void The_perspective_frame_for_front_and_back_is_unchanged_bit_for_bit(RenderView view)
    {
        foreach (var (min, max) in Shapes)
        {
            var half = DrawnSpace.Of(min, max).Apply(max);
            var given = view == RenderView.Front ? new Vector3(1.6f, 1.2f, 2.2f) : -new Vector3(1.6f, 1.2f, 2.2f);
            var direction = Vector3.Normalize(given);
            var rotation = Matrix4x4.CreateLookAt(direction, Vector3.Zero, Vector3.UnitY) with { M41 = 0, M42 = 0, M43 = 0 };
            var tangent = MathF.Tan(0.6f * 0.5f) * (1 - 2 * 0.08f);
            var distance = 1.2f;
            foreach (var corner in Corners(-half, half))
            {
                var seen = Vector3.Transform(corner, rotation);
                distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
            }

            var eye = direction * (distance + 0);
            var view0 = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(0.6f, 1f, (distance - 1) * 0.9f, (distance + 0 + 1) * 1.1f + 0);

            var frame = ViewFraming.Frame(half, view);
            Assert.Equal(Elements(view0).Select(BitConverter.SingleToInt32Bits), Elements(frame.View).Select(BitConverter.SingleToInt32Bits));
            Assert.Equal(Elements(projection).Select(BitConverter.SingleToInt32Bits), Elements(frame.Projection).Select(BitConverter.SingleToInt32Bits));
        }
    }

    private static ViewFrame Frame(Vector3 min, Vector3 max, RenderView view) =>
        ViewFraming.Frame(DrawnSpace.Of(min, max).Apply(max), view);

    public static TheoryData<float, float, float, float, float, float, RenderView> Bounds()
    {
        var data = new TheoryData<float, float, float, float, float, float, RenderView>();
        foreach (var view in new[] { RenderView.Front, RenderView.Back })
        {
            data.Add(0, 0, 0, 1, 1, 1, view); // A cube.
            data.Add(-5, -0.5f, -0.5f, 5, 0.5f, 0.5f, view); // A long box along X.
            data.Add(10, 20, 29, 11, 21, 37, view); // A long box along Z, away from the origin.
            data.Add(0, 0, 0, 1, 1, 0, view); // A flat quad.
            data.Add(0, 0, 0, 3, 0, 2, view); // A flat quad lying down.
            data.Add(0, 0, 0, 1e-8f, 1e-8f, 1e-8f, view); // Tiny at the origin.
            data.Add(0, 0, 0, 1e-12f, 1e-12f, 1e-12f, view); // Tinier.
            data.Add(1000, 0, 0, 1001, 1, 1, view); // Small, far from the origin.
            data.Add(0, 0, 0, 10, 0.01f, 0.01f, view); // A thin box.
            data.Add(0, 0, 0, 0.01f, 10, 10, view); // A thin slab.
            data.Add(1e5f, 0, 0, 1e5f + 1, 1, 1, view); // Far out, where the side still rounds exactly.
            data.Add(4e6f, 4e6f, 4e6f, 4e6f + 1, 4e6f + 1, 4e6f + 1, view);
            data.Add(0, 0, 0, 1e30f, 1e30f, 1e30f, view); // Huge.
            data.Add(0, 0, 0, 1e-30f, 1e-30f, 1e-30f, view); // Tiniest here.
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Bounds))]
    public void All_corners_stay_inside_the_margin_and_the_tightest_reaches_it(
        float x0, float y0, float z0, float x1, float y1, float z1, RenderView view)
    {
        var min = new Vector3(x0, y0, z0);
        var max = new Vector3(x1, y1, z1);
        var space = DrawnSpace.Of(min, max);
        var frame = Frame(min, max, view);

        var tightest = 0f;
        foreach (var corner in Corners(min, max))
        {
            var device = Device(space.Apply(corner), frame);
            Assert.InRange(Math.Abs(device.X), 0, Inner + 1e-4f);
            Assert.InRange(Math.Abs(device.Y), 0, Inner + 1e-4f);
            Assert.InRange(device.Z, 0, 1); // Between the near and far planes.
            tightest = Math.Max(tightest, Math.Max(Math.Abs(device.X), Math.Abs(device.Y)));
        }

        Assert.InRange(tightest, Inner - 0.01f, Inner + 1e-4f);
    }

    [Theory]
    [InlineData(RenderView.Front)]
    [InlineData(RenderView.Back)]
    public void Bounds_of_no_size_keep_a_sound_camera(RenderView view)
    {
        var frame = Frame(new Vector3(3, 4, 5), new Vector3(3, 4, 5), view);

        var device = Device(Vector3.Zero, frame);
        Assert.Equal(0, device.X, 1e-5f);
        Assert.Equal(0, device.Y, 1e-5f);
        Assert.InRange(device.Z, 0, 1);
    }

    [Fact]
    public void Front_looks_along_the_agreed_direction_and_back_from_its_negation()
    {
        var agreed = Vector3.Normalize(new Vector3(1.6f, 1.2f, 2.2f));

        var front = Frame(-Vector3.One, Vector3.One, RenderView.Front);
        var back = Frame(-Vector3.One, Vector3.One, RenderView.Back);

        Assert.Equal(1, Vector3.Dot(Vector3.Normalize(front.Eye), agreed), 1e-4f);
        Assert.Equal(-1, Vector3.Dot(Vector3.Normalize(back.Eye), agreed), 1e-4f);
    }

    [Fact]
    public void The_light_keeps_its_place_relative_to_the_camera()
    {
        // Forward of the light's transform, in the engine's convention, in camera space.
        static Vector3 SeenFromCamera(RenderView view)
        {
            var frame = Frame(-Vector3.One, Vector3.One, view);
            var forward = Vector3.Transform(-Vector3.UnitZ, frame.LightOrientation);
            return Vector3.TransformNormal(forward, frame.View);
        }

        var front = SeenFromCamera(RenderView.Front);
        var back = SeenFromCamera(RenderView.Back);

        Assert.Equal(front.X, back.X, 1e-4f);
        Assert.Equal(front.Y, back.Y, 1e-4f);
        Assert.Equal(front.Z, back.Z, 1e-4f);
    }
}

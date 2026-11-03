using System.Numerics;

namespace Carve.Server.Tests;

/// <summary>T-9: the camera for any direction, with zoom and a target.</summary>
[Trait("Tier", "unit")]
public class CameraFramingTests
{
    private const float Inner = 1 - 2 * 0.08f;

    public static TheoryData<float, float, float> Directions => new()
    {
        { 1, 0, 0 }, { 0, 1, 0 }, { 0, -1, 0 }, { 1, 1, 1 }, { 0.4f, 2, 0.8f },
    };

    private static readonly (Vector3 Min, Vector3 Max)[] Shapes =
    [
        (Vector3.Zero, Vector3.One),
        (new Vector3(-5, -0.5f, -0.5f), new Vector3(5, 0.5f, 0.5f)),
        (Vector3.Zero, new Vector3(1, 1, 0)),
    ];

    private static ViewFrame Frame(Vector3 min, Vector3 max, CameraSpec camera, Vector3? mappedTarget = null) =>
        ViewFraming.Frame(DrawnSpace.Of(min, max).Apply(max), mappedTarget is { } t ? camera with { Target = t } : camera);

    private static Vector3 Device(Vector3 drawn, ViewFrame frame)
    {
        var clip = Vector4.Transform(new Vector4(drawn, 1), frame.View * frame.Projection);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

    private static bool HasNaN(Matrix4x4 m) => float.IsNaN(m.M11 + m.M12 + m.M13 + m.M14 + m.M21 + m.M22 + m.M23 + m.M24
        + m.M31 + m.M32 + m.M33 + m.M34 + m.M41 + m.M42 + m.M43 + m.M44);

    [Theory]
    [MemberData(nameof(Directions))]
    public void Every_corner_of_every_shape_stays_inside_the_margin(float x, float y, float z)
    {
        foreach (var (min, max) in Shapes)
        {
            var space = DrawnSpace.Of(min, max);
            var frame = Frame(min, max, new CameraSpec(new Vector3(x, y, z)));
            var tightest = 0f;
            foreach (var cx in new[] { min.X, max.X })
            foreach (var cy in new[] { min.Y, max.Y })
            foreach (var cz in new[] { min.Z, max.Z })
            {
                var d = Device(space.Apply(new Vector3(cx, cy, cz)), frame);
                Assert.InRange(Math.Abs(d.X), 0, Inner + 1e-4f);
                Assert.InRange(Math.Abs(d.Y), 0, Inner + 1e-4f);
                Assert.InRange(d.Z, 0, 1);
                tightest = Math.Max(tightest, Math.Max(Math.Abs(d.X), Math.Abs(d.Y)));
            }

            // A flat quad seen edge-on has nothing to fill the frame with; the others reach the margin.
            Assert.InRange(tightest, 0, Inner + 1e-4f);
        }
    }

    [Theory]
    [InlineData(0, 1, 0, true)]
    [InlineData(0, -1, 0, true)]
    [InlineData(0.01f, 1, 0, true)]   // About 0.6 degrees from vertical.
    [InlineData(0.05f, 1, 0, false)]  // About 2.9 degrees.
    [InlineData(1, 0, 0, false)]
    [InlineData(0.4f, 2, 0.8f, false)]
    public void Up_is_minus_Z_only_inside_one_degree_of_vertical(float x, float y, float z, bool vertical)
    {
        var d = Vector3.Normalize(new Vector3(x, y, z));
        Assert.Equal(vertical ? -Vector3.UnitZ : Vector3.UnitY, ViewFraming.UpFor(d));

        var frame = Frame(Vector3.Zero, Vector3.One, new CameraSpec(d));
        Assert.False(HasNaN(frame.World) || HasNaN(frame.View) || HasNaN(frame.Projection));
        Assert.False(float.IsNaN(frame.LightOrientation.X + frame.LightOrientation.W));
    }

    [Theory]
    [InlineData(RenderView.Front)]
    [InlineData(RenderView.Back)]
    public void Front_and_back_are_the_M1_frame_exactly(RenderView view)
    {
        var half = DrawnSpace.Of(new Vector3(0, 0, 0), new Vector3(5, 2, 1)).Apply(new Vector3(5, 2, 1));
        var got = ViewFraming.Frame(half, CameraSpec.ForView(view));
        var want = M1(half, view);
        Assert.Equal(want.Eye, got.Eye);
        Assert.Equal(want.World, got.World);
        Assert.Equal(want.View, got.View);
        Assert.Equal(want.Projection, got.Projection);
        Assert.Equal(want.Light, got.LightOrientation);
    }

    /// <summary>The M-1 camera, as it was before T-9.</summary>
    private static (Vector3 Eye, Matrix4x4 World, Matrix4x4 View, Matrix4x4 Projection, Quaternion Light) M1(Vector3 half, RenderView view)
    {
        static Vector3 Dir(RenderView v)
        {
            var front = Vector3.Normalize(new Vector3(1.6f, 1.2f, 2.2f));
            return v == RenderView.Front ? front : -front;
        }

        static Matrix4x4 Rot(Vector3 d) =>
            Matrix4x4.CreateLookAt(d, Vector3.Zero, Vector3.UnitY) with { M41 = 0, M42 = 0, M43 = 0 };

        var direction = Dir(view);
        var rotation = Rot(direction);
        var tangent = MathF.Tan(0.6f * 0.5f) * (1 - 2 * 0.08f);
        var distance = 1.2f;
        for (var corner = 0; corner < 8; corner++)
        {
            var offset = new Vector3(
                (corner & 1) == 0 ? -half.X : half.X,
                (corner & 2) == 0 ? -half.Y : half.Y,
                (corner & 4) == 0 ? -half.Z : half.Z);
            var seen = Vector3.Transform(offset, rotation);
            distance = Math.Max(distance, Math.Max(Math.Abs(seen.X), Math.Abs(seen.Y)) / tangent + seen.Z);
        }

        var eye = direction * distance;
        var lookAt = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
        Matrix4x4.Invert(lookAt, out var world);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(0.6f, 1f, (distance - 1) * 0.9f, (distance + 1) * 1.1f);
        var light = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);
        Matrix4x4.Invert(Rot(direction), out var cameraToWorld);
        var q = Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(light * Rot(Dir(RenderView.Front)) * cameraToWorld));
        return (eye, world, lookAt, projection, q);
    }

    [Fact]
    public void Zoom_3_shrinks_a_box_projected_size_three_times_and_leaves_the_eye()
    {
        var (min, max) = (new Vector3(-5, -0.5f, -0.5f), new Vector3(5, 0.5f, 0.5f));
        var space = DrawnSpace.Of(min, max);
        var one = Frame(min, max, new CameraSpec(new Vector3(0, 0, 1)));
        var three = Frame(min, max, new CameraSpec(new Vector3(0, 0, 1), 3f));
        Assert.Equal(one.Eye, three.Eye);

        // Corners at the same height, so their spread is the box's width on screen.
        var a = space.Apply(new Vector3(-5, 0, 0.5f));
        var b = space.Apply(new Vector3(5, 0, 0.5f));
        var width1 = Math.Abs(Device(a, one).X - Device(b, one).X);
        var width3 = Math.Abs(Device(a, three).X - Device(b, three).X);
        Assert.Equal(3f, width3 / width1, 2);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(-1, 1, -1)]
    [InlineData(0, 0, 0)]
    public void An_off_centre_target_keeps_the_eye_outside_the_bounding_sphere_and_in_front_of_the_target(float x, float y, float z)
    {
        foreach (var direction in new[] { new Vector3(1, 0, 0), new Vector3(0.4f, 2, 0.8f), new Vector3(0, -1, 0), new Vector3(-1, -1, -1) })
        {
            var target = new Vector3(x, y, z) / MathF.Sqrt(3); // On the drawn sphere, as a corner is.
            var frame = Frame(Vector3.Zero, Vector3.One, new CameraSpec(direction, 2f), target);
            Assert.True(frame.Eye.Length() > 1.2f, $"eye at {frame.Eye.Length()}");
            Assert.False(HasNaN(frame.View) || HasNaN(frame.Projection));

            // The target is on the axis of the view, between the planes.
            var d = Device(target, frame);
            Assert.InRange(Math.Abs(d.X), 0, 1e-3f);
            Assert.InRange(Math.Abs(d.Y), 0, 1e-3f);
            Assert.InRange(d.Z, 0, 1);
        }
    }

    [Fact]
    public void The_light_turns_with_the_camera_for_any_direction()
    {
        var front = ViewFraming.LightOrientation(new Vector3(1, 0, 0));
        var above = ViewFraming.LightOrientation(new Vector3(0, 1, 0));
        Assert.NotEqual(front, above);
        Assert.Equal(ViewFraming.LightOrientation(RenderView.Front), ViewFraming.LightOrientation(new Vector3(1.6f, 1.2f, 2.2f)));
    }
}

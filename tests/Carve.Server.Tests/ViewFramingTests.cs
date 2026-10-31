using System.Numerics;

namespace Carve.Server.Tests;

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

    private static Vector3 Device(Vector3 world, ViewFrame frame)
    {
        var clip = Vector4.Transform(new Vector4(world, 1), frame.View * frame.Projection);
        return new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
    }

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
        var frame = ViewFraming.Frame(min, max, view);
        Assert.True(frame.Framed, frame.ToString());

        var tightest = 0f;
        foreach (var corner in Corners(min, max))
        {
            var device = Device(corner, frame);
            Assert.InRange(Math.Abs(device.X), 0, Inner + 1e-4f);
            Assert.InRange(Math.Abs(device.Y), 0, Inner + 1e-4f);
            Assert.InRange(device.Z, 0, 1); // Between the near and far planes.
            tightest = Math.Max(tightest, Math.Max(Math.Abs(device.X), Math.Abs(device.Y)));
        }

        Assert.InRange(tightest, Inner - 0.01f, Inner + ViewFraming.MarginSlack);
    }

    /// <summary>
    /// Far enough out that coordinates round to a real part of the side, so the margin can only
    /// hold as well as that. What this pins is that it is the mesh's margin, not the floor's.
    /// </summary>
    [Theory]
    [InlineData(1e5f, 0, 0, RenderView.Front, 0.03f)]
    [InlineData(1e5f, 0, 0, RenderView.Back, 0.03f)]
    [InlineData(1e6f, 0, 0, RenderView.Front, 0.1f)]
    [InlineData(1e6f, 1e6f, 1e6f, RenderView.Front, 0.1f)]
    [InlineData(1e6f, 1e6f, 1e6f, RenderView.Back, 0.1f)]
    public void A_side_one_box_far_out_fills_the_frame_as_far_as_float_allows(
        float x, float y, float z, RenderView view, float tolerance)
    {
        var min = new Vector3(x, y, z);
        var max = min + Vector3.One;
        var frame = ViewFraming.Frame(min, max, view);

        var tightest = Corners(min, max)
            .Select(c => Device(c, frame))
            .Max(d => Math.Max(Math.Abs(d.X), Math.Abs(d.Y)));

        Assert.True(frame.Framed, frame.ToString());
        Assert.InRange(tightest, Inner - tolerance, Inner + ViewFraming.MarginSlack); // Backed off, never past the line.
    }

    /// <summary>The bounds of the findings that float can't hold, and float's own far end.</summary>
    [Theory]
    [InlineData(4e6f, 1f, RenderView.Front)]
    [InlineData(4e6f, 1f, RenderView.Back)]
    [InlineData(1e7f, 1f, RenderView.Front)]
    [InlineData(1e7f, 1f, RenderView.Back)]
    [InlineData(100f, 4e-5f, RenderView.Front)]
    [InlineData(100f, 4e-5f, RenderView.Back)]
    [InlineData(float.MaxValue / 2, float.MaxValue / 2, RenderView.Front)]
    public void Bounds_rounding_cannot_draw_are_not_framed(float offset, float side, RenderView view)
    {
        var min = new Vector3(offset);

        Assert.False(ViewFraming.Frame(min, min + new Vector3(side), view).Framed);
    }

    [Theory]
    [InlineData(1e19f)]
    [InlineData(1e30f)]
    [InlineData(1e-15f)]
    public void A_cube_at_the_origin_frames_at_any_scale_float_can_hold(float side)
    {
        foreach (var view in new[] { RenderView.Front, RenderView.Back })
        {
            var frame = ViewFraming.Frame(Vector3.Zero, new Vector3(side), view);

            Assert.True(frame.Framed, frame.ToString());
            Assert.All(Corners(Vector3.Zero, new Vector3(side)), c => Assert.InRange(Device(c, frame).Z, 0, 1));
        }
    }

    [Fact]
    public void The_refusal_gives_the_size_and_the_offset_at_any_scale()
    {
        Assert.Equal(
            "This mesh can't be framed: side 1, about 4e+06 from the origin. Move it nearer the origin or scale it, then call define_mesh again.",
            ViewFraming.Refusal(new Vector3(4e6f, 0, 0), new Vector3(4e6f + 1, 1, 1)));
        Assert.Contains("side 3e+38, about 2.6e+38 from", ViewFraming.Refusal(new Vector3(0), new Vector3(3e38f, 3e38f, 3e38f)));
    }

    [Theory]
    [InlineData(1e6f, 1e6f, 1e6f, RenderView.Front)]
    [InlineData(1e6f, 1e6f, 1e6f, RenderView.Back)]
    [InlineData(0, 0, 0, RenderView.Front)]
    [InlineData(0, 0, 0, RenderView.Back)]
    public void The_floor_still_separates_the_camera_from_a_point(float x, float y, float z, RenderView view)
    {
        var point = new Vector3(x, y, z);

        var frame = ViewFraming.Frame(point, point, view);

        Assert.NotEqual(point, frame.Eye);
        Assert.True(float.IsFinite(frame.View.M43) && float.IsFinite(frame.Projection.M33), frame.ToString());
    }

    [Theory]
    [InlineData(RenderView.Front)]
    [InlineData(RenderView.Back)]
    public void A_point_sized_bounds_still_frames(RenderView view)
    {
        var point = new Vector3(3, 4, 5);

        var frame = ViewFraming.Frame(point, point, view);

        var device = Device(point, frame);
        Assert.Equal(0, device.X, 1e-2f);
        Assert.Equal(0, device.Y, 1e-2f);
        Assert.InRange(device.Z, 0, 1);
        Assert.NotEqual(point, frame.Eye);
    }

    [Fact]
    public void Front_looks_along_the_agreed_direction_and_back_from_its_negation()
    {
        var agreed = Vector3.Normalize(new Vector3(1.6f, 1.2f, 2.2f));

        var front = ViewFraming.Frame(-Vector3.One, Vector3.One, RenderView.Front);
        var back = ViewFraming.Frame(-Vector3.One, Vector3.One, RenderView.Back);

        Assert.Equal(1, Vector3.Dot(Vector3.Normalize(front.Eye - front.Target), agreed), 1e-4f);
        Assert.Equal(-1, Vector3.Dot(Vector3.Normalize(back.Eye - back.Target), agreed), 1e-4f);
    }

    [Fact]
    public void The_light_keeps_its_place_relative_to_the_camera()
    {
        // Forward of the light's transform, in the engine's convention, in camera space.
        static Vector3 SeenFromCamera(RenderView view)
        {
            var frame = ViewFraming.Frame(-Vector3.One, Vector3.One, view);
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

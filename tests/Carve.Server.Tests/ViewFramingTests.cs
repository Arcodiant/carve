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

        var tightest = 0f;
        foreach (var corner in Corners(min, max))
        {
            var device = Device(corner, frame);
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

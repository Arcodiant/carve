using System.Numerics;

namespace Carve.Server.Tests;

/// <summary>Drawn space to pixels, against points whose place follows from the framing rule alone.</summary>
[Trait("Tier", "unit")]
public class ScreenProjectionTests
{
    private const float H = 0.57735026f; // A cube's half side in drawn space: its half-diagonal is 1.

    private static readonly Vector3 Half = new(H);

    private static Vector2 Pixel(ViewFrame frame, Vector3 point, int size) => ScreenProjection.ToPixel(point, frame.View, frame.Projection, size);

    private static void AssertNear(float x, float y, Vector2 actual) =>
        Assert.True(Vector2.Distance(new Vector2(x, y), actual) < 0.01f, $"({x}, {y}) against {actual}");

    [Theory]
    [InlineData(256, Projection.Perspective)]
    [InlineData(512, Projection.Perspective)]
    [InlineData(1024, Projection.Orthographic)]
    public void The_point_looked_at_is_the_middle_of_the_image(int size, Projection projection)
    {
        var frame = ViewFraming.Frame(Half, new CameraSpec(ViewFraming.NamedViews["front"]), projection);
        AssertNear(size / 2f, size / 2f, Pixel(frame, Vector3.Zero, size));
    }

    /// <summary>From +X orthographic, +Z is on the left and the margin is 8% of 512: 40.96 pixels from each edge.</summary>
    [Fact]
    public void A_corner_lands_on_its_known_pixel_with_y_down()
    {
        var frame = ViewFraming.Frame(Half, new CameraSpec(Vector3.UnitX), Projection.Orthographic);
        AssertNear(40.96f, 40.96f, Pixel(frame, new Vector3(H, H, H), 512));
        AssertNear(471.04f, 40.96f, Pixel(frame, new Vector3(H, H, -H), 512));
        AssertNear(40.96f, 471.04f, Pixel(frame, new Vector3(H, -H, H), 512));
    }

    [Fact]
    public void A_higher_point_is_higher_on_the_image_in_perspective_too()
    {
        var frame = ViewFraming.Frame(Half, new CameraSpec(ViewFraming.NamedViews["front"]));
        Assert.True(Pixel(frame, new Vector3(0, H, 0), 512).Y < 256);
        Assert.True(Pixel(frame, new Vector3(0, -H, 0), 512).Y > 256);
    }

    [Fact]
    public void Zoom_and_target_are_in_the_matrices()
    {
        var target = new Vector3(H, H, H);
        var frame = ViewFraming.Frame(Half, new CameraSpec(Vector3.UnitX, 3, target), Projection.Orthographic);
        AssertNear(256, 256, Pixel(frame, target, 512));
    }
}

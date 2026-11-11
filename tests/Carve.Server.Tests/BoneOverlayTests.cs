using System.Numerics;
using Carve.Core;
using Xunit;

namespace Carve.Server.Tests;

/// <summary>T-39: where bones land in pixels, against CameraPixels' own projection of the same points.</summary>
[Trait("Tier", "unit")]
public class BoneOverlayTests
{
    [Fact]
    public void Lines_and_joints_land_where_the_framing_rule_puts_the_points_and_a_pose_moves_them()
    {
        var rig = Skeleton.Create([
            new("a", null, Vector3.Zero, Quaternion.Identity),
            new("b", "a", new Vector3(0, 1, 0), Quaternion.Identity),
        ]).Skeleton!;
        var min = new Vector3(-0.2f, 0, -0.2f);
        var max = new Vector3(0.2f, 2, 0.2f);
        var space = DrawnSpace.Of(min, max);
        var frame = ViewFraming.Frame(space.Apply(max), new CameraSpec(Vector3.UnitZ));
        var expected = Carve.Scenario.Crate.CameraPixels.Perspective(min, max, Vector3.UnitZ, 512);

        var rest = BoneOverlay.Plan(BoneDraw.Of(rig, null, false), space, frame, 512);
        Assert.Equal(2, rest.Joints.Count);
        Assert.True(Vector2.Distance(expected(new Vector3(0, 1, 0)), rest.Joints[1]) < 0.01f);
        Assert.True(Vector2.Distance(expected(Vector3.Zero), rest.Lines[0].From) < 0.01f);
        Assert.True(Vector2.Distance(expected(new Vector3(0, 1.5f, 0)), rest.Lines[1].To) < 0.01f);

        var turned = new Pose(new Dictionary<string, (Quaternion?, Vector3?)> { ["a"] = (Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2), null) });
        var posed = BoneOverlay.Plan(BoneDraw.Of(rig, turned, false), space, frame, 512);
        // A quarter turn about +Z puts b at (-1, 0, 0): the image's left, and the leaf runs on to (-1.5, 0, 0).
        Assert.True(Vector2.Distance(expected(new Vector3(-1, 0, 0)), posed.Joints[1]) < 0.01f);
        Assert.True(posed.Joints[1].X < posed.Joints[0].X);
        Assert.True(Vector2.Distance(expected(new Vector3(-1.5f, 0, 0)), posed.Lines[1].To) < 0.01f);
    }
}

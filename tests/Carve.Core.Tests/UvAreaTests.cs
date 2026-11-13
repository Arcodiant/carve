using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class UvAreaTests
{
    [Fact]
    public void A_counter_clockwise_on_screen_square_with_v_down_is_negative()
    {
        // (0,0) top-left, v down: (0,0) -> (0,1) -> (1,1) -> (1,0) goes down, right, up: counter-clockwise on screen.
        var face = new Face([0, 1, 2, 3]);
        Assert.Equal(-1.0, UvArea.Signed(face, [new(0, 0), new(0, 1), new(1, 1), new(1, 0)]));
        Assert.Equal(1.0, UvArea.Signed(face, [new(0, 0), new(1, 0), new(1, 1), new(0, 1)]));
    }

    [Fact]
    public void A_hole_is_subtracted()
    {
        var face = new Face([0, 1, 2, 3], [[4, 5, 6, 7]]);
        var uvs = new Vector2[]
        {
            new(0, 0), new(0, 4), new(4, 4), new(4, 0),
            new(1, 1), new(3, 1), new(3, 3), new(1, 3), // opposite winding
        };
        Assert.Equal(-12.0, UvArea.Signed(face, uvs));
    }
}

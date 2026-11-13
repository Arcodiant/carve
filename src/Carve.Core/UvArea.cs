using System.Numerics;

namespace Carve.Core;

/// <summary>The signed area of a face's UVs (T-60, shared with T-55 and T-56).</summary>
public static class UvArea
{
    /// <summary>
    /// The signed shoelace area of <paramref name="uvs"/>, one per entry of <see cref="Face.Corners"/>, in double: the outer
    /// loop's plus each hole's, so with the holes wound opposite the outer loop, as a face's are, a hole's area is subtracted.
    /// Negative is unflipped (counter-clockwise on screen, u right and v down, the glTF axes); positive is flipped; the
    /// magnitude is the UV area. Unchecked: <paramref name="uvs"/> must have as many entries as the face has corners.
    /// </summary>
    public static double Signed(Face face, IReadOnlyList<Vector2> uvs)
    {
        var sum = Loop(uvs, 0, face.Loop.Count);
        var start = face.Loop.Count;
        foreach (var hole in face.Holes)
        {
            sum += Loop(uvs, start, hole.Count);
            start += hole.Count;
        }
        return sum / 2;
    }

    private static double Loop(IReadOnlyList<Vector2> uvs, int start, int count)
    {
        var sum = 0.0;
        for (var i = 0; i < count; i++)
        {
            var a = uvs[start + i];
            var b = uvs[start + (i + 1) % count];
            sum += (double)a.X * b.Y - (double)b.X * a.Y;
        }
        return sum;
    }
}

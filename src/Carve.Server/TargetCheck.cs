using System.Globalization;
using System.Numerics;

namespace Carve.Server;

/// <summary>The render target's check against a mesh's bounds, shared by the tool and the frame thread.</summary>
internal static class TargetCheck
{
    /// <summary>The error text if <paramref name="target"/> is outside the box, else null.</summary>
    public static string? Refusal(Vector3 target, Vector3 min, Vector3 max)
    {
        if (target.X >= min.X && target.X <= max.X
            && target.Y >= min.Y && target.Y <= max.Y
            && target.Z >= min.Z && target.Z <= max.Z)
        {
            return null;
        }

        return $"target: ({Join(target)}) is outside the mesh's bounds, ({Join(min)}) to ({Join(max)}).";
    }

    private static string Join(Vector3 v) =>
        string.Join(", ", new[] { v.X, v.Y, v.Z }.Select(x => x.ToString("G", CultureInfo.InvariantCulture)));
}

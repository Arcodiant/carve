using System.Numerics;

namespace Carve.Core;

/// <summary>What <see cref="AutoWeights.Weigh"/> did: the vertices weighed, how many weighted vertices it left as they were, and how many of the mesh's vertices are still unweighted.</summary>
public sealed record WeighOutcome(int Weighed, int Kept, int Unweighted);

/// <summary>
/// Automatic weights (D-6, Weights; T-37): each vertex is weighted by its distance to each bone's segment, with the falloff
/// <c>(nearest / d)^</c><see cref="Power"/> (so the nearest bone's raw weight is 1), the largest four kept (ties to the lower
/// bone index) and normalised. A vertex at distance 0 from one or more bones is shared equally among exactly those. A bone's
/// segment runs from its rest head to each child's head (the nearest of them counts); a leaf's runs on along its parent's
/// direction for half its parent's length; a lone root with no children is its head alone. Pure and deterministic: the same
/// mesh and skeleton give the same weights bit for bit, worked in double in bone order.
/// </summary>
public static class AutoWeights
{
    /// <summary>The falloff's exponent: a bone twice as far as the nearest pulls 1/16 as hard. Scale-free, so a model's units do not matter.</summary>
    public const double Power = 4;

    /// <summary>The segments of each bone, in model space at rest, as (head, end) pairs: one per child, or one for a leaf.</summary>
    public static (Vector3D Head, Vector3D End)[][] Segments(Skeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        var n = skeleton.Bones.Count;
        var head = new Vector3D[n];
        for (var i = 0; i < n; i++) head[i] = Vector3D.From(skeleton.RestWorldPosition(i));

        var segments = new (Vector3D, Vector3D)[n][];
        for (var i = 0; i < n; i++)
        {
            var children = Enumerable.Range(i + 1, n - i - 1).Where(c => skeleton.Bones[c].Parent == i).ToArray();
            if (children.Length > 0)
            {
                segments[i] = [.. children.Select(c => (head[i], head[c]))];
            }
            else if (skeleton.Bones[i].Parent is var p and >= 0)
            {
                // Along the parent's direction (parent head to this head) for half the parent's length toward this bone.
                segments[i] = [(head[i], head[i] + (head[i] - head[p]) * 0.5)];
            }
            else
            {
                segments[i] = [(head[i], head[i])];
            }
        }
        return segments;
    }

    /// <summary>The distance from <paramref name="p"/> to the segment from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static double Distance(Vector3D p, Vector3D a, Vector3D b)
    {
        var ab = b - a;
        var length2 = ab.Dot(ab);
        var t = length2 > 0 ? Math.Clamp((p - a).Dot(ab) / length2, 0, 1) : 0;
        return (p - (a + ab * t)).Length();
    }

    /// <summary>The distance from <paramref name="p"/> to bone <paramref name="bone"/>: the nearest of its segments.</summary>
    public static double Distance(Vector3D p, (Vector3D Head, Vector3D End)[] bone) => bone.Min(s => Distance(p, s.Head, s.End));

    /// <summary>
    /// The influences on a vertex at <paramref name="p"/> from the bones <paramref name="bones"/> (indices, ascending), given
    /// every bone's <paramref name="segments"/>: normalised, at most four, in bone order.
    /// </summary>
    public static Influence[] For(Vector3D p, (Vector3D Head, Vector3D End)[][] segments, IReadOnlyList<int> bones)
    {
        var d = bones.Select(b => Distance(p, segments[b])).ToArray();
        var nearest = d.Min();
        var raw = new Dictionary<int, double>();
        for (var k = 0; k < bones.Count; k++)
        {
            var w = nearest == 0 ? (d[k] == 0 ? 1 : 0) : Math.Pow(nearest / d[k], Power);
            if (w > 0) raw[bones[k]] = w;
        }
        return VertexWeights.Normalise(raw, out _);
    }

    /// <summary>
    /// <paramref name="mesh"/>'s weights after weighing it on <paramref name="skeleton"/>. <paramref name="bones"/> (indices) limits
    /// it to those bones, or null for all: a vertex is then in scope when its nearest bone of the whole skeleton (ties to the
    /// lower index) is one of them, and is weighted among them alone. In scope, an unweighted vertex is always weighed, and a
    /// weighted one only when <paramref name="replace"/> is true (D-6: any existing weight counts as explicit).
    /// </summary>
    public static VertexWeights Weigh(PolygonMesh mesh, Skeleton skeleton, IReadOnlyCollection<int>? bones, bool replace, out WeighOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var segments = Segments(skeleton);
        var all = Enumerable.Range(0, skeleton.Bones.Count).ToArray();
        var named = bones is null ? all : [.. bones.Distinct().Order()];
        var existing = mesh.Weights ?? VertexWeights.None;
        var result = new SortedDictionary<int, Influence[]>();
        foreach (var v in existing.Vertices) result[v] = [.. existing.Of(v)];

        int weighed = 0, kept = 0;
        for (var v = 0; v < mesh.Vertices.Count; v++)
        {
            var p = Vector3D.From(mesh.Vertices[v]);
            if (bones is not null && Array.BinarySearch(named, Nearest(p, segments)) < 0) continue;
            if (existing.Has(v) && !replace)
            {
                kept++;
                continue;
            }
            result[v] = For(p, segments, named);
            weighed++;
        }
        var weights = VertexWeights.Of(result);
        outcome = new WeighOutcome(weighed, kept, mesh.Vertices.Count - weights.Count);
        return weights;
    }

    private static int Nearest(Vector3D p, (Vector3D Head, Vector3D End)[][] segments)
    {
        var best = 0;
        var bestD = double.PositiveInfinity;
        for (var b = 0; b < segments.Length; b++)
        {
            var d = Distance(p, segments[b]);
            if (d < bestD)
            {
                best = b;
                bestD = d;
            }
        }
        return best;
    }
}

/// <summary>A point or direction in double precision, for <see cref="AutoWeights"/>'s distances.</summary>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D From(Vector3 v) => new(v.X, v.Y, v.Z);
    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3D operator *(Vector3D a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public double Dot(Vector3D b) => X * b.X + Y * b.Y + Z * b.Z;
    public double Length() => Math.Sqrt(Dot(this));
}

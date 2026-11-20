using System.Numerics;

namespace Carve.Core.Baking;

/// <summary>The closest triangle a ray of <see cref="RayCaster"/> meets: its distance, and which mesh, face and triangle it is.</summary>
/// <param name="Distance">Along the ray's unit direction, from its origin.</param>
/// <param name="Mesh">The index of the mesh in the list the caster was built from.</param>
/// <param name="Face">The face of that mesh.</param>
/// <param name="Triangle">The triangle's index in <see cref="RayCaster.Triangles"/>.</param>
public readonly record struct RayHit(float Distance, int Mesh, int Face, int Triangle);

/// <summary>
/// A bounding volume hierarchy over the triangles of some meshes at rest (T-124), from each mesh's own positions
/// (<see cref="PolygonMesh.Vertices"/>, never a pose) and <see cref="Triangulator.Triangulate"/>'s triangles. It answers the
/// closest hit within a distance. Both sides of a triangle block. It is built and cast the same way every time: the split is the
/// median of the triangles' centroids along the box's longest axis, ties broken by triangle index, so two casters of one model
/// are the same tree and give the same hits.
/// </summary>
public sealed class RayCaster
{
    private const int LeafSize = 4;

    // Triangle corners, three per triangle in the tree's leaf order.
    private readonly Vector3[] _a, _e1, _e2;
    private readonly int[] _order;
    private readonly List<(int Mesh, int Face)> _owners;

    // Flattened nodes: a leaf has Count > 0 and its triangles at [First, First + Count) of the leaf order; an inner node has
    // Count 0, its left child next to it and its right child at First.
    private readonly Vector3[] _min, _max;
    private readonly int[] _first, _count;
    private int _nodes;

    /// <summary>Builds the tree over <paramref name="meshes"/>' live faces.</summary>
    public RayCaster(IReadOnlyList<PolygonMesh> meshes)
    {
        ArgumentNullException.ThrowIfNull(meshes);
        var corners = new List<(Vector3 A, Vector3 B, Vector3 C)>();
        _owners = [];
        for (var m = 0; m < meshes.Count; m++)
        {
            var mesh = meshes[m];
            foreach (var tri in Triangulator.Triangulate(mesh))
            {
                corners.Add((mesh.Vertices[tri.A], mesh.Vertices[tri.B], mesh.Vertices[tri.C]));
                _owners.Add((m, tri.Face));
            }
        }

        var count = corners.Count;
        Triangles = count;
        var boundsMin = new Vector3(float.MaxValue);
        var boundsMax = new Vector3(float.MinValue);
        foreach (var (a, b, c) in corners)
        {
            boundsMin = Vector3.Min(boundsMin, Vector3.Min(a, Vector3.Min(b, c)));
            boundsMax = Vector3.Max(boundsMax, Vector3.Max(a, Vector3.Max(b, c)));
        }
        if (count == 0) boundsMin = boundsMax = Vector3.Zero;
        BoundsMin = boundsMin;
        BoundsMax = boundsMax;

        var index = Enumerable.Range(0, count).ToArray();
        var centroid = corners.Select(t => (t.A + t.B + t.C) / 3).ToArray();
        var capacity = Math.Max(1, 2 * count);
        _min = new Vector3[capacity];
        _max = new Vector3[capacity];
        _first = new int[capacity];
        _count = new int[capacity];
        if (count > 0) Build(index, 0, count, corners, centroid);

        _order = index;
        _a = new Vector3[count];
        _e1 = new Vector3[count];
        _e2 = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            var (a, b, c) = corners[index[i]];
            _a[i] = a;
            _e1[i] = b - a;
            _e2[i] = c - a;
        }
    }

    /// <summary>The number of triangles in the tree.</summary>
    public int Triangles { get; }

    /// <summary>The least corner of the box around every triangle.</summary>
    public Vector3 BoundsMin { get; }

    /// <summary>The greatest corner of the box around every triangle.</summary>
    public Vector3 BoundsMax { get; }

    /// <summary>The mesh and face of triangle <paramref name="triangle"/> (an index in the order the meshes were given).</summary>
    public (int Mesh, int Face) OwnerOf(int triangle) => _owners[triangle];

    private int Build(int[] index, int start, int end, List<(Vector3 A, Vector3 B, Vector3 C)> corners, Vector3[] centroid)
    {
        var node = _nodes++;
        var lo = new Vector3(float.MaxValue);
        var hi = new Vector3(float.MinValue);
        for (var i = start; i < end; i++)
        {
            var (a, b, c) = corners[index[i]];
            lo = Vector3.Min(lo, Vector3.Min(a, Vector3.Min(b, c)));
            hi = Vector3.Max(hi, Vector3.Max(a, Vector3.Max(b, c)));
        }
        _min[node] = lo;
        _max[node] = hi;

        var n = end - start;
        if (n <= LeafSize)
        {
            _first[node] = start;
            _count[node] = n;
            return node;
        }

        var cLo = new Vector3(float.MaxValue);
        var cHi = new Vector3(float.MinValue);
        for (var i = start; i < end; i++)
        {
            cLo = Vector3.Min(cLo, centroid[index[i]]);
            cHi = Vector3.Max(cHi, centroid[index[i]]);
        }
        var extent = cHi - cLo;
        var axis = extent.X >= extent.Y && extent.X >= extent.Z ? 0 : extent.Y >= extent.Z ? 1 : 2;
        static float On(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
        System.Array.Sort(index, start, n, Comparer<int>.Create((p, q) =>
        {
            var c = On(centroid[p], axis).CompareTo(On(centroid[q], axis));
            return c != 0 ? c : p.CompareTo(q);
        }));

        var mid = start + n / 2;
        _count[node] = 0;
        Build(index, start, mid, corners, centroid);
        _first[node] = Build(index, mid, end, corners, centroid);
        return node;
    }

    /// <summary>
    /// The closest triangle the ray from <paramref name="origin"/> along the unit <paramref name="direction"/> meets at a distance
    /// over 0 and under <paramref name="reach"/>, either side of the triangle.
    /// </summary>
    public bool Cast(Vector3 origin, Vector3 direction, float reach, out RayHit hit)
    {
        hit = default;
        if (Triangles == 0) return false;
        // A zero component is nudged so the slab test never multiplies 0 by infinity.
        static float Inverse(float d) => 1f / (d == 0 ? 1e-30f : d);
        var inverse = new Vector3(Inverse(direction.X), Inverse(direction.Y), Inverse(direction.Z));
        var best = reach;
        var bestTriangle = -1;
        Span<int> stack = stackalloc int[64];
        var top = 0;
        stack[top++] = 0;
        while (top > 0)
        {
            var node = stack[--top];
            if (!Box(_min[node], _max[node], origin, inverse, best, out _)) continue;
            if (_count[node] > 0)
            {
                for (int i = _first[node], end = i + _count[node]; i < end; i++)
                {
                    if (Intersect(origin, direction, _a[i], _e1[i], _e2[i], out var t) && t < best)
                    {
                        best = t;
                        bestTriangle = i;
                    }
                }
                continue;
            }

            int left = node + 1, right = _first[node];
            var hitLeft = Box(_min[left], _max[left], origin, inverse, best, out var nearLeft);
            var hitRight = Box(_min[right], _max[right], origin, inverse, best, out var nearRight);
            if (hitLeft && hitRight)
            {
                // The nearer child is searched first, so the farther is usually cut by the hit it finds.
                if (nearLeft <= nearRight) { stack[top++] = right; stack[top++] = left; }
                else { stack[top++] = left; stack[top++] = right; }
            }
            else if (hitLeft) stack[top++] = left;
            else if (hitRight) stack[top++] = right;
        }

        if (bestTriangle < 0) return false;
        var triangle = _order[bestTriangle];
        var (mesh, face) = _owners[triangle];
        hit = new RayHit(best, mesh, face, triangle);
        return true;
    }

    /// <summary>
    /// The ray against one triangle with corner <paramref name="a"/> and edges <paramref name="e1"/> (to B) and
    /// <paramref name="e2"/> (to C), Möller and Trumbore's test with no culling, so either side meets it: true with the distance
    /// <paramref name="t"/> when it meets it at a distance over 0.
    /// </summary>
    public static bool Intersect(Vector3 origin, Vector3 direction, Vector3 a, Vector3 e1, Vector3 e2, out float t)
    {
        t = 0;
        var p = Vector3.Cross(direction, e2);
        var det = Vector3.Dot(e1, p);
        if (det == 0 || !float.IsFinite(det)) return false;
        var inv = 1f / det;
        var s = origin - a;
        var u = Vector3.Dot(s, p) * inv;
        if (u < 0 || u > 1) return false;
        var q = Vector3.Cross(s, e1);
        var v = Vector3.Dot(direction, q) * inv;
        if (v < 0 || u + v > 1) return false;
        t = Vector3.Dot(e2, q) * inv;
        return t > 0;
    }

    private static bool Box(Vector3 lo, Vector3 hi, Vector3 origin, Vector3 inverse, float reach, out float near)
    {
        var t1 = (lo - origin) * inverse;
        var t2 = (hi - origin) * inverse;
        var tMin = Vector3.Min(t1, t2);
        var tMax = Vector3.Max(t1, t2);
        near = MathF.Max(MathF.Max(tMin.X, tMin.Y), MathF.Max(tMin.Z, 0));
        var far = MathF.Min(MathF.Min(tMax.X, tMax.Y), MathF.Min(tMax.Z, reach));
        return near <= far;
    }
}

namespace Carve.Core;

/// <summary>
/// One face of a <see cref="PolygonMesh"/>: an outer loop and zero or more hole loops, each of three or more vertex ids.
/// Seen from outside, the outer loop winds counter-clockwise and every hole clockwise (opposite to it), so a hole edge
/// shared with a face filling the hole is crossed once each way. A face's normal, centre and plane are its outer loop's;
/// its area is the outer loop's less its holes'.
/// </summary>
/// <remarks>
/// <para><b>Corners.</b> A face's corners are numbered outer loop first, then each hole in order (<see cref="Corners"/>):
/// with an outer loop of 4 and one hole of 4, corners 0..3 are the outer loop's and 4..7 the hole's. Corner normals
/// (<see cref="Shading.Corners"/>), the face's edges (<see cref="MeshTopology.FaceEdges"/>) and its listed vertices all
/// take this order. A plain face's corners are its loop, as before holes.</para>
/// <para><b>Names.</b> The outer loop's corner <c>i</c> is <c>faces[n][i]</c>, a hole <c>faces[n].holes[k]</c> and one of its
/// corners <c>faces[n].holes[k][i]</c> (<see cref="LoopName"/>, <see cref="CornerName"/>).</para>
/// <para>The type is not a list of ints on purpose: code written for plain loops has to say what it does with holes.</para>
/// </remarks>
public sealed class Face : IEquatable<Face>
{
    private readonly int[] _loop;
    private readonly int[][] _holes;
    private readonly int[] _corners;

    /// <summary>A face of <paramref name="loop"/> with <paramref name="holes"/> (none when null). The lists are copied.</summary>
    public Face(IEnumerable<int> loop, IEnumerable<IEnumerable<int>>? holes = null)
    {
        _loop = loop.ToArray();
        _holes = holes?.Select(h => h.ToArray()).ToArray() ?? [];
        _corners = _holes.Length == 0 ? _loop : [.. _loop, .. _holes.SelectMany(h => h)];
        Loop = Array.AsReadOnly(_loop);
        Holes = Array.AsReadOnly(_holes.Select(h => (IReadOnlyList<int>)Array.AsReadOnly(h)).ToArray());
        Corners = Array.AsReadOnly(_corners);
    }

    /// <summary>A plain face, as <c>define_mesh</c>'s plain array form.</summary>
    public static implicit operator Face(int[] loop) => new(loop);

    /// <summary>The outer loop, counter-clockwise seen from outside.</summary>
    public IReadOnlyList<int> Loop { get; }

    /// <summary>The hole loops in order, each clockwise seen from outside; empty for a plain face.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Holes { get; }

    public bool HasHoles => _holes.Length > 0;

    /// <summary>Every corner's vertex id: the outer loop, then each hole in order. A plain face's is its loop.</summary>
    public IReadOnlyList<int> Corners { get; }

    /// <summary>The loop <c>hole</c> names: the outer loop for -1, else <see cref="Holes"/>[hole].</summary>
    public IReadOnlyList<int> LoopAt(int hole) => hole < 0 ? Loop : Holes[hole];

    /// <summary>Where corner <paramref name="corner"/> (an index into <see cref="Corners"/>) is: its loop (-1 outer, else the hole) and its index there.</summary>
    public (int Hole, int Index) Locate(int corner)
    {
        if (corner < _loop.Length) return (-1, corner);
        var c = corner - _loop.Length;
        for (var h = 0; h < _holes.Length; h++)
        {
            if (c < _holes[h].Length) return (h, c);
            c -= _holes[h].Length;
        }
        throw new ArgumentOutOfRangeException(nameof(corner));
    }

    /// <summary>The index into <see cref="Corners"/> of the corner at <paramref name="index"/> of loop <paramref name="hole"/> (-1 outer).</summary>
    public int CornerAt(int hole, int index)
    {
        if (hole < 0) return index;
        var c = _loop.Length;
        for (var h = 0; h < hole; h++) c += _holes[h].Length;
        return c + index;
    }

    /// <summary>The index into <see cref="Corners"/> of <paramref name="vertex"/>; a face never repeats a vertex across its loops.</summary>
    public int CornerOf(int vertex)
    {
        var i = Array.IndexOf(_corners, vertex);
        return i >= 0 ? i : throw new ArgumentException("the vertex is not in the face", nameof(vertex));
    }

    /// <summary>The corners either side of <paramref name="corner"/> in its own loop: the vertex ids it joins by an edge.</summary>
    public (int Previous, int Next) Neighbours(int corner)
    {
        var (hole, i) = Locate(corner);
        var loop = LoopAt(hole);
        return (loop[(i + loop.Count - 1) % loop.Count], loop[(i + 1) % loop.Count]);
    }

    /// <summary>This face with every vertex id mapped by <paramref name="map"/>, holes kept.</summary>
    public Face Map(Func<int, int> map) => new(_loop.Select(map), _holes.Select(h => h.Select(map)));

    /// <summary><c>faces[n]</c> for the outer loop (-1), else <c>faces[n].holes[k]</c>.</summary>
    public static string LoopName(int face, int hole) => hole < 0 ? $"faces[{face}]" : $"faces[{face}].holes[{hole}]";

    /// <summary><c>faces[n][i]</c> for an outer corner, else <c>faces[n].holes[k][i]</c>.</summary>
    public static string CornerName(int face, int hole, int index) => $"{LoopName(face, hole)}[{index}]";

    public bool Equals(Face? other) =>
        other is not null && _loop.AsSpan().SequenceEqual(other._loop) && _holes.Length == other._holes.Length
        && _holes.Zip(other._holes).All(p => p.First.AsSpan().SequenceEqual(p.Second));

    public override bool Equals(object? obj) => Equals(obj as Face);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var v in _loop) hash.Add(v);
        foreach (var h in _holes) { hash.Add(-1); foreach (var v in h) hash.Add(v); }
        return hash.ToHashCode();
    }

    public override string ToString() => _holes.Length == 0
        ? $"[{string.Join(", ", _loop)}]"
        : $"{{loop: [{string.Join(", ", _loop)}], holes: [{string.Join(", ", _holes.Select(h => $"[{string.Join(", ", h)}]"))}]}}";
}

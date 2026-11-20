using System.Numerics;

namespace Carve.Core.Baking;

/// <summary>How a texel of a <see cref="BakeLayout"/> is covered.</summary>
public enum BakeCoverage
{
    /// <summary>No face covers it: padding may fill it (<see cref="Bake.Pad"/>).</summary>
    None,

    /// <summary>A triangle's uv triangle holds the texel's centre.</summary>
    Centre,

    /// <summary>
    /// The centre lies outside every triangle but within half a texel's diagonal of one, and was clamped to its nearest point.
    /// A centre exactly on an island's bottom or right boundary, which the top-left rule gives to no triangle, is
    /// <see cref="Centre"/>: it lies on the face.
    /// </summary>
    Clamp,
}

/// <summary>
/// What one covered texel of a <see cref="BakeLayout"/> shows. <see cref="Weights"/> are the barycentric weights of the
/// sampled point on triangle <see cref="Triangle"/> (an index into <see cref="BakeLayout.Triangles"/>, which is
/// <see cref="Triangulator.Triangulate"/>'s list), for its corners <c>A</c>, <c>B</c>, <c>C</c> in that order; they sum to
/// 1 and none is negative. The sampled point is the texel's centre, or for a <see cref="BakeCoverage.Clamp"/> texel the
/// nearest point of the triangle to it.
/// </summary>
/// <param name="X">The texel's column.</param>
/// <param name="Y">The texel's row, top row 0.</param>
/// <param name="Face">The face the texel shows.</param>
/// <param name="Triangle">The index of its triangle in <see cref="BakeLayout.Triangles"/>.</param>
/// <param name="Weights">Barycentric weights of corners A, B, C.</param>
/// <param name="Coverage"><see cref="BakeCoverage.Centre"/> or <see cref="BakeCoverage.Clamp"/>.</param>
/// <param name="Uv">The uv of the sampled point.</param>
/// <param name="Position">The world position at rest, from the mesh's own positions (never a pose).</param>
/// <param name="ShadingNormal">The unit interpolated shading normal, which render lights: the face normal on a flat mesh, <see cref="Shading.Corners"/>' corner normals on a smooth one.</param>
/// <param name="FaceNormal">The face's geometric (Newell) normal, <see cref="PolygonMesh.FaceNormal"/>.</param>
/// <param name="Frame">The engine's tangent frame at the texel, about <paramref name="ShadingNormal"/>.</param>
public readonly record struct BakeTexel(
    int X, int Y, int Face, int Triangle, Vector3 Weights, BakeCoverage Coverage, Vector2 Uv,
    Vector3 Position, Vector3 ShadingNormal, Vector3 FaceNormal, TangentFrame Frame);

/// <summary>What <see cref="BakeLayout.Create(PolygonMesh, int)"/> gives: a layout, or an error to pass through as it stands.</summary>
public sealed class BakeLayoutResult
{
    internal BakeLayoutResult(BakeLayout? layout, string? error, IReadOnlyList<int> unmapped)
    {
        Layout = layout;
        Error = error;
        Unmapped = unmapped;
    }

    public BakeLayout? Layout { get; }

    /// <summary>Why there is no layout; null when there is one.</summary>
    public string? Error { get; }

    /// <summary>The faces that were to be baked but have no UVs, ascending: skipped, never baked, for a warning.</summary>
    public IReadOnlyList<int> Unmapped { get; }

    public bool Succeeded => Layout is not null;
}

/// <summary>
/// The coverage of one mesh's UV layout at one square power-of-two size (M-24, T-120): for each texel, the face and the
/// point of it that the texel shows, with that point's rest position, normals and tangent frame (<see cref="BakeTexel"/>).
/// Every bake is built on it. Immutable; the texels are worked out once at <see cref="Create(PolygonMesh, int)"/>, and a
/// texel's position, normals and frame are interpolated from its triangle each time it is read, the same each time.
/// </summary>
/// <remarks>
/// <para><b>Coverage.</b> Texel (x, y)'s centre is at uv ((x + 0.5) / size, (y + 0.5) / size). It is covered by the
/// triangle whose uv triangle holds its centre; a centre on an edge two triangles share is settled by a top-left fill
/// rule (in the image, top row first), so no texel is counted twice. A texel whose centre lies outside every triangle
/// but within half a texel's diagonal of one (<see cref="ClampReach"/>, measured in texels) is covered too, clamped to
/// the nearest point of the nearest triangle, so a face a few texels wide still samples. Of two faces in reach the
/// nearer takes it; a tie goes to the lower face id (and within a face to the lower triangle). A triangle whose uv
/// triangle has no area covers nothing.</para>
/// <para><b>Refusals.</b> Two triangles that both hold one texel centre overlap, and the layout is refused naming the
/// face pairs (a face twice when its own triangles overlap), at most <see cref="NamedOverlaps"/> with a count of the
/// rest. Faces with no UVs are skipped and listed in <see cref="Unmapped"/>. A mesh with no UVs is an error.</para>
/// </remarks>
public sealed partial class BakeLayout
{
    /// <summary>The smallest size, in texels a side.</summary>
    public const int MinSize = 64;

    /// <summary>The largest size, M-7's <see cref="MaterialTexture.MaxSize"/>.</summary>
    public const int MaxSize = MaterialTexture.MaxSize;

    /// <summary>How far outside a triangle a texel centre may lie and still be covered by the clamp: half a texel's diagonal, in texels.</summary>
    public static readonly double ClampReach = Math.Sqrt(0.5);

    /// <summary>How many overlapping face pairs an overlap error names before it counts the rest.</summary>
    public const int NamedOverlaps = 8;

    public const string NoUvsMessage = "the mesh has no UVs, so it has nothing to bake into; give it UVs first (unwrap, project_uvs or set_uvs)";

    private readonly int[] _slot;          // per texel, row-major: index into the covered arrays, or -1
    private readonly int[] _triangle;      // per covered texel
    private readonly float[] _weights;     // per covered texel, 3 each, corners A, B, C
    private readonly bool[] _clamped;      // per covered texel
    private readonly Corners?[] _corners;  // per triangle; null for a triangle not baked
    private readonly int _clampedCount;

    private BakeLayout(PolygonMesh mesh, int size, IReadOnlyList<MeshTriangle> triangles, IReadOnlyList<int> faces,
        IReadOnlyList<int> unmapped, int[] slot, int[] triangle, float[] weights, bool[] clamped, Corners?[] corners)
    {
        Mesh = mesh;
        Size = size;
        Triangles = triangles;
        Faces = faces;
        Unmapped = unmapped;
        _slot = slot;
        _triangle = triangle;
        _weights = weights;
        _clamped = clamped;
        _corners = corners;
        _clampedCount = clamped.Count(c => c);
    }

    /// <summary>The mesh laid out: its own positions, normals and UVs are what every texel reads.</summary>
    public PolygonMesh Mesh { get; }

    /// <summary>Texels a side; the layout is square.</summary>
    public int Size { get; }

    /// <summary><see cref="Triangulator.Triangulate"/>'s triangles of <see cref="Mesh"/>, which <see cref="BakeTexel.Triangle"/> indexes.</summary>
    public IReadOnlyList<MeshTriangle> Triangles { get; }

    /// <summary>The faces baked, ascending: those asked for (every live face when none were named) that have UVs.</summary>
    public IReadOnlyList<int> Faces { get; }

    /// <summary>The faces asked for that have no UVs, ascending: skipped, never baked.</summary>
    public IReadOnlyList<int> Unmapped { get; }

    /// <summary>How many texels are covered, by centre or by the clamp.</summary>
    public int CoveredCount => _triangle.Length;

    /// <summary>How many of the covered texels were covered by the clamp.</summary>
    public int ClampedCount => _clampedCount;

    /// <summary>Lays out every live face of <paramref name="mesh"/> at <paramref name="size"/> texels a side.</summary>
    public static BakeLayoutResult Create(PolygonMesh mesh, int size) => Create(mesh, size, null);

    /// <summary>
    /// Lays out <paramref name="faces"/> of <paramref name="mesh"/> (every live face when null) at <paramref name="size"/>
    /// texels a side, a power of two from <see cref="MinSize"/> to <see cref="MaxSize"/>.
    /// </summary>
    public static BakeLayoutResult Create(PolygonMesh mesh, int size, IEnumerable<int>? faces)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (SizeProblem(size) is { } sizeProblem) return new BakeLayoutResult(null, sizeProblem, []);
        if (mesh.Uvs is not { } uvs || uvs.MappedFaces.Count == 0) return new BakeLayoutResult(null, NoUvsMessage, []);

        SortedSet<int> wanted;
        if (faces is null) wanted = [.. mesh.LiveFaces];
        else
        {
            wanted = [];
            foreach (var f in faces)
            {
                if (mesh.FaceProblem(f) is { } problem) return new BakeLayoutResult(null, $"faces[{f}]: {problem}", []);
                wanted.Add(f);
            }
        }
        var baked = wanted.Where(uvs.IsMapped).ToArray();
        var unmapped = wanted.Where(f => !uvs.IsMapped(f)).ToArray();
        var isBaked = new bool[mesh.Faces.Count];
        foreach (var f in baked) isBaked[f] = true;

        var triangles = Triangulator.Triangulate(mesh);
        var cornerNormals = Shading.Corners(mesh);
        var corners = new Corners?[triangles.Count];
        var faceNormals = new Vector3?[mesh.Faces.Count];
        for (var t = 0; t < triangles.Count; t++)
        {
            var tri = triangles[t];
            if (!isBaked[tri.Face]) continue;
            var face = mesh.Faces[tri.Face];
            var uv = uvs.Of(tri.Face)!;
            var normals = cornerNormals[tri.Face];
            int a = face.CornerOf(tri.A), b = face.CornerOf(tri.B), c = face.CornerOf(tri.C);
            corners[t] = new Corners(
                mesh.Vertices[tri.A], mesh.Vertices[tri.B], mesh.Vertices[tri.C],
                normals[a], normals[b], normals[c], uv[a], uv[b], uv[c],
                faceNormals[tri.Face] ??= mesh.FaceNormal(tri.Face));
        }

        var raster = Raster.Run(size, triangles, corners);
        if (raster.Overlaps.Count > 0)
            return new BakeLayoutResult(null, OverlapMessage(raster.Overlaps), unmapped);

        return new BakeLayoutResult(
            new BakeLayout(mesh, size, triangles, baked, unmapped, raster.Slot, raster.Triangle, raster.Weights, raster.Clamped, corners),
            null, unmapped);
    }

    /// <summary>Why <paramref name="size"/> is no layout size, or null when it is one.</summary>
    public static string? SizeProblem(int size) =>
        size >= MinSize && size <= MaxSize && (size & (size - 1)) == 0
            ? null
            : $"size must be a power of two from {MinSize} to {MaxSize}, not {size}";

    /// <summary>The overlap error for these face pairs (lower id first), in order.</summary>
    public static string OverlapMessage(IReadOnlyList<(int Lo, int Hi)> pairs)
    {
        var named = string.Join(", ", pairs.Take(NamedOverlaps).Select(p => $"faces[{p.Lo}] and faces[{p.Hi}]"));
        var rest = pairs.Count > NamedOverlaps ? $" and {pairs.Count - NamedOverlaps} more pair{(pairs.Count - NamedOverlaps == 1 ? "" : "s")}" : "";
        return $"the UVs overlap, so a texel would show two faces: {named}{rest}; move the islands apart (unwrap or set_uvs) and bake again";
    }

    /// <summary>How texel (<paramref name="x"/>, <paramref name="y"/>) is covered.</summary>
    public BakeCoverage CoverageAt(int x, int y)
    {
        var slot = _slot[Index(x, y)];
        return slot < 0 ? BakeCoverage.None : _clamped[slot] ? BakeCoverage.Clamp : BakeCoverage.Centre;
    }

    /// <summary>Whether texel (<paramref name="x"/>, <paramref name="y"/>) is covered, by centre or by the clamp.</summary>
    public bool IsCovered(int x, int y) => _slot[Index(x, y)] >= 0;

    /// <summary>The face texel (<paramref name="x"/>, <paramref name="y"/>) shows, or -1 when it is not covered.</summary>
    public int FaceAt(int x, int y)
    {
        var slot = _slot[Index(x, y)];
        return slot < 0 ? -1 : Triangles[_triangle[slot]].Face;
    }

    /// <summary>What texel (<paramref name="x"/>, <paramref name="y"/>) shows, when it is covered.</summary>
    public bool TryGetTexel(int x, int y, out BakeTexel texel)
    {
        var slot = _slot[Index(x, y)];
        if (slot < 0) { texel = default; return false; }
        texel = Make(x, y, slot);
        return true;
    }

    /// <summary>Every covered texel, row-major with the top row first.</summary>
    public IEnumerable<BakeTexel> Texels()
    {
        for (var i = 0; i < _slot.Length; i++)
            if (_slot[i] >= 0) yield return Make(i % Size, i / Size, _slot[i]);
    }

    /// <summary>A new image of zeros, this layout's size, with <paramref name="channels"/> values a texel.</summary>
    public BakeImage NewImage(int channels) => new(Size, Size, channels);

    private int Index(int x, int y)
    {
        if ((uint)x >= (uint)Size) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Size) throw new ArgumentOutOfRangeException(nameof(y));
        return y * Size + x;
    }

    private BakeTexel Make(int x, int y, int slot)
    {
        var t = _triangle[slot];
        var c = _corners[t]!.Value;
        var w = new Vector3(_weights[3 * slot], _weights[3 * slot + 1], _weights[3 * slot + 2]);
        var position = c.PA * w.X + c.PB * w.Y + c.PC * w.Z;
        var uv = c.UvA * w.X + c.UvB * w.Y + c.UvC * w.Z;
        var n = c.NA * w.X + c.NB * w.Y + c.NC * w.Z;
        var len = n.Length();
        var shading = len > 0 ? n / len : c.FaceNormal;
        var frame = TangentFrame.Of(c.PA, c.PB, c.PC, c.UvA, c.UvB, c.UvC, shading);
        return new BakeTexel(x, y, Triangles[t].Face, t, w, _clamped[slot] ? BakeCoverage.Clamp : BakeCoverage.Centre, uv,
            position, shading, c.FaceNormal, frame);
    }

    /// <summary>A baked triangle's corners A, B, C: rest positions, shading normals and uvs, and its face's normal.</summary>
    internal readonly record struct Corners(
        Vector3 PA, Vector3 PB, Vector3 PC, Vector3 NA, Vector3 NB, Vector3 NC, Vector2 UvA, Vector2 UvB, Vector2 UvC, Vector3 FaceNormal);
}

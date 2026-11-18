using System.Numerics;
using System.Text.Json.Nodes;
using Carve.Core;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Schema2;
using AttributeFormat = SharpGLTF.Memory.AttributeFormat;

namespace Carve.Server;

/// <summary>
/// A model's morph targets as glTF writes them (D-6, Morph targets; T-104): one glTF target per name across the model
/// (<see cref="Model.TargetNames"/>, the union in first-definition order), on every primitive of the one glTF mesh, as
/// dense <c>POSITION</c> and <c>NORMAL</c> accessors; no <c>TANGENT</c>. A vertex's position delta is its Carve vertex's;
/// its normal delta is its corner's normal from <see cref="Shading"/> on the mesh at weight 1, less the normal written at
/// rest. A primitive whose Carve mesh lacks a name gets zero deltas for it. <c>mesh.weights</c> are all 0 and the names
/// go in <c>mesh.extras.targetNames</c>, where the engine's <c>ParsedMorphTargets.TargetNames</c> reads them.
/// <para>
/// Each written vertex carries a tag (<see cref="Tagged{T}"/>) naming its set of deltas, so the mesh builder merges two
/// corners only when their deltas agree too: two coplanar flat faces share a normal at rest and not once bent, and two
/// coincident vertices can come apart. The tag is not written. A model without targets never comes here, so its bytes
/// are unchanged.
/// </para>
/// </summary>
internal sealed class GlbTargets
{
    private readonly List<Vector3[]> _byTag = [];
    private readonly Dictionary<DeltaKey, int> _tags = [];

    private GlbTargets(IReadOnlyList<string> names) => Names = names;

    public IReadOnlyList<string> Names { get; }

    /// <summary>The targets to write for <paramref name="names"/>, or null for none (the export as it was).</summary>
    public static GlbTargets? For(IReadOnlyList<string> names) => names.Count == 0 ? null : new GlbTargets(names);

    /// <summary>The deltas of one Carve mesh: each name's target (null where the mesh lacks it) and its corner normals at weight 1.</summary>
    public sealed class Piece
    {
        internal Piece(PolygonMesh mesh, IReadOnlyList<string> names)
        {
            Mesh = mesh;
            Targets = [.. names.Select(n => mesh.Targets?.Find(n))];
            Corners = [.. Targets.Select(t => t is null ? null : Shading.Corners(mesh.Morphed(new Dictionary<string, float> { [t.Name] = 1f })))];
        }

        public PolygonMesh Mesh { get; }

        internal MorphTarget?[] Targets { get; }

        internal Vector3[][]?[] Corners { get; }
    }

    public Piece Of(PolygonMesh mesh) => new(mesh, Names);

    /// <summary>The tag of the corner of <paramref name="face"/> at <paramref name="vertex"/>, written with <paramref name="normal"/> at rest.</summary>
    public int Tag(Piece piece, int face, int vertex, Vector3 normal)
    {
        var n = Names.Count;
        var deltas = new Vector3[2 * n];
        var corner = piece.Mesh.Faces[face].CornerOf(vertex);
        for (var i = 0; i < n; i++)
        {
            if (piece.Targets[i] is not { } target) continue;
            deltas[i] = target.DeltaOf(vertex);
            deltas[n + i] = piece.Corners[i]![face][corner] - normal;
        }

        var key = new DeltaKey(deltas);
        if (!_tags.TryGetValue(key, out var tag))
        {
            _tags[key] = tag = _byTag.Count;
            _byTag.Add(deltas);
        }
        return tag;
    }

    /// <summary>
    /// Writes the targets on <paramref name="mesh"/>, whose primitives are <paramref name="tags"/>' in order: per primitive,
    /// each vertex's tag in the builder's vertex order, which is the written order (checked against <c>POSITION</c>'s count).
    /// </summary>
    public void Write(ModelRoot root, Mesh mesh, IReadOnlyList<IReadOnlyList<int>> tags)
    {
        var primitives = mesh.Primitives;
        if (primitives.Count != tags.Count)
            throw new InvalidOperationException($"the glTF mesh has {primitives.Count} primitives and the builder {tags.Count}");
        var n = Names.Count;
        for (var p = 0; p < primitives.Count; p++)
        {
            var primitive = primitives[p];
            var vertexTags = tags[p];
            if (primitive.GetVertexAccessor("POSITION").Count != vertexTags.Count)
                throw new InvalidOperationException($"primitive {p} has {primitive.GetVertexAccessor("POSITION").Count} vertices and the builder {vertexTags.Count}");
            for (var t = 0; t < n; t++)
            {
                var positions = new Vector3[vertexTags.Count];
                var normals = new Vector3[vertexTags.Count];
                for (var v = 0; v < vertexTags.Count; v++)
                {
                    var deltas = _byTag[vertexTags[v]];
                    positions[v] = deltas[t];
                    normals[v] = deltas[n + t];
                }
                primitive.SetMorphTargetAccessors(t, new Dictionary<string, Accessor>
                {
                    ["POSITION"] = Accessor(root, positions),
                    ["NORMAL"] = Accessor(root, normals),
                });
            }
        }

        mesh.SetMorphWeights(new float[n]);
        var names = new JsonArray();
        foreach (var name in Names) names.Add(name);
        mesh.Extras = new JsonObject { ["targetNames"] = names };
    }

    /// <summary>Each primitive's vertex tags in the builder's order, the primitives with no triangle left out, as the glTF mesh leaves them out.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> TagsOf<TvG, TvM, TvS>(IEnumerable<SharpGLTF.Geometry.PrimitiveBuilder<SharpGLTF.Materials.MaterialBuilder, TvG, TvM, TvS>> primitives)
        where TvG : struct, IVertexGeometry
        where TvM : struct, IVertexMaterial
        where TvS : struct, IVertexSkinning =>
        [.. primitives.Where(p => p.Triangles.Count > 0)
            .Select(p => (IReadOnlyList<int>)[.. p.Vertices.Select(v => ((ITaggedVertex)v.Material).Tag)])];

    private static Accessor Accessor(ModelRoot root, Vector3[] values)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        var view = root.UseBufferView(bytes, 0, null, 0, BufferMode.ARRAY_BUFFER);
        var accessor = root.CreateAccessor();
        accessor.SetVertexData(view, 0, values.Length, AttributeFormat.Float3);
        return accessor;
    }

    /// <summary>A set of deltas compared by value, so equal deltas share a tag.</summary>
    private sealed class DeltaKey(Vector3[] deltas) : IEquatable<DeltaKey>
    {
        private readonly Vector3[] _deltas = deltas;
        private readonly int _hash = Hash(deltas);

        private static int Hash(Vector3[] deltas)
        {
            var hash = new HashCode();
            foreach (var d in deltas) hash.Add(d);
            return hash.ToHashCode();
        }

        public bool Equals(DeltaKey? other) => other is not null && _deltas.AsSpan().SequenceEqual(other._deltas);

        public override bool Equals(object? obj) => Equals(obj as DeltaKey);

        public override int GetHashCode() => _hash;
    }
}

/// <summary>A vertex's tag on a written vertex, so a reader of the builder's vertices finds each one's deltas.</summary>
internal interface ITaggedVertex
{
    int Tag { get; }
}

/// <summary>
/// <typeparamref name="T"/>'s attributes, written as they are, and a tag that is never written but takes part in equality,
/// so the mesh builder keeps apart two vertices whose deltas differ (see <see cref="GlbTargets"/>).
/// </summary>
internal readonly struct Tagged<T>(T inner, int tag) : IVertexMaterial, ITaggedVertex, IEquatable<Tagged<T>>
    where T : struct, IVertexMaterial
{
    public T Inner { get; } = inner;

    public int Tag { get; } = tag;

    public int MaxColors => Inner.MaxColors;

    public int MaxTextCoords => Inner.MaxTextCoords;

    public Vector4 GetColor(int index) => Inner.GetColor(index);

    public Vector2 GetTexCoord(int index) => Inner.GetTexCoord(index);

    public void SetColor(int setIndex, Vector4 color) => throw new NotSupportedException();

    public void SetTexCoord(int setIndex, Vector2 coord) => throw new NotSupportedException();

    public VertexMaterialDelta Subtract(IVertexMaterial baseValue) => Inner.Subtract(baseValue);

    public void Add(in VertexMaterialDelta delta) => throw new NotSupportedException();

    public IEnumerable<KeyValuePair<string, AttributeFormat>> GetEncodingAttributes() => Inner.GetEncodingAttributes();

    public bool Equals(Tagged<T> other) => Tag == other.Tag && Inner.Equals(other.Inner);

    public override bool Equals(object? obj) => obj is Tagged<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Inner, Tag);
}

using System.Collections.Immutable;
using System.Numerics;
using Carve.Core;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server;

/// <summary>How <c>render</c> draws the mesh.</summary>
public enum Look
{
    Shaded,
    Wireframe,
    Normals,
}

/// <summary>
/// What a render picks out, by id: the shape <c>select</c> returns, as sets, each pair lowest id first.
/// Compared by value, so that an equal highlight is not a change of view.
/// </summary>
public sealed class Highlight : IEquatable<Highlight>
{
    public static readonly Highlight None = Of([], [], []);

    private Highlight(ImmutableSortedSet<int> vertices, ImmutableSortedSet<(int A, int B)> edges, ImmutableSortedSet<int> faces)
    {
        Vertices = vertices;
        Edges = edges;
        Faces = faces;
    }

    public ImmutableSortedSet<int> Vertices { get; }

    public ImmutableSortedSet<(int A, int B)> Edges { get; }

    public ImmutableSortedSet<int> Faces { get; }

    public bool IsEmpty => Vertices.IsEmpty && Edges.IsEmpty && Faces.IsEmpty;

    public static Highlight Of(IEnumerable<int> vertices, IEnumerable<(int A, int B)> edges, IEnumerable<int> faces) =>
        new(vertices.ToImmutableSortedSet(), edges.Select(e => (Math.Min(e.A, e.B), Math.Max(e.A, e.B))).ToImmutableSortedSet(), faces.ToImmutableSortedSet());

    /// <summary>What this names that the mesh does not have; empty when it fits the mesh.</summary>
    public IReadOnlyList<MeshError> Unknown(PolygonMesh mesh) => MeshEdits.UnknownElements(mesh, Vertices, Edges, Faces);

    /// <summary>The caption's words: <c>3 faces, 2 edges, 1 vertex</c>, the non-zero counts only.</summary>
    public string Counts()
    {
        var parts = new List<string>();
        if (Faces.Count > 0) parts.Add(Faces.Count == 1 ? "1 face" : $"{Faces.Count} faces");
        if (Edges.Count > 0) parts.Add(Edges.Count == 1 ? "1 edge" : $"{Edges.Count} edges");
        if (Vertices.Count > 0) parts.Add(Vertices.Count == 1 ? "1 vertex" : $"{Vertices.Count} vertices");
        return string.Join(", ", parts);
    }

    public bool Equals(Highlight? other) =>
        other is not null && Vertices.SequenceEqual(other.Vertices) && Edges.SequenceEqual(other.Edges) && Faces.SequenceEqual(other.Faces);

    public override bool Equals(object? obj) => Equals(obj as Highlight);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var v in Vertices) hash.Add(v);
        foreach (var e in Edges) hash.Add(e);
        foreach (var f in Faces) hash.Add(f);
        return hash.ToHashCode();
    }

    public static bool operator ==(Highlight? a, Highlight? b) => a?.Equals(b) ?? b is null;

    public static bool operator !=(Highlight? a, Highlight? b) => !(a == b);
}

/// <summary>One look of a mesh, ready for the engine. The faces are always <see cref="MeshBridge"/>'s triangles.</summary>
/// <param name="FlatColour">The unlit material's colour, or null for the lit surface.</param>
/// <param name="VertexColours">One per vertex of the triangles, or null for a mesh carrying none.</param>
/// <param name="EdgeLines">The lines in <see cref="LookMeshes.Edge"/>, or null for none.</param>
/// <param name="OrangeLines">The lines in <see cref="LookMeshes.Orange"/>, or null for none.</param>
public sealed record LookScene(
    Vector4? FlatColour,
    Rgba8[]? VertexColours,
    MeshData<PositionVertex>? EdgeLines,
    MeshData<PositionVertex>? OrangeLines);

/// <summary>
/// Pure CPU: what the looks of <c>render</c> draw, all in drawn space. Nothing here touches a device,
/// so the lines and colours are tested without one.
/// </summary>
public static class LookMeshes
{
    /// <summary>The wireframe's face fill, linear.</summary>
    public static readonly Vector4 Fill = new(0.09f, 0.095f, 0.10f, 1);

    /// <summary>Edges and spikes, linear.</summary>
    public static readonly Vector4 Edge = new(0.85f, 0.85f, 0.85f, 1);

    /// <summary>Everything highlighted, linear.</summary>
    public static readonly Vector4 Orange = new(1, 0.55f, 0.1f, 1);

    /// <summary>
    /// How far an edge stands off its faces, along the mean of their normals, so that it wins the depth test.
    /// Decided by the person over a scale about the drawn origin, which pushes an edge in a concave corner into the wall.
    /// </summary>
    public const float LineLift = 0.004f;

    public const float SpikeLength = 0.1f;

    public const float StarHalfLength = 0.02f;

    private static readonly Rgba8 FillVertex = Rgba8.FromVector(new Vector3(Fill.X, Fill.Y, Fill.Z));

    private static readonly Rgba8 OrangeVertex = Rgba8.FromVector(new Vector3(Orange.X, Orange.Y, Orange.Z));

    /// <summary>
    /// With no face highlighted, a look that needs no vertex colours carries none: the shaded look is then
    /// the mesh as it always was, and the fill is exactly <see cref="Fill"/>, not its nearest eight bits.
    /// </summary>
    public static LookScene Compose(PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight)
    {
        var orange = OrangeLines(mesh, space, look, highlight);
        var picked = highlight.Faces.Any(f => f >= 0 && f < mesh.Faces.Count);
        Rgba8[] Picked(Rgba8 others) => FaceColours(mesh, f => highlight.Faces.Contains(f) ? OrangeVertex : others);
        return look switch
        {
            Look.Wireframe => new LookScene(picked ? Vector4.One : Fill, picked ? Picked(FillVertex) : null, WireframeLines(mesh, space, highlight), orange),
            Look.Normals => new LookScene(Vector4.One, FaceColours(mesh, f => NormalColour(mesh.FaceNormal(f))), Spikes(mesh, space), orange),
            _ => new LookScene(null, picked ? Picked(Rgba8.White) : null, null, orange),
        };
    }

    /// <summary>A face's colour in the normals look: <c>(n + 1) / 2</c>, in linear RGB.</summary>
    public static Rgba8 NormalColour(Vector3 normal) => Rgba8.FromVector((normal + Vector3.One) / 2);

    /// <summary>One colour per vertex of <see cref="MeshBridge"/>'s triangles, each its face's.</summary>
    private static Rgba8[] FaceColours(PolygonMesh mesh, Func<int, Rgba8> colour)
    {
        var triangles = FlatShading.Triangles(mesh);
        var colours = new Rgba8[triangles.Count * 3];
        for (var t = 0; t < triangles.Count; t++)
        {
            colours[t * 3] = colours[t * 3 + 1] = colours[t * 3 + 2] = colour(triangles[t].Face);
        }

        return colours;
    }

    /// <summary>Every edge but those highlighted, which are drawn in orange at the same place and would tie with it.</summary>
    public static MeshData<PositionVertex>? WireframeLines(PolygonMesh mesh, DrawnSpace space, Highlight highlight) =>
        Lines(mesh.Topology.Edges.Where(e => !highlight.Edges.Contains((e.Lo, e.Hi))).SelectMany(e => Lifted(mesh, space, e)));

    /// <summary>A segment per face: from its centre along its normal.</summary>
    public static MeshData<PositionVertex>? Spikes(PolygonMesh mesh, DrawnSpace space) =>
        Lines(Enumerable.Range(0, mesh.Faces.Count).SelectMany(f =>
        {
            var centre = space.Apply(mesh.FaceCentre(f));
            return new[] { centre, centre + mesh.FaceNormal(f) * SpikeLength };
        }));

    /// <summary>
    /// The highlighted edges, lifted as the wireframe's, in normals the loops of the highlighted faces too,
    /// and a star of three axis-aligned segments through each highlighted vertex. An id the mesh lacks draws
    /// nothing: a stale highlight can meet a new mesh here, though no render of it is ever answered.
    /// </summary>
    public static MeshData<PositionVertex>? OrangeLines(PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight)
    {
        var loops = look == Look.Normals
            ? highlight.Faces.Where(f => f >= 0 && f < mesh.Faces.Count).SelectMany(f => mesh.Topology.FaceEdges(f)).Select(e => (e.Lo, e.Hi))
            : [];
        var wanted = highlight.Edges.Union(loops);
        var edges = mesh.Topology.Edges.Where(e => wanted.Contains((e.Lo, e.Hi))).SelectMany(e => Lifted(mesh, space, e));
        var stars = highlight.Vertices.Where(v => v >= 0 && v < mesh.Vertices.Count).SelectMany(v =>
        {
            var at = space.Apply(mesh.Vertices[v]);
            return new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ }.SelectMany(axis => new[] { at - axis * StarHalfLength, at + axis * StarHalfLength });
        });
        return Lines(edges.Concat(stars));
    }

    /// <summary>An edge's ends, moved off its faces. Faces back to back have no mean, and such an edge stays where it is.</summary>
    private static Vector3[] Lifted(PolygonMesh mesh, DrawnSpace space, MeshEdge edge)
    {
        var mean = edge.Faces.Aggregate(Vector3.Zero, (sum, use) => sum + mesh.FaceNormal(use.Face));
        var lift = mean.Length() < 1e-6f ? Vector3.Zero : Vector3.Normalize(mean) * LineLift;
        return [space.Apply(mesh.Vertices[edge.Lo]) + lift, space.Apply(mesh.Vertices[edge.Hi]) + lift];
    }

    private static MeshData<PositionVertex>? Lines(IEnumerable<Vector3> ends)
    {
        var vertices = ends.Select(p => new PositionVertex { Position = p }).ToArray();
        if (vertices.Length == 0)
        {
            return null;
        }

        var indices = Enumerable.Range(0, vertices.Length).Select(i => (uint)i).ToArray();
        return new MeshData<PositionVertex>(vertices, indices) { Topology = Empyrean.MeshTopology.LineList };
    }
}

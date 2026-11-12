namespace Carve.Core;

/// <summary>
/// One face's use of an edge: the face's id, the loop its side lies on (<see cref="Hole"/>: -1 for the outer loop, else
/// the hole's index in <see cref="Face.Holes"/>), the corner of that loop the side starts at (the side runs from
/// <c>Faces[Face].LoopAt(Hole)[Corner]</c> to the next corner of the same loop), and which way it runs. A hole winds
/// opposite to its outer loop, so a hole edge shared with a face filling the hole is crossed once each way.
/// </summary>
public readonly record struct EdgeUse(int Face, int Hole, int Corner, bool LoToHi)
{
    /// <summary>The element name of the corner the side starts at: <c>faces[n][i]</c>, or <c>faces[n].holes[k][i]</c> on a hole.</summary>
    public string CornerName => Carve.Core.Face.CornerName(Face, Hole, Corner);
}

/// <summary>
/// An undirected edge of a <see cref="PolygonMesh"/>: the vertex pair <c>(Lo, Hi)</c> with <c>Lo &lt; Hi</c>,
/// and every face that uses it. An edge has no id of its own; it is named by its two vertex ids, as <see cref="Name"/>.
/// </summary>
public sealed class MeshEdge
{
    internal MeshEdge(int lo, int hi, EdgeUse[] faces)
    {
        Lo = lo;
        Hi = hi;
        Faces = Array.AsReadOnly(faces);
    }

    public int Lo { get; }
    public int Hi { get; }

    /// <summary>The faces on this edge, by face id ascending. One for an open edge, two for a manifold one, or more.</summary>
    public IReadOnlyList<EdgeUse> Faces { get; }

    /// <summary>Exactly two faces, crossing the edge in opposite directions.</summary>
    public bool IsManifold => Faces.Count == 2 && Faces[0].LoToHi != Faces[1].LoToHi;

    /// <summary>The element name warnings use, <c>edges[lo-hi]</c>.</summary>
    public string Name => $"edges[{Lo}-{Hi}]";

    /// <summary>The vertex at the other end from <paramref name="vertex"/>, which must be <see cref="Lo"/> or <see cref="Hi"/>.</summary>
    public int Other(int vertex) => vertex == Lo ? Hi : vertex == Hi ? Lo
        : throw new ArgumentException($"vertex {vertex} is not on {Name}", nameof(vertex));
}

/// <summary>
/// How one <see cref="PolygonMesh"/>'s faces join: its edges, each vertex's faces and each face's edges.
/// Derived from the mesh's face loops, immutable, built once per mesh and reached only through
/// <see cref="PolygonMesh.Topology"/>. Open, non-manifold and mixed-winding meshes are all described as they are.
/// </summary>
public sealed class MeshTopology
{
    private readonly Dictionary<(int Lo, int Hi), MeshEdge> _byPair;
    private readonly IReadOnlyList<int>[] _vertexFaces;
    private readonly IReadOnlyList<MeshEdge>[] _faceEdges;

    internal MeshTopology(PolygonMesh mesh)
    {
        var faces = mesh.Faces;
        var uses = new Dictionary<(int, int), List<EdgeUse>>();
        var vertexFaces = new List<int>?[mesh.Vertices.Count];

        for (var f = 0; f < faces.Count; f++)
        {
            var face = faces[f];
            for (var h = -1; h < face.Holes.Count; h++)
            {
                var ring = face.LoopAt(h);
                for (var k = 0; k < ring.Count; k++)
                {
                    var a = ring[k];
                    var b = ring[(k + 1) % ring.Count];
                    var key = a < b ? (a, b) : (b, a);
                    if (!uses.TryGetValue(key, out var list))
                        uses[key] = list = [];
                    list.Add(new EdgeUse(f, h, k, a < b));

                    // A face never repeats a vertex across its loops (Create refuses it), so one add per face per vertex.
                    (vertexFaces[a] ??= []).Add(f);
                }
            }
        }

        _byPair = new Dictionary<(int, int), MeshEdge>(uses.Count);
        var edges = new MeshEdge[uses.Count];
        var e = 0;
        foreach (var (key, list) in uses.OrderBy(e => e.Key))
            _byPair[key] = edges[e++] = new MeshEdge(key.Item1, key.Item2, list.ToArray());
        Edges = Array.AsReadOnly(edges);

        _vertexFaces = vertexFaces.Select(l => (IReadOnlyList<int>)Array.AsReadOnly(l?.ToArray() ?? [])).ToArray();

        _faceEdges = new IReadOnlyList<MeshEdge>[faces.Count];
        for (var f = 0; f < faces.Count; f++)
        {
            var face = faces[f];
            var loop = new MeshEdge[face.Corners.Count];
            var n = 0;
            for (var h = -1; h < face.Holes.Count; h++)
            {
                var ring = face.LoopAt(h);
                for (var k = 0; k < ring.Count; k++)
                {
                    var a = ring[k];
                    var b = ring[(k + 1) % ring.Count];
                    loop[n++] = _byPair[a < b ? (a, b) : (b, a)];
                }
            }
            _faceEdges[f] = Array.AsReadOnly(loop);
        }
    }

    /// <summary>Every distinct undirected edge, ordered by <c>(Lo, Hi)</c>. A position here is not an id and may change across an edit.</summary>
    public IReadOnlyList<MeshEdge> Edges { get; }

    /// <summary>The faces that use a vertex, by face id ascending; empty for an unused vertex.</summary>
    public IReadOnlyList<int> VertexFaces(int vertex) => _vertexFaces[vertex];

    /// <summary>
    /// A face's edges, outer loop first and then each hole in order, by corner (<see cref="Face.Corners"/>): edge <c>c</c>
    /// runs from corner <c>c</c> to the next corner of the same loop (wrapping within the loop).
    /// </summary>
    public IReadOnlyList<MeshEdge> FaceEdges(int face) => _faceEdges[face];

    /// <summary>The edge between two vertices, given in either order, if any face uses it.</summary>
    public bool TryGetEdge(int a, int b, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MeshEdge? edge)
        => _byPair.TryGetValue(a < b ? (a, b) : (b, a), out edge);

    /// <summary>Whether the edge between two vertices (either order) exists and has exactly two faces crossing it in opposite directions.</summary>
    public bool IsManifoldEdge(int a, int b) => TryGetEdge(a, b, out var edge) && edge.IsManifold;
}

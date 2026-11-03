using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class MeshTopologyTests
{
    private static readonly Vector3[] Quad = [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)];

    private static MeshEdge Edge(PolygonMesh mesh, int a, int b)
    {
        Assert.True(mesh.Topology.TryGetEdge(a, b, out var edge), $"no edge {a}-{b}");
        return edge;
    }

    [Fact]
    public void UnitCubeHasTwelveManifoldEdges()
    {
        var cube = TestMeshes.Cube();
        var edges = cube.Topology.Edges;
        Assert.Equal(12, edges.Count);
        foreach (var edge in edges)
        {
            Assert.True(edge.Lo < edge.Hi);
            Assert.Equal(2, edge.Faces.Count);
            Assert.NotEqual(edge.Faces[0].LoToHi, edge.Faces[1].LoToHi);
            Assert.True(edge.IsManifold);
            Assert.True(cube.Topology.IsManifoldEdge(edge.Hi, edge.Lo));
        }

        // Each use points back at the face's own side along the edge.
        foreach (var edge in edges)
        foreach (var use in edge.Faces)
        {
            var loop = cube.Faces[use.Face];
            var from = loop[use.Corner];
            var to = loop[(use.Corner + 1) % loop.Count];
            Assert.Equal(use.LoToHi ? (edge.Lo, edge.Hi) : (edge.Hi, edge.Lo), (from, to));
        }

        Assert.Equal([new EdgeUse(0, 3, false), new EdgeUse(2, 0, true)], Edge(cube, 1, 0).Faces);
    }

    [Fact]
    public void EdgesAreOrderedByVertexPair()
    {
        var edges = TestMeshes.Cube().Topology.Edges;
        Assert.Equal(edges.OrderBy(e => (e.Lo, e.Hi)).Select(e => e.Name), edges.Select(e => e.Name));
    }

    [Fact]
    public void TryGetEdgeTakesEitherOrderAndMissesNonEdges()
    {
        var cube = TestMeshes.Cube();
        Assert.Same(Edge(cube, 2, 6), Edge(cube, 6, 2));
        Assert.Equal("edges[2-6]", Edge(cube, 6, 2).Name);
        Assert.False(cube.Topology.TryGetEdge(0, 6, out _)); // a diagonal, not an edge
        Assert.False(cube.Topology.IsManifoldEdge(0, 6));
    }

    [Fact]
    public void OpenQuadHasFourOneFaceEdges()
    {
        var quad = TestMeshes.Make(Quad, [[0, 1, 2, 3]]);
        var edges = quad.Topology.Edges;
        Assert.Equal(4, edges.Count);
        Assert.All(edges, e => Assert.Single(e.Faces));
        Assert.All(edges, e => Assert.False(e.IsManifold));
        Assert.Equal(["edges[0-1]", "edges[0-3]", "edges[1-2]", "edges[2-3]"], edges.Select(e => e.Name));
    }

    [Fact]
    public void ThreeFacesOnOneEdgeAreAllListed()
    {
        var v = new Vector3[] { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1) };
        var mesh = TestMeshes.Make(v, [[0, 1, 2], [1, 0, 3], [0, 1, 4]]);
        var edge = Edge(mesh, 0, 1);
        Assert.Equal([new EdgeUse(0, 0, true), new EdgeUse(1, 0, false), new EdgeUse(2, 0, true)], edge.Faces);
        Assert.False(edge.IsManifold);
    }

    [Fact]
    public void FlippedFaceShowsTheSameDirectionTwice()
    {
        var faces = TestMeshes.CubeFaces.ToArray();
        faces[0] = [.. faces[0].Reverse()];
        var mesh = TestMeshes.Make(TestMeshes.CubeVertices, faces);
        foreach (var edge in mesh.Topology.FaceEdges(0))
        {
            Assert.Equal(2, edge.Faces.Count);
            Assert.Equal(edge.Faces[0].LoToHi, edge.Faces[1].LoToHi);
            Assert.False(edge.IsManifold);
        }
        // The flipped face's neighbours across other edges are untouched.
        Assert.True(mesh.Topology.IsManifoldEdge(4, 5));
        Assert.Equal(4, mesh.Topology.Edges.Count(e => !e.IsManifold));
    }

    [Fact]
    public void LBlockVertexFaces()
    {
        var l = TestMeshes.LBlock();
        Assert.Equal([0, 2, 7], l.Topology.VertexFaces(0));
        Assert.Equal([0, 4, 5], l.Topology.VertexFaces(3)); // the notch corner
        Assert.Equal([1, 4, 5], l.Topology.VertexFaces(9));
        Assert.Equal([1, 6, 7], l.Topology.VertexFaces(11));
        Assert.All(Enumerable.Range(0, 12), i => Assert.Equal(3, l.Topology.VertexFaces(i).Count));
    }

    [Fact]
    public void LBlockFaceEdgesInLoopOrder()
    {
        var l = TestMeshes.LBlock();
        Assert.Equal(18, l.Topology.Edges.Count);
        Assert.All(l.Topology.Edges, e => Assert.True(e.IsManifold));
        Assert.Equal(["edges[4-5]", "edges[3-4]", "edges[2-3]", "edges[1-2]", "edges[0-1]", "edges[0-5]"],
            l.Topology.FaceEdges(0).Select(e => e.Name));
        Assert.Equal(["edges[2-3]", "edges[3-9]", "edges[8-9]", "edges[2-8]"],
            l.Topology.FaceEdges(4).Select(e => e.Name));
    }

    [Fact]
    public void UnusedVertexHasNoFaces()
    {
        var mesh = TestMeshes.Make([.. Quad, new(5, 5, 5)], [[0, 1, 2, 3]]);
        Assert.Empty(mesh.Topology.VertexFaces(4));
    }

    [Fact]
    public void TopologyIsBuiltOncePerMesh()
    {
        var cube = TestMeshes.Cube();
        Assert.Same(cube.Topology, cube.Topology);
    }

    [Fact]
    public void EditKeepsOldIdsAndAppendsAtTheEnd()
    {
        var cube = TestMeshes.Cube();
        var result = cube.Edit(
            movedVertices: [new(3, new Vector3(0, 1.5f, 0))],
            reshapedFaces: [new(1, [4, 5, 8])],
            addedVertices: [new(0.5f, 0.5f, 2)],
            addedFaces: [[5, 6, 8], [6, 7, 8], [7, 4, 8]]);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var edited = result.Mesh!;

        Assert.Equal(9, edited.Vertices.Count);
        for (var i = 0; i < 8; i++)
            Assert.Equal(i == 3 ? new Vector3(0, 1.5f, 0) : TestMeshes.CubeVertices[i], edited.Vertices[i]);
        Assert.Equal(new Vector3(0.5f, 0.5f, 2), edited.Vertices[8]);

        Assert.Equal(9, edited.Faces.Count);
        for (var f = 0; f < 6; f++)
            Assert.Equal(f == 1 ? [4, 5, 8] : TestMeshes.CubeFaces[f], edited.Faces[f]);
        Assert.Equal([5, 6, 8], edited.Faces[6]);
        Assert.Equal([6, 7, 8], edited.Faces[7]);
        Assert.Equal([7, 4, 8], edited.Faces[8]);

        // The old mesh is untouched, and the new one has its own edge table.
        Assert.Equal(TestMeshes.CubeVertices, cube.Vertices);
        Assert.Equal(12, cube.Topology.Edges.Count);
        Assert.Equal([1, 6, 7, 8], edited.Topology.VertexFaces(8));
    }

    [Fact]
    public void EditWithNothingChangedKeepsTheMesh()
    {
        var cube = TestMeshes.Cube();
        var edited = cube.Edit().Mesh!;
        Assert.Equal(cube.Vertices, edited.Vertices);
        Assert.Equal(cube.Faces, edited.Faces);
    }

    [Fact]
    public void EditRefusesWhatCreateWouldRefuseByElement()
    {
        var cube = TestMeshes.Cube();
        var result = cube.Edit(
            movedVertices: [new(3, new Vector3(float.NaN, 0, 0))],
            reshapedFaces: [new(2, [0, 1, 1])],
            addedFaces: [[0, 9, 1]]);
        Assert.False(result.Succeeded);
        Assert.Null(result.Mesh);

        var verts = TestMeshes.CubeVertices.ToArray();
        verts[3] = new Vector3(float.NaN, 0, 0);
        var faces = TestMeshes.CubeFaces.ToList();
        faces[2] = [0, 1, 1];
        faces.Add([0, 9, 1]);
        var create = PolygonMesh.Create(verts, faces);

        Assert.Equal(create.Errors, result.Errors);
        Assert.Contains(result.Errors, e => e.Element == "vertices[3]");
        Assert.Contains(result.Errors, e => e.Element == "faces[2][2]");
        Assert.Contains(result.Errors, e => e.Element == "faces[6][1]");
    }

    [Fact]
    public void EditRefusesAnIdTheMeshDoesNotHave()
    {
        var result = TestMeshes.Cube().Edit(
            movedVertices: [new(8, Vector3.Zero)],
            reshapedFaces: [new(-1, [0, 1, 2])]);
        Assert.False(result.Succeeded);
        Assert.Equal(["vertices[8]", "faces[-1]"], result.Errors.Select(e => e.Element));
    }
}

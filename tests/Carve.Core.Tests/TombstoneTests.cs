using System.Numerics;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>T-64's Core: tombstones in <see cref="PolygonMesh"/>, <see cref="MeshDelete"/> and <see cref="EditResult.Deleted"/>.</summary>
[Trait("Tier", "unit")]
public class TombstoneTests
{
    // TestMeshes.Cube(): face 1 is +z [4, 5, 6, 7], the top as these tests call it; vertex 6 is (1, 1, 1), on faces 1 (+z), 3 (+y) and 5 (+x).
    private const int Top = 1;
    private const int Corner = 6;

    private static EditResult Delete(PolygonMesh mesh, int[]? vertices = null, int[]? faces = null)
        => MeshDelete.Delete(mesh, vertices, faces);

    [Fact]
    public void DeletingAFaceLeavesItsSlotAndKeepsVerticesOtherFacesUse()
    {
        var cube = TestMeshes.Cube();
        var r = Delete(cube, faces: [Top]);
        var m = TestMeshes.Ok(r);

        Assert.Equal([Top], r.Deleted.Faces);
        Assert.Empty(r.Deleted.Vertices);
        Assert.Equal(6, m.Faces.Count);
        Assert.Equal(5, m.LiveFaceCount);
        Assert.Equal(8, m.LiveVertexCount);
        Assert.True(m.IsFaceDeleted(Top));
        Assert.Same(Face.Deleted, m.Faces[Top]);
        Assert.Equal([0, 2, 3, 4, 5], m.LiveFaces);
        Assert.True(m.HasTombstones);
        // The other faces keep their ids and loops.
        foreach (var f in m.LiveFaces) Assert.Equal(cube.Faces[f], m.Faces[f]);
        Assert.False(cube.IsFaceDeleted(Top));
    }

    [Fact]
    public void DeletingAVertexTakesEveryFaceOnItAndTheVerticesLeftWithNoFace()
    {
        var r = Delete(TestMeshes.Cube(), vertices: [Corner]);
        var m = TestMeshes.Ok(r);

        Assert.Equal([1, 3, 5], r.Deleted.Faces);
        Assert.Equal([Corner], r.Deleted.Vertices);
        Assert.Equal(8, m.Vertices.Count);
        Assert.Equal(7, m.LiveVertexCount);
        Assert.Equal(3, m.LiveFaceCount);
        Assert.True(m.IsVertexDeleted(Corner));
        Assert.Equal(Vector3.Zero, m.Vertices[Corner]);
        Assert.Equal([0, 1, 2, 3, 4, 5, 7], m.LiveVertices);
    }

    [Fact]
    public void DeletingFacesTakesOnlyTheirVerticesNoLiveFaceUsesAfterwards()
    {
        // Faces 1, 3 and 5 meet only at vertex 6; every other corner of theirs is on a face that stays.
        var around = Delete(TestMeshes.Cube(), faces: [1, 3, 5]);
        Assert.Equal([Corner], around.Deleted.Vertices);

        // A vertex no face used before the delete is not taken.
        var withStray = TestMeshes.Make([.. TestMeshes.CubeVertices, new Vector3(5, 5, 5)], TestMeshes.CubeFaces);
        var r = Delete(withStray, faces: [Top]);
        Assert.Empty(r.Deleted.Vertices);
        Assert.False(TestMeshes.Ok(r).IsVertexDeleted(8));
    }

    private static PolygonMesh HoledPlateWithBack()
    {
        Vector3[] v =
        [
            new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0),
            new(-0.5f, -0.5f, 0), new(0.5f, -0.5f, 0), new(0.5f, 0.5f, 0), new(-0.5f, 0.5f, 0),
        ];
        Face[] f =
        [
            new([0, 1, 2, 3], [[4, 7, 6, 5]]),
            new([4, 5, 6, 7]),
            new([3, 2, 1, 0]),
        ];
        var made = PolygonMesh.Create(v, f);
        Assert.True(made.Succeeded, string.Join("; ", made.Errors));
        return made.Mesh!;
    }

    [Fact]
    public void AFaceWhoseHoleLosesAVertexIsDeletedWhole()
    {
        var r = Delete(HoledPlateWithBack(), vertices: [4]);
        var m = TestMeshes.Ok(r);
        Assert.Equal([0, 1], r.Deleted.Faces);
        Assert.Equal([4, 5, 6, 7], r.Deleted.Vertices);
        Assert.Equal(new Face([3, 2, 1, 0]), m.Faces[2]);
    }

    [Fact]
    public void IdsAfterADeleteThenAnExtrudeAreAppendedAndNoneIsReused()
    {
        var deleted = TestMeshes.Ok(Delete(TestMeshes.Cube(), vertices: [Corner]));
        var r = MeshEdits.Extrude(deleted, [0], 1);
        var m = TestMeshes.Ok(r);

        Assert.Equal([8, 9, 10, 11], r.NewVertices);
        Assert.Equal([6, 7, 8, 9], r.NewFaces);
        Assert.Equal(12, m.Vertices.Count);
        Assert.Equal(11, m.LiveVertexCount);
        Assert.True(m.IsVertexDeleted(Corner));
        Assert.All(new[] { 1, 3, 5 }, f => Assert.True(m.IsFaceDeleted(f)));
        Assert.Equal(7, m.LiveFaceCount);
    }

    [Fact]
    public void AnEditNamingADeletedIdSaysItWasDeletedDistinctFromOneThatNeverExisted()
    {
        var m = TestMeshes.Ok(Delete(TestMeshes.Cube(), vertices: [Corner]));

        var move = MeshEdits.Move(m, [Corner], null, null, new Vector3(0, 0, 1));
        Assert.Equal(["vertices[6]: deleted"], move.Errors.Select(e => e.ToString()));

        var moveFace = MeshEdits.Move(m, null, null, [Top], new Vector3(0, 0, 1));
        Assert.Equal(["faces[1]: deleted"], moveFace.Errors.Select(e => e.ToString()));

        var extrude = MeshEdits.Extrude(m, [Top], 1);
        Assert.Equal(["faces[1]: deleted"], extrude.Errors.Select(e => e.ToString()));

        var again = Delete(m, vertices: [Corner], faces: [3]);
        Assert.Equal(["vertices[6]: deleted", "faces[3]: deleted"], again.Errors.Select(e => e.ToString()));

        var never = Delete(m, faces: [99]);
        Assert.Equal(["faces[99]: the 1st listed face: there is no face 99: ids run 0..5"], never.Errors.Select(e => e.ToString()));

        Assert.Equal("deleted", m.FaceProblem(Top));
        Assert.Equal("deleted", m.VertexProblem(Corner));
        Assert.Equal("there is no vertex 8: ids run 0..7", m.VertexProblem(8));
        Assert.Null(m.FaceProblem(0));

        var reshaped = m.Edit(reshapedFaces: [KeyValuePair.Create(Top, new Face([4, 5, 6, 7]))]);
        Assert.Contains(reshaped.Errors, e => e.ToString() == "faces[1]: deleted");
    }

    [Fact]
    public void DeletingNothingOrEveryFaceIsRefused()
    {
        var cube = TestMeshes.Cube();
        Assert.Equal([$"vertices, faces: {MeshDelete.NothingMessage}"], Delete(cube).Errors.Select(e => e.ToString()));
        Assert.Equal([$"vertices, faces: {MeshDelete.EveryFaceMessage}"], Delete(cube, faces: [0, 1, 2, 3, 4, 5]).Errors.Select(e => e.ToString()));
        // Vertices 0 and 6 are opposite corners: between them they are on every face.
        Assert.Equal([$"vertices, faces: {MeshDelete.EveryFaceMessage}"], Delete(cube, vertices: [0, Corner]).Errors.Select(e => e.ToString()));
        Assert.True(Delete(cube, faces: [0, 1, 2, 3, 4]).Succeeded);
    }

    [Fact]
    public void ADeletedFaceLeavesItsColourAndGroupsAndADeletedVertexItsWeights()
    {
        var style = FaceStyle.None.WithColour([Top, 0], "#ff0000").WithGroup("lid", [Top]).WithGroup("sides", [Top, 0]);
        var weights = VertexWeights.Of([KeyValuePair.Create(Corner, new[] { new Influence(0, 1) }), KeyValuePair.Create(5, new[] { new Influence(0, 1) })]);
        var cube = TestMeshes.Cube().WithStyle(style).WithWeights(weights);

        var m = TestMeshes.Ok(Delete(cube, vertices: [Corner]));
        Assert.Equal([0], m.Style!.Colours.Keys);
        Assert.Null(m.Style.FacesOf("lid"));
        Assert.Equal([0], m.Style.FacesOf("sides"));
        Assert.Equal([5], m.Weights!.Vertices);
    }

    [Fact]
    public void TopologyHasNoEdgeOfADeletedFaceOrVertex()
    {
        var top = TestMeshes.Ok(Delete(TestMeshes.Cube(), faces: [Top]));
        Assert.Equal(12, top.Topology.Edges.Count);
        Assert.Equal([(4, 5), (4, 7), (5, 6), (6, 7)],
            top.Topology.Edges.Where(e => e.Faces.Count == 1).Select(e => (e.Lo, e.Hi)).Order());
        Assert.DoesNotContain(top.Topology.Edges, e => e.Faces.Any(u => u.Face == Top));

        var corner = TestMeshes.Ok(Delete(TestMeshes.Cube(), vertices: [Corner]));
        Assert.DoesNotContain(corner.Topology.Edges, e => e.Lo == Corner || e.Hi == Corner);
    }

    [Fact]
    public void ACreateRefusesALiveFaceOnADeletedVertexAndAMeshOfTombstonesOnly()
    {
        var onDeleted = PolygonMesh.Create(TestMeshes.CubeVertices, TestMeshes.CubeFaces.Select(f => new Face(f)), [Corner]);
        Assert.Equal(["faces[1][2]: index 6 is a deleted vertex", "faces[3][2]: index 6 is a deleted vertex", "faces[5][2]: index 6 is a deleted vertex"],
            onDeleted.Errors.Select(e => e.ToString()));

        var none = PolygonMesh.Create(TestMeshes.CubeVertices, [Face.Deleted, Face.Deleted], null);
        Assert.Equal(["faces: mesh has no faces: every face is deleted"], none.Errors.Select(e => e.ToString()));

        // An empty face is still refused as a face, not taken for a tombstone.
        var empty = PolygonMesh.Create(TestMeshes.CubeVertices, [.. TestMeshes.CubeFaces.Select(f => new Face(f)), new Face([])]);
        Assert.Contains(empty.Errors, e => e.ToString() == "faces[6]: face has 0 indices; at least 3 are needed");
    }

    [Fact]
    public void TheFileWritesNullInADeletedSlotAndReadsItBackBitExact()
    {
        var m = TestMeshes.Ok(Delete(TestMeshes.Cube(), vertices: [Corner]));
        var text = CarveFile.Write(m);
        Assert.Contains("\"carve\": 4", text);
        var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim()).ToArray();
        Assert.Equal(4, lines.Count(l => l is "null" or "null,"));

        var read = CarveFile.Read(text);
        Assert.True(read.Succeeded, string.Join("; ", read.Errors));
        var back = read.Model!.Meshes[0].Mesh;
        Assert.True(back.IsVertexDeleted(Corner));
        Assert.Equal([1, 3, 5], Enumerable.Range(0, back.Faces.Count).Where(back.IsFaceDeleted));
        Assert.Equal(m.Vertices, back.Vertices);
        Assert.Equal(m.Faces, back.Faces);
        Assert.Equal(text, CarveFile.Write(back));
    }

    [Fact]
    public void AFileWhoseLiveFaceUsesANullVertexIsRefusedNamingTheFace()
    {
        var text = CarveFile.Write(TestMeshes.Ok(Delete(TestMeshes.Cube(), faces: [Top]))).Replace("\r", "");
        // Vertex 6 is (1, 1, 1); make its slot a tombstone while faces 3 and 5 still use it.
        var broken = text.Replace("[1, 1, 1]", "null");
        Assert.NotEqual(text, broken);
        var read = CarveFile.Read(broken);
        Assert.False(read.Succeeded);
        Assert.Equal(["meshes[0].faces[3][2]: index 6 is a deleted vertex", "meshes[0].faces[5][2]: index 6 is a deleted vertex"],
            read.Errors.Select(e => e.ToString()));
    }

    [Fact]
    public void AModelWithNoTombstoneAndNoHoleStillWritesVersion2()
        => Assert.Contains("\"carve\": 2", CarveFile.Write(TestMeshes.Cube()));
}

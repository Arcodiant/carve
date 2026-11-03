using System.Numerics;
using Xunit;
using static Carve.Core.Tests.MoveTests;

namespace Carve.Core.Tests;

/// <summary>The planning probe's crate, built through <see cref="MeshEdits"/> alone: the seed cube and 11 edits.</summary>
[Trait("Tier", "unit")]
public class CrateTests
{
    private static readonly int[] AllCubeFaces = [0, 1, 2, 3, 4, 5];

    /// <summary>The face's highest and lowest edges in Y, as vertex pairs.</summary>
    private static ((int A, int B) Top, (int A, int B) Bottom) TopAndBottom(PolygonMesh mesh, int face)
    {
        var loop = mesh.Faces[face];
        var pairs = Enumerable.Range(0, loop.Count).Select(k => (A: loop[k], B: loop[(k + 1) % loop.Count])).ToArray();
        float Y((int A, int B) e) => Math.Min(mesh.Vertices[e.A].Y, mesh.Vertices[e.B].Y);
        bool Level((int A, int B) e) => Math.Abs(mesh.Vertices[e.A].Y - mesh.Vertices[e.B].Y) < 1e-6f;
        var level = pairs.Where(Level).OrderBy(Y).ToArray();
        Assert.Equal(2, level.Length);
        return (level[1], level[0]);
    }

    [Fact]
    public void TheCrate()
    {
        var mesh = TestMeshes.Cube();
        mesh = Ok(MeshEdits.Move(mesh, null, null, [3], new Vector3(0, -0.2f, 0)));
        mesh = Ok(MeshEdits.Extrude(mesh, AllCubeFaces, 0, 0.12f));
        mesh = Ok(MeshEdits.Extrude(mesh, AllCubeFaces, -0.08f));

        // -X: the slot's edges drawn together by moving their vertices.
        mesh = Ok(MeshEdits.Extrude(mesh, [4], 0, 0.05f));
        var (top, bottom) = TopAndBottom(mesh, 4);
        mesh = Ok(MeshEdits.Move(mesh, [top.A, top.B], null, null, new Vector3(0, -0.17f, 0)));
        mesh = Ok(MeshEdits.Move(mesh, [bottom.A, bottom.B], null, null, new Vector3(0, 0.17f, 0)));
        mesh = Ok(MeshEdits.Extrude(mesh, [4], 0.1f));

        // +X: the same by moving the edges.
        mesh = Ok(MeshEdits.Extrude(mesh, [5], 0, 0.05f));
        (top, bottom) = TopAndBottom(mesh, 5);
        mesh = Ok(MeshEdits.Move(mesh, null, [top], null, new Vector3(0, -0.17f, 0)));
        mesh = Ok(MeshEdits.Move(mesh, null, [bottom], null, new Vector3(0, 0.17f, 0)));
        mesh = Ok(MeshEdits.Extrude(mesh, [5], 0.1f));

        var report = MeshReport.Create(mesh);
        Assert.Equal(72, report.VertexCount);
        Assert.Equal(70, report.FaceCount);
        Assert.True(report.Closed);
        Assert.Empty(report.Warnings);
        Assert.True(Vector3.Distance(new Vector3(-0.02f, 0, 0), report.BoundsMin) < 1e-5f, $"min {report.BoundsMin}");
        Assert.True(Vector3.Distance(new Vector3(1.02f, 0.8f, 1), report.BoundsMax) < 1e-5f, $"max {report.BoundsMax}");
    }
}

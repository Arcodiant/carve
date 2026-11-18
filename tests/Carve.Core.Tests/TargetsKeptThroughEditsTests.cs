using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>
/// T-100: every edit that keeps vertex identity keeps the mesh's morph targets. For each edit the morphed mesh (every target at
/// its weight) of the edited mesh is compared with the same edit applied to the morphed mesh, within 1e-6 of the bounding-box
/// diagonal. The targets are affine in position (and leave the cut plane's own coordinate alone), so that a cut's blended
/// deltas are exact even for a point inside a face; a knife point inside a face is also asserted against the stencil rule.
/// </summary>
[Trait("Tier", "unit")]
public class TargetsKeptThroughEditsTests
{
    private static readonly Dictionary<string, float> Weights = new() { ["A"] = 1f, ["B"] = 0.5f };

    // A and B move y and z by an affine function of x, y and z; neither moves x, so a plane x = c stays the same plane.
    private static Vector3 DeltaA(Vector3 p) => new(0, 0.3f * p.X + 0.1f * p.Z, 0.2f * p.Y + 0.05f);
    private static Vector3 DeltaB(Vector3 p) => new(0, -0.15f * p.Z, 0.1f * p.X - 0.2f * p.Y);

    private static MorphTarget Target(PolygonMesh mesh, string name, Func<Vector3, Vector3> delta)
    {
        var positions = mesh.Vertices.Select(v => v + delta(v)).ToArray();
        var r = MorphTarget.FromPositions(mesh, name, positions);
        Assert.True(r.Succeeded, string.Join("; ", r.Errors));
        return r.Target!;
    }

    private static PolygonMesh Affine(PolygonMesh mesh) =>
        mesh.WithTargets(MorphTargets.Of([Target(mesh, "A", DeltaA), Target(mesh, "B", DeltaB)]));

    private static PolygonMesh Plain(PolygonMesh mesh) => mesh.WithTargets(null);

    private static PolygonMesh Morph(PolygonMesh mesh) => mesh.Morphed(Weights);

    private static double Diagonal(PolygonMesh mesh)
    {
        var live = Enumerable.Range(0, mesh.Vertices.Count).Where(v => !mesh.IsVertexDeleted(v)).Select(v => mesh.Vertices[v]).ToArray();
        var lo = live.Aggregate(Vector3.Min);
        var hi = live.Aggregate(Vector3.Max);
        return (hi - lo).Length();
    }

    /// <summary>The kept mesh at its weights is the plain edit of the morphed mesh: the same live vertices, within 1e-6 of the diagonal, on the same faces.</summary>
    private static void AssertSameMorphed(PolygonMesh edited, PolygonMesh editedMorphed, IReadOnlyDictionary<string, float>? weights = null)
    {
        Assert.NotNull(edited.Targets);
        var morphed = edited.Morphed(weights ?? Weights);
        var tol = 1e-6 * Diagonal(editedMorphed);
        Assert.Equal(editedMorphed.Vertices.Count, morphed.Vertices.Count);
        for (var v = 0; v < morphed.Vertices.Count; v++)
        {
            Assert.Equal(editedMorphed.IsVertexDeleted(v), edited.IsVertexDeleted(v));
            if (edited.IsVertexDeleted(v)) continue;
            Assert.True((morphed.Vertices[v] - editedMorphed.Vertices[v]).Length() <= tol,
                $"vertex {v}: {morphed.Vertices[v]} against {editedMorphed.Vertices[v]}");
        }
        Assert.Equal(editedMorphed.Faces.Select(f => f.Corners), edited.Faces.Select(f => f.Corners));
    }

    [Fact]
    public void MoveKeepsEveryDeltaSoTheTargetMovesWithTheBase()
    {
        var cube = Affine(Cube());
        var by = new Vector3(0.3f, -0.2f, 0.4f);
        var moved = Ok(MeshEdits.Move(cube, [2, 5], null, null, by));
        var onMorphed = Ok(MeshEdits.Move(Plain(Morph(cube)), [2, 5], null, null, by));
        AssertSameMorphed(moved, onMorphed);
        foreach (var t in cube.Targets!.All)
            Assert.Equal(t.Deltas, moved.Targets!.Find(t.Name)!.Deltas);
    }

    [Fact]
    public void ExtrudeGivesEachNewVertexTheDeltaOfTheVertexItStandsOver()
    {
        // A stretch of the top face: its vertices rise by 0.5, so the face stays flat and keeps its normal.
        var cube = Cube();
        var stretch = MorphTarget.FromPositions(cube, "Tall", cube.Vertices.Select(v => v.Z > 0.5f ? v + new Vector3(0, 0, 0.5f) : v).ToArray()).Target!;
        var mesh = cube.WithTargets(MorphTargets.Of([stretch]));
        var w = new Dictionary<string, float> { ["Tall"] = 1f };
        foreach (var inset in new[] { 0f, 0.1f })
        {
            var r = MeshEdits.Extrude(mesh, [1], 0.3f, inset);
            var extruded = Ok(r);
            var onMorphed = Ok(MeshEdits.Extrude(Plain(mesh.Morphed(w)), [1], 0.3f, inset));
            AssertSameMorphed(extruded, onMorphed, w);
            var x = Assert.Single(r.Extruded);
            var tall = extruded.Targets!.Find("Tall")!;
            for (var k = 0; k < x.NewVertices.Count; k++)
                Assert.Equal(stretch.DeltaOf(mesh.Faces[1].Corners[k]), tall.DeltaOf(x.NewVertices[k]));
            Assert.Equal(4, x.NewVertices.Count(v => tall.DeltaOf(v) != Vector3.Zero));
        }
    }

    [Fact]
    public void ExtrudeOfAFaceWithHolesCopiesTheDeltasOfItsHoleCornersToo()
    {
        var verts = new List<Vector3>();
        foreach (var s in new[] { 1f, 0.5f }) verts.AddRange([new(-s, -s, 0), new(s, -s, 0), new(s, s, 0), new(-s, s, 0)]);
        var plate = PolygonMesh.Create(verts, [new Face([0, 1, 2, 3], [[4, 7, 6, 5]])]).Mesh!;
        var target = MorphTarget.FromPositions(plate, "Lift", verts.Select((v, i) => v + new Vector3(0, 0, 0.1f * (i + 1))).ToArray()).Target!;
        var mesh = plate.WithTargets(MorphTargets.Of([target]));
        var r = MeshEdits.Extrude(mesh, [0], 0.2f);
        var m = Ok(r);
        var x = Assert.Single(r.Extruded);
        Assert.Equal(8, x.NewVertices.Count);
        for (var k = 0; k < 8; k++)
            Assert.Equal(target.DeltaOf(mesh.Faces[0].Corners[k]), m.Targets!.Find("Lift")!.DeltaOf(x.NewVertices[k]));
    }

    [Fact]
    public void DeleteDropsADeletedVertexsDeltaAndKeepsTheRest()
    {
        var cube = Affine(Cube());
        var edited = Ok(MeshDelete.Delete(cube, [6], null));
        var onMorphed = Ok(MeshDelete.Delete(Plain(Morph(cube)), [6], null));
        AssertSameMorphed(edited, onMorphed);
        foreach (var t in edited.Targets!.All)
        {
            Assert.DoesNotContain(6, t.Deltas.Keys);
            Assert.Equal(cube.Targets!.Find(t.Name)!.Deltas.Where(p => p.Key != 6), t.Deltas);
        }
    }

    [Fact]
    public void MergeFacesKeepsTargetsAndTheVertexItRemovesLosesItsDelta()
    {
        var grid = Affine(Grid(2, 2));
        var merged = Ok(FaceMerge.Merge(grid, [0, 1, 2, 3], out _));
        Assert.True(merged.IsVertexDeleted(4));
        AssertSameMorphed(merged, Ok(FaceMerge.Merge(Plain(Morph(grid)), [0, 1, 2, 3], out _)));
        foreach (var t in merged.Targets!.All)
        {
            Assert.DoesNotContain(4, t.Deltas.Keys);
            Assert.Equal(grid.Targets!.Find(t.Name)!.Deltas.Where(p => p.Key != 4), t.Deltas);
        }
    }

    [Fact]
    public void BridgeKeepsEveryTarget()
    {
        var verts = new List<Vector3>();
        foreach (var z in new[] { 0f, 1f }) verts.AddRange([new(0, 0, z), new(1, 0, z), new(1, 1, z), new(0, 1, z)]);
        var squares = Affine(Make(verts, [new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }]));
        var bridged = Ok(Bridge.Join(squares, [0, 1, 2, 3], [4, 5, 6, 7]));
        AssertSameMorphed(bridged, Ok(Bridge.Join(Plain(Morph(squares)), [0, 1, 2, 3], [4, 5, 6, 7])));
        foreach (var t in squares.Targets!.All)
            Assert.Equal(t.Deltas, bridged.Targets!.Find(t.Name)!.Deltas);
    }

    [Fact]
    public void PlaneCutBlendsEachNewVertexsDeltaFromTheEdgeItSplits()
    {
        var cube = Affine(Cube());
        var plane = new Vector3(0.5f, 0, 0);
        var r = PlaneCut.Cut(cube, plane, new Vector3(1, 0, 0));
        var cut = Ok(r.Edit);
        Assert.Equal(4, r.Edit.NewVertices.Count);
        AssertSameMorphed(cut, Ok(PlaneCut.Cut(Plain(Morph(cube)), plane, new Vector3(1, 0, 0)).Edit));
        Assert.All(r.Edit.NewVertices, v => Assert.NotEqual(Vector3.Zero, cut.Targets!.Find("A")!.DeltaOf(v)));
    }

    [Fact]
    public void PlaneCutThatDeletesASideDropsTheDeltasOfTheVerticesItRemoves()
    {
        var cube = Affine(Cube());
        var plane = new Vector3(0.5f, 0, 0);
        var cut = Ok(PlaneCut.Cut(cube, plane, new Vector3(1, 0, 0), PlaneKeep.Above).Edit);
        AssertSameMorphed(cut, Ok(PlaneCut.Cut(Plain(Morph(cube)), plane, new Vector3(1, 0, 0), PlaneKeep.Above).Edit));
        var gone = Enumerable.Range(0, cut.Vertices.Count).Where(cut.IsVertexDeleted).ToArray();
        Assert.NotEmpty(gone);
        Assert.All(gone, v => Assert.All(cut.Targets!.All, t => Assert.DoesNotContain(v, t.Deltas.Keys)));
    }

    [Fact]
    public void LoopCutBlendsEachNewVertexsDeltaFromItsEdge()
    {
        var cube = Affine(Cube());
        var r = LoopCut.Cut(cube, (0, 1), [0.5]);
        var cut = Ok(r.Edit);
        Assert.Equal(4, r.Edit.NewVertices.Count);
        AssertSameMorphed(cut, Ok(LoopCut.Cut(Plain(Morph(cube)), (0, 1), [0.5]).Edit));
        Assert.All(r.Edit.NewVertices, v => Assert.NotEqual(Vector3.Zero, cut.Targets!.Find("A")!.DeltaOf(v)));
    }

    [Fact]
    public void KnifeAcrossAFaceFromEdgeToEdgeKeepsTheTargets()
    {
        var cube = Affine(Cube());
        var morphed = Morph(cube);
        var cut = Ok(Knife.Cut(cube, [new(0.5f, 0, 1), new(0.5f, 1, 1)]).Edit);
        var onMorphed = Ok(Knife.Cut(Plain(morphed),
            [Vector3.Lerp(morphed.Vertices[4], morphed.Vertices[5], 0.5f), Vector3.Lerp(morphed.Vertices[7], morphed.Vertices[6], 0.5f)]).Edit);
        AssertSameMorphed(cut, onMorphed);
    }

    [Fact]
    public void KnifePointInsideAFaceTakesTheStencilBlendOfTheFacesCornerDeltas()
    {
        var cube = Affine(Cube());
        var r = Knife.Cut(cube, [new(0.5f, 0, 1), new(0.4f, 0.5f, 1), new(0.5f, 1, 1)]);
        var cut = Ok(r.Edit);
        Assert.Equal([8, 9, 10], r.Edit.NewVertices);
        // Vertex 10 is the face's barycentric blend of its corners' deltas; the targets are affine, so that blend is the affine
        // delta at its position. The edge vertices blend their edge's two ends.
        foreach (var (name, f) in new (string, Func<Vector3, Vector3>)[] { ("A", DeltaA), ("B", DeltaB) })
        {
            var t = cut.Targets!.Find(name)!;
            foreach (var v in r.Edit.NewVertices)
                Assert.True((t.DeltaOf(v) - f(cut.Vertices[v])).Length() <= 1e-6, $"{name} vertex {v}");
        }
        var a = cut.Targets!.Find("A")!;
        Assert.Equal(Vector3.Lerp(a.DeltaOf(4), a.DeltaOf(5), 0.5f).Y, a.DeltaOf(8).Y, 1e-6);
    }

    [Fact]
    public void KnifePointInsideAFaceBlendsATargetThatIsNotAffineByTheTriangleThePointLiesIn()
    {
        // RV-64: an affine target gives the same delta under any blend that reproduces the point, so this one is not affine
        // over the top face (only corners 4 and 6 move, by different amounts). The stencil rule for an interior point is the
        // barycentric blend of the corners of the face triangle it lies in, the stencil that blends its weights.
        var plain = Cube();
        var corners = new Dictionary<int, Vector3> { [4] = new(0, 0, 0.6f), [6] = new(0, 0.2f, -0.3f) };
        var cube = plain.WithTargets(MorphTargets.Of([MorphTarget.Of("C", corners)]));
        var at = new Vector3(0.4f, 0.5f, 1);
        var r = Knife.Cut(cube, [new(0.5f, 0, 1), at, new(0.5f, 1, 1)]);
        var cut = Ok(r.Edit);
        Assert.Equal(new Vector3(0.4f, 0.5f, 1), cut.Vertices[10]);

        var lies = Triangulator.TriangulateFace(plain, 1).Select(t =>
        {
            var (a, b, c) = (plain.Vertices[t.A], plain.Vertices[t.B], plain.Vertices[t.C]);
            double Orient(Vector3 p, Vector3 q, Vector3 o) => ((double)q.X - p.X) * ((double)o.Y - p.Y) - ((double)q.Y - p.Y) * ((double)o.X - p.X);
            var area = Orient(a, b, c);
            return (t, Wa: Orient(at, b, c) / area, Wb: Orient(a, at, c) / area, Wc: Orient(a, b, at) / area);
        }).Single(x => x.Wa >= 0 && x.Wb >= 0 && x.Wc >= 0);
        Vector3 D(int v) => corners.TryGetValue(v, out var d) ? d : Vector3.Zero;
        var expected = D(lies.t.A) * (float)lies.Wa + D(lies.t.B) * (float)lies.Wb + D(lies.t.C) * (float)lies.Wc;
        var got = cut.Targets!.Find("C")!.DeltaOf(10);
        Assert.True((got - expected).Length() <= 1e-6, $"vertex 10's delta {got}, the stencil rule gives {expected}");
    }
}

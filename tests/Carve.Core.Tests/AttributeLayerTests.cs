using System.Numerics;
using Xunit;
using static Carve.Core.Tests.TestMeshes;

namespace Carve.Core.Tests;

/// <summary>
/// The attribute layer registry (T-71): each rule on its own (lerp, weights, split edges, moves, reshapes), the standard
/// layers carried through real edits, and determinism.
/// </summary>
[Trait("Tier", "unit")]
public class AttributeLayerTests
{
    /// <summary>A unit square in z = 0, corners 0..3 counter-clockwise from the origin.</summary>
    private static PolygonMesh Square() => Make(
        [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0)], [[0, 1, 2, 3]]);

    private static readonly Vector2[] SquareUvs = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

    /// <summary>The square cut in two from the middle of edge 0-1 to the middle of edge 2-3 (new vertices 4 and 5).</summary>
    private static EditResult Halve(PolygonMesh square) =>
        FaceSplit.Split(square, [new FaceCut(0, CutPoint.OnEdge(0, 1, 0.5), CutPoint.OnEdge(3, 2, 0.5))]).Edit;

    private static Vector2 Lerped(int a, int b, double t, Vector2 at, Vector2 bt)
    {
        var stencil = Stencil.Lerp(a, b, t);
        var terms = stencil.Terms.Select(p => (p.Element == a ? at : bt, p.Weight)).ToList();
        Assert.True(LayerRules.Lerp(terms, stencil, out var value));
        return value;
    }

    // ---- lerp

    [Fact]
    public void Lerp_gives_each_end_exactly_and_the_midpoint_between_them()
    {
        Vector2 a = new(0.1f, 0.7f), b = new(0.9f, 0.3f);
        Assert.Equal(a, Lerped(0, 1, 0, a, b));
        Assert.Equal(b, Lerped(0, 1, 1, a, b));
        Assert.Equal(new Vector2(0.5f, 0.5f), Lerped(0, 1, 0.5, a, b));
    }

    [Fact]
    public void Lerp_over_a_divisor_sums_first_then_divides_as_a_mean_does()
    {
        Vector2[] corners = [new(0.1f, 0.2f), new(0.4f, 0.3f), new(0.7f, 0.9f)];
        var stencil = Stencil.Of([(0, 1.0), (1, 1.0), (2, 1.0)], divisor: 3);
        Assert.True(LayerRules.Lerp(corners.Select(c => (c, 1.0)).ToList(), stencil, out var mean));
        double x = 0, y = 0;
        foreach (var c in corners) { x += c.X; y += c.Y; }
        Assert.Equal(new Vector2((float)(x / 3), (float)(y / 3)), mean);
    }

    [Fact]
    public void A_sum_from_the_first_term_keeps_a_negative_zero_and_a_sum_from_zero_does_not()
    {
        List<(Vector2, double)> zeros = [(new Vector2(-0f, -0f), 1.0), (new Vector2(-0f, -0f), 1.0)];
        Assert.True(LayerRules.Lerp(zeros, Stencil.Of([(0, 1.0), (1, 1.0)], divisor: 2, sumsFromFirst: true), out var first));
        Assert.True(LayerRules.Lerp(zeros, Stencil.Of([(0, 1.0), (1, 1.0)], divisor: 2), out var zero));
        Assert.True(float.IsNegative(first.X) && float.IsNegative(first.Y));
        Assert.False(float.IsNegative(zero.X) || float.IsNegative(zero.Y));
    }

    [Fact]
    public void Subdivide_keeps_a_negative_zero_uv_at_an_edge_midpoint_and_takes_the_mean_from_zero()
    {
        // RV-62: subdivide's midpoints sum from their first corner, as they did before T-71, and the mean from 0.
        var cube = Cube();
        var uvs = MeshUvs.None.WithFace(0, [new(-0f, -0f), new(-0f, -0f), new(-0f, 1), new(-0f, 1)]);
        var child = Ok(MeshEdits.Subdivide(cube.WithUvs(uvs), 1)).Uvs!.Of(0)!; // [corner 0, mid 0-1, mean, mid 3-0]

        Assert.True(float.IsNegative(child[0].X) && float.IsNegative(child[0].Y));
        Assert.True(float.IsNegative(child[1].X) && float.IsNegative(child[1].Y));
        Assert.False(float.IsNegative(child[2].X));
        Assert.True(float.IsNegative(child[3].X));
    }

    [Fact]
    public void A_cut_lerps_the_uvs_of_its_face_onto_the_new_corners()
    {
        var square = Square().WithUvs(MeshUvs.None.WithFace(0, SquareUvs));
        var mesh = Ok(Halve(square));

        Assert.Equal([0, 4, 5, 3], mesh.Faces[0].Loop);
        Assert.Equal([new(0, 0), new(0.5f, 0), new(0.5f, 1), new(0, 1)], mesh.Uvs!.Of(0)!);
        Assert.Equal([new(0.5f, 0), new(1, 0), new(1, 1), new(0.5f, 1)], mesh.Uvs.Of(1)!);
    }

    // ---- weights

    [Fact]
    public void Blended_weights_keep_the_top_four_and_sum_to_one()
    {
        var source = new Dictionary<int, Influence[]>
        {
            [0] = [new(0, 0.5), new(1, 0.3), new(2, 0.2)],
            [1] = [new(3, 0.4), new(4, 0.35), new(5, 0.25)],
        };
        Assert.True(LayerRules.BlendWeights(VertexOrigin.Blend(Stencil.Lerp(0, 1, 0.5)), source, out var blended));

        Assert.Equal(VertexWeights.MaxInfluences, blended.Length);
        Assert.Equal([0, 1, 3, 4], blended.Select(i => i.Bone));
        Assert.Equal(1, blended.Sum(i => i.Weight), 12);
    }

    [Fact]
    public void Blended_weights_break_a_tie_for_fourth_to_the_lower_bone()
    {
        var source = new Dictionary<int, Influence[]> { [0] = [new(7, 0.2), new(2, 0.2), new(5, 0.2), new(1, 0.2), new(9, 0.2)] };
        Assert.True(LayerRules.BlendWeights(VertexOrigin.Blend(Stencil.Of(0)), source, out var blended));
        Assert.Equal([1, 2, 5, 7], blended.Select(i => i.Bone));
    }

    [Fact]
    public void An_unweighted_source_adds_nothing_and_none_weighted_gives_no_value()
    {
        var source = new Dictionary<int, Influence[]> { [0] = [new(3, 1)] };
        Assert.True(LayerRules.BlendWeights(VertexOrigin.Blend(Stencil.Lerp(0, 1, 0.75)), source, out var blended));
        Assert.Equal([new Influence(3, 1)], blended);
        Assert.False(LayerRules.BlendWeights(VertexOrigin.Blend(Stencil.Of(1)), source, out _));
    }

    [Fact]
    public void An_extrudes_ring_vertex_is_an_offset_and_starts_unweighted()
    {
        // The person, 2026-11-18: extrude's tool behaviour stays; the ring is declared, but the weights rule gives an offset nothing.
        var cube = Cube().WithWeights(VertexWeights.Of(Enumerable.Range(0, 8).Select(v => KeyValuePair.Create(v, new[] { new Influence(0, 1) }))));
        var result = MeshEdits.Extrude(cube, [0], 0.5f);
        var mesh = Ok(result);

        Assert.All(result.NewVertices, v => Assert.False(mesh.Weights!.Has(v)));
        Assert.All(Enumerable.Range(0, 8), v => Assert.Equal([new Influence(0, 1)], mesh.Weights!.Of(v)));
        Assert.False(LayerRules.BlendWeights(VertexOrigin.Offset(0), new Dictionary<int, Influence[]> { [0] = [new(0, 1)] }, out _));
    }

    [Fact]
    public void A_cut_blends_the_weights_of_its_edges_ends_by_fraction()
    {
        var square = Square().WithWeights(VertexWeights.Of([
            KeyValuePair.Create(0, new[] { new Influence(0, 1) }),
            KeyValuePair.Create(1, new[] { new Influence(1, 1) }),
        ]));
        var mesh = Ok(FaceSplit.Split(square, [new FaceCut(0, CutPoint.OnEdge(0, 1, 0.25), CutPoint.OnEdge(3, 2, 0.25))]).Edit);

        Assert.Equal([new Influence(0, 0.75), new Influence(1, 0.25)], mesh.Weights!.Of(4));
        Assert.False(mesh.Weights.Has(5));
    }

    // ---- split edges

    [Fact]
    public void Each_piece_of_a_split_hard_edge_is_hard_and_the_cuts_new_edge_is_not()
    {
        var square = Square().WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1)]));
        var mesh = Ok(Halve(square));

        Assert.Equal([(0, 4), (1, 4)], mesh.HardEdges);
        Assert.Equal(ShadingMode.Smooth, mesh.ShadingMode);
    }

    [Fact]
    public void The_split_rule_sees_the_parent_and_each_piece_in_order()
    {
        var seen = new List<((int, int) Parent, (int, int) Piece)>();
        var layer = new EdgeLayer<int>("test", 0, _ => new Dictionary<(int Lo, int Hi), int> { [(0, 1)] = 7 }, (_, m, _) => m,
            ((int Lo, int Hi) parent, int value, (int Lo, int Hi) piece, out int result) => { seen.Add((parent, piece)); result = value + 1; return true; });
        var provenance = new EditProvenance().Split(1, 0, [5, 4]); // given from 1: 1, 5, 4, 0

        var carried = layer.Carried(Square(), Square(), provenance);

        Assert.Equal([((0, 1), (0, 4)), ((0, 1), (4, 5)), ((0, 1), (1, 5))], seen);
        Assert.Equal(8, carried[(4, 5)]);
        Assert.False(carried.ContainsKey((0, 1)));
    }

    [Fact]
    public void Subdivide_carries_a_hard_edge_to_both_halves()
    {
        var cube = Cube();
        var edge = cube.Topology.Edges[0];
        var mesh = Ok(MeshEdits.Subdivide(cube.WithShade(MeshShade.Of(ShadingMode.Smooth, [(edge.Lo, edge.Hi)])), 1));
        var point = cube.Vertices.Count + cube.Faces.Count; // edge 0's edge point

        Assert.Equal(new[] { (edge.Lo, point), (edge.Hi, point) }.Order(), mesh.HardEdges);
    }

    // ---- moves

    [Fact]
    public void A_moved_vertex_keeps_its_weights_and_its_corners_uvs()
    {
        var square = Square()
            .WithWeights(VertexWeights.Of([KeyValuePair.Create(2, new[] { new Influence(0, 0.25), new Influence(1, 0.75) })]))
            .WithUvs(MeshUvs.None.WithFace(0, SquareUvs));
        var mesh = Ok(MeshEdits.Move(square, [2], null, null, new Vector3(0.5f, 0.5f, 0)));

        Assert.Equal(square.Weights!.Of(2), mesh.Weights!.Of(2));
        Assert.Equal(SquareUvs, mesh.Uvs!.Of(0)!);
    }

    [Fact]
    public void The_move_rules_run_only_for_a_vertex_that_moved()
    {
        var square = Square();
        var moved = Ok(MeshEdits.Move(square, [2], null, null, new Vector3(0.5f, 0, 0)));
        var vertexCalls = new List<int>();
        var vertices = new VertexLayer<int>("v", 0, m => Enumerable.Range(0, 4).ToDictionary(v => v, v => v * 10), (_, m, _) => m,
            LayerRules.NoValue,
            (int v, VertexOrigin? origin, int current, IReadOnlyDictionary<int, int> source, out int value) => { vertexCalls.Add(v); value = -current; return true; });
        var cornerCalls = new List<(int Face, int Corner, int Vertex)>();
        var corners = new CornerLayer<int>("c", 0, m => new Dictionary<int, int[]> { [0] = [1, 2, 3, 4] }, (_, m, _) => m,
            (IReadOnlyList<(int Value, double Weight)> terms, Stencil stencil, out int value) => { value = 0; return false; },
            EqualityComparer<int>.Default,
            (int f, int c, int v, int current, out int value) => { cornerCalls.Add((f, c, v)); value = current * 100; return true; });

        var vs = vertices.Carried(square, moved, new EditProvenance());
        var cs = corners.Carried(square, moved, new EditProvenance());

        Assert.Equal([2], vertexCalls);
        Assert.Equal([0, 10, -20, 30], vs.Values);
        Assert.Equal([(0, 2, 2)], cornerCalls);
        Assert.Equal([1, 2, 300, 4], cs[0]);
    }

    // ---- reshapes

    [Fact]
    public void An_extrudes_cap_keeps_its_look_and_uvs_and_each_wall_takes_its_faces_look_and_groups_unmapped()
    {
        var cube = Cube()
            .WithStyle(FaceStyle.None.WithColour([0], "#C8503C").WithMaterial([1], "oak").WithGroup("lid", [0, 2]))
            .WithUvs(MeshUvs.None.WithFace(0, SquareUvs));
        var result = MeshEdits.Extrude(cube, [0], 0.5f);
        var mesh = Ok(result);
        var walls = result.Extruded[0].Walls;

        Assert.Equal("#C8503C", mesh.Style!.ColourOf(0));
        Assert.Equal(SquareUvs, mesh.Uvs!.Of(0)!);
        Assert.All(walls, w => Assert.Equal("#C8503C", mesh.Style.ColourOf(w)));
        Assert.All(walls, w => Assert.False(mesh.Uvs.IsMapped(w)));
        Assert.Equal([0, 2, .. walls], mesh.Style.FacesOf("lid")!);
        Assert.Equal("oak", mesh.Style.MaterialOf(1));
    }

    [Fact]
    public void A_reshaped_face_runs_the_reshape_rule_and_an_untouched_face_does_not()
    {
        var cube = Cube();
        var result = MeshEdits.Extrude(cube, [0], 0.5f);
        var calls = new List<(int Face, FaceOrigin? Origin)>();
        var layer = new FaceLayer<string>("f", "", _ => Enumerable.Range(0, 6).ToDictionary(f => f, f => $"face {f}"), (_, m, _) => m,
            LayerRules.CopyFromSourceFace,
            (int f, FaceOrigin? origin, string current, out string value) => { calls.Add((f, origin)); value = current + " reshaped"; return true; });
        var provenance = new EditProvenance().Face(0, new FaceOrigin(0, CornerOriginKind.Position));
        foreach (var w in result.Extruded[0].Walls) provenance.Face(w, new FaceOrigin(0, CornerOriginKind.None));

        var carried = layer.Carried(cube, Ok(result), provenance);

        Assert.Equal([(0, (FaceOrigin?)new FaceOrigin(0, CornerOriginKind.Position))], calls);
        Assert.Equal("face 0 reshaped", carried[0]);
        Assert.Equal("face 1", carried[1]);
        Assert.All(result.Extruded[0].Walls, w => Assert.Equal("face 0", carried[w]));
    }

    [Fact]
    public void A_hard_rim_edge_stays_on_its_pair_when_an_extrude_moves_its_faces_and_the_reshape_rule_sees_them()
    {
        // The bevel finding: the pair 0-1 was between faces 0 and its neighbour, and after the extrude it is between the
        // neighbour and a wall. The person, 2026-11-18: the mark stays on the pair; the cap's new rim edge is soft.
        var cube = Cube();
        var (a, b) = (cube.Faces[0].Loop[0], cube.Faces[0].Loop[1]);
        var smooth = cube.WithShade(MeshShade.Of(ShadingMode.Smooth, [(a, b)]));
        var result = MeshEdits.Extrude(smooth, [0], 0.5f);
        var mesh = Ok(result);
        Assert.Equal([(Math.Min(a, b), Math.Max(a, b))], mesh.HardEdges);

        var seen = new List<(IReadOnlyList<int> Before, IReadOnlyList<int> After)>();
        var layer = new EdgeLayer<bool>("h", false, m => m.HardEdges.ToDictionary(e => e, _ => true), (_, m, _) => m,
            LayerRules.InheritFromSplitEdge,
            ((int Lo, int Hi) edge, bool value, IReadOnlyList<int> before, IReadOnlyList<int> after, out bool kept) => { seen.Add((before, after)); kept = value; return true; });
        layer.Carried(smooth, mesh, new EditProvenance());

        var (before, after) = Assert.Single(seen);
        Assert.Contains(0, before);
        Assert.DoesNotContain(0, after);
        Assert.Contains(result.Extruded[0].Walls[0], after);
    }

    [Fact]
    public void A_merged_face_keeps_corner_uvs_only_where_its_faces_agree()
    {
        var grid = Grid(2, 1); // faces [0, 1, 4, 3] and [1, 2, 5, 4]
        Vector2 At(int v) => new(grid.Vertices[v].X, grid.Vertices[v].Y);
        Vector2[] Uvs(int f) => grid.Faces[f].Corners.Select(At).ToArray();
        var merged = Ok(FaceMerge.Merge(grid, [0, 1], out _));
        var provenance = new EditProvenance().Face(0, new FaceOrigin(0, CornerOriginKind.Agree) { Joined = [0, 1] });

        var agreeing = MeshUvs.None.WithFace(0, Uvs(0)).WithFace(1, Uvs(1));
        Assert.Equal(merged.Faces[0].Corners.Select(At), StandardLayers.Uvs.Carried(grid.WithUvs(agreeing), merged, provenance)[0]);

        var shifted = Uvs(1);
        shifted[grid.Faces[1].CornerOf(4)] += new Vector2(0.5f, 0);
        var differing = agreeing.WithFace(1, shifted);
        Assert.False(StandardLayers.Uvs.Carried(grid.WithUvs(differing), merged, provenance).ContainsKey(0));

        var half = MeshUvs.None.WithFace(0, Uvs(0));
        Assert.False(StandardLayers.Uvs.Carried(grid.WithUvs(half), merged, provenance).ContainsKey(0));
    }

    [Fact]
    public void A_face_whose_corner_count_changes_with_no_origin_loses_its_uvs()
    {
        var square = Square().WithUvs(MeshUvs.None.WithFace(0, SquareUvs));
        var triangle = square.Edit(reshapedFaces: [KeyValuePair.Create(0, new Face([0, 1, 2]))]).Mesh!;
        Assert.Empty(StandardLayers.Uvs.Carried(square, triangle, new EditProvenance()));
    }

    [Fact]
    public void A_deleted_face_and_vertex_leave_every_layer()
    {
        var cube = Cube()
            .WithStyle(FaceStyle.None.WithColour([0, 1], "#112233").WithGroup("top", [0]))
            .WithWeights(VertexWeights.Of(Enumerable.Range(0, 8).Select(v => KeyValuePair.Create(v, new[] { new Influence(0, 1) }))))
            .WithUvs(MeshUvs.None.WithFace(0, SquareUvs));
        // Deleting vertex 0 takes the three faces on it and leaves no face on it, so it goes too.
        var result = MeshDelete.Delete(cube, [0], null);
        var mesh = Ok(result);

        Assert.Equal([0], result.Deleted.Vertices);
        Assert.Contains(0, result.Deleted.Faces);
        Assert.Null(mesh.Style!.ColourOf(0));
        Assert.Null(mesh.Style.FacesOf("top"));
        Assert.False(mesh.Uvs?.IsMapped(0) ?? false);
        Assert.False(mesh.Weights!.Has(0));
        Assert.Equal(7, mesh.Weights.Count);
    }

    // ---- registry and determinism

    [Fact]
    public void The_standard_registry_carries_its_layers_in_a_fixed_order_and_refuses_a_second_of_one_name()
    {
        Assert.Equal(["colour", "material", "groups", "weights", "hard", "uvs", "seams", "targets"], LayerRegistry.Standard.Layers.Select(l => l.Name));
        Assert.Equal(
            [LayerElement.Face, LayerElement.Face, LayerElement.Face, LayerElement.Vertex, LayerElement.Edge, LayerElement.Corner, LayerElement.Edge, LayerElement.Vertex],
            LayerRegistry.Standard.Layers.Select(l => l.Element));
        Assert.Throws<ArgumentException>(() => LayerRegistry.Standard.With(StandardLayers.Colour));
    }

    [Fact]
    public void Carrying_is_deterministic_whatever_order_the_provenance_was_given_in()
    {
        var square = Square()
            .WithStyle(FaceStyle.None.WithColour([0], "#445566").WithGroup("g", [0]))
            .WithWeights(VertexWeights.Of([KeyValuePair.Create(0, new[] { new Influence(2, 1) }), KeyValuePair.Create(1, new[] { new Influence(1, 1) })]))
            .WithShade(MeshShade.Of(ShadingMode.Smooth, [(0, 1), (2, 3)]))
            .WithUvs(MeshUvs.None.WithFace(0, SquareUvs).WithSeams([(1, 2)], true));
        var geometry = Ok(Halve(Square()));

        EditProvenance Provenance(bool reversed)
        {
            var p = new EditProvenance();
            var steps = new List<Action>
            {
                () => p.Vertex(4, VertexOrigin.Blend(Stencil.Lerp(0, 1, 0.5))),
                () => p.Vertex(5, VertexOrigin.Blend(Stencil.Lerp(2, 3, 0.5))),
                () => p.Face(0, new FaceOrigin(0, CornerOriginKind.Vertex)),
                () => p.Face(1, new FaceOrigin(0, CornerOriginKind.Vertex)),
                () => p.Split(0, 1, [4]),
                () => p.Split(3, 2, [5]),
            };
            if (reversed) steps.Reverse();
            foreach (var step in steps) step();
            return p;
        }

        var first = LayerRegistry.Standard.Carry(square, geometry, Provenance(false));
        var again = LayerRegistry.Standard.Carry(square, geometry, Provenance(false));
        var reversed = LayerRegistry.Standard.Carry(square, geometry, Provenance(true));
        var viaEdit = Ok(Halve(square));

        foreach (var other in new[] { again, reversed, viaEdit })
        {
            Assert.Equal(Text(first), Text(other));
        }
        Assert.Equal([(0, 4), (1, 4), (2, 5), (3, 5)], first.HardEdges);
        Assert.Equal([(1, 2)], first.Seams);
    }

    /// <summary>Everything a mesh carries, as text, so two meshes compare by all their layers at once.</summary>
    private static string Text(PolygonMesh mesh)
    {
        var style = mesh.Style ?? FaceStyle.None;
        var weights = mesh.Weights ?? VertexWeights.None;
        return string.Join(Environment.NewLine,
            "colours " + string.Join(" ", style.Colours.Select(c => $"{c.Key}={c.Value}")),
            "materials " + string.Join(" ", style.Materials.Select(c => $"{c.Key}={c.Value}")),
            "groups " + string.Join(" ", style.Groups.Select(g => $"{g.Name}=[{string.Join(",", g.Faces)}]")),
            "weights " + string.Join(" ", weights.Vertices.Select(v => $"{v}=[{string.Join(",", weights.Of(v).Select(i => $"{i.Bone}:{i.Weight:R}"))}]")),
            $"shade {mesh.ShadingMode} " + string.Join(" ", mesh.HardEdges),
            "uvs " + string.Join(" ", (mesh.Uvs?.MappedFaces ?? []).Select(f => $"{f}=[{string.Join(",", mesh.Uvs!.Of(f)!.Select(u => $"{u.X:R}/{u.Y:R}"))}]")),
            "seams " + string.Join(" ", mesh.Seams));
    }
}

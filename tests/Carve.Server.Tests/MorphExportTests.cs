using System.Numerics;
using System.Security.Cryptography;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;
using Model = Carve.Core.Model;
using Skeleton = Carve.Core.Skeleton;

namespace Carve.Server.Tests;

/// <summary>
/// Morph targets in the <c>.glb</c> (T-104), read back by Empyrean's own parser with no process and no GPU: the names, the
/// default weights, and every position and normal delta the loader gives, each against Carve's own (the Carve vertex's
/// delta, and its corner's <see cref="Shading"/> normal at weight 1 less at rest), flat and smooth, skinned and not.
/// </summary>
[Trait("Tier", "unit")]
public class MorphExportTests
{
    private static ParsedModel Parse(byte[] glb) => GlbExportTests.Parser.Parse(glb, ModelFormat.Gltf);

    private static Skeleton Rig() => Skeleton.Create([new BoneSpec("Root", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;

    private static PolygonMesh Smooth(PolygonMesh mesh) => mesh.WithShade(MeshShade.Flat.WithMode(ShadingMode.Smooth));

    private static PolygonMesh Moved(PolygonMesh mesh, Vector3 by) =>
        TestMeshes.Make(mesh.Vertices.Select(v => v + by), mesh.Faces.Select(f => f.Corners.ToArray())).WithShade(mesh.Shade);

    private static MorphTarget Target(PolygonMesh mesh, string name, Func<int, Vector3, Vector3?> moved)
    {
        var positions = new Dictionary<int, Vector3>();
        for (var v = 0; v < mesh.Vertices.Count; v++)
            if (moved(v, mesh.Vertices[v]) is { } p) positions[v] = p;
        var result = MorphTarget.FromPositions(mesh, name, positions);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.Target!;
    }

    /// <summary>A cube with Bulk (every vertex out from the centre) and Lift (the top four up, so its side faces bend).</summary>
    private static PolygonMesh Cube(bool smooth, Vector3 at)
    {
        var mesh = Moved(TestMeshes.Cube(), at);
        if (smooth) mesh = Smooth(mesh);
        return mesh.WithTargets(MorphTargets.Of([
            Target(mesh, "Bulk", (_, p) => at + ((p - at) * 1.25f)),
            Target(mesh, "Lift", (v, p) => p.Y > at.Y ? p + new Vector3(0.1f * v, 0.3f, 0) : null),
        ]));
    }

    /// <summary>A flat grid whose middle row rises under Bend: coplanar faces at rest, bent at weight 1.</summary>
    private static PolygonMesh Grid(bool smooth, Vector3 at)
    {
        var mesh = Moved(TestMeshes.Grid(3, 3), at);
        if (smooth) mesh = Smooth(mesh);
        var mid = mesh.Vertices.Select(v => v.X).Distinct().Order().ElementAt(1);
        return mesh.WithTargets(MorphTargets.Of([
            Target(mesh, "Bulk", (_, p) => p.X == mid ? p + new Vector3(0, 0, 0.2f) : null),
            Target(mesh, "Bend", (_, p) => p.X > mid ? p + new Vector3(0, 0.15f, 0.4f) : null),
        ]));
    }

    /// <summary>The cube and a grid beside it, sharing the name Bulk; the grid alone has Bend, the cube alone Lift.</summary>
    private static Model Pair(bool smooth) =>
        Model.Of("cube", Cube(smooth, Vector3.Zero)).With("grid", Grid(smooth, new Vector3(3, 0, 0)));

    private static byte[] Export(Model model, bool skinned) =>
        skinned ? SkinnedExport.Write(model.WithSkeleton(Rig()), "pair").Bytes : GlbExport.Write(model.Combined, Shading.Triangles(model.Combined!), "pair", model.Materials);

    /// <summary>
    /// Every vertex the loader gives, checked against the Carve vertex at its position (positions are unique in these
    /// models) and the Carve face of each triangle it is a corner of: the position delta is the vertex's, the normal
    /// delta its corner's normal at weight 1 less at rest, for every target name, zero where the mesh lacks the name.
    /// </summary>
    private static void AssertDeltas(Model model, ParsedModel parsed)
    {
        Vector3[] positions, normals;
        uint[] indices;
        if (parsed.Skin is { } skin)
        {
            positions = [.. skin.SkinnedMeshData.Vertices.Select(v => v.Position)];
            normals = [.. skin.SkinnedMeshData.Vertices.Select(v => v.Normal)];
            indices = [.. skin.SkinnedMeshData.Indices!.Select(i => (uint)i)];
        }
        else
        {
            positions = [.. parsed.MeshData.Vertices.Select(v => v.Position)];
            normals = [.. parsed.MeshData.Vertices.Select(v => v.Normal)];
            indices = [.. parsed.MeshData.Indices!.Select(i => (uint)i)];
        }
        var targets = parsed.MorphTargets!;
        var names = model.TargetNames;

        var at = new Dictionary<Vector3, (PolygonMesh Mesh, int Vertex)>();
        foreach (var (_, mesh) in model.Meshes)
            foreach (var v in mesh.LiveVertices) at.Add(mesh.Vertices[v], (mesh, v));

        Assert.All(targets.PositionDeltas, d => Assert.Equal(positions.Length, d.Length));
        Assert.All(targets.NormalDeltas, d => Assert.Equal(positions.Length, d.Length));
        var seen = new bool[positions.Length];
        for (var i = 0; i < indices.Length; i += 3)
        {
            var corners = new[] { (int)indices[i], (int)indices[i + 1], (int)indices[i + 2] };
            var carve = corners.Select(c => at[positions[c]]).ToArray();
            var mesh = carve[0].Mesh;
            var face = mesh.LiveFaces.Single(f => carve.All(c => mesh.Faces[f].Corners.Contains(c.Vertex)));
            for (var k = 0; k < 3; k++)
            {
                var (index, vertex) = (corners[k], carve[k].Vertex);
                var corner = mesh.Faces[face].CornerOf(vertex);
                var rest = Shading.Corners(mesh)[face][corner];
                Assert.Equal(rest, normals[index]);
                seen[index] = true;
                for (var t = 0; t < names.Count; t++)
                {
                    var target = mesh.Targets?.Find(names[t]);
                    var position = target?.DeltaOf(vertex) ?? Vector3.Zero;
                    var normal = target is null ? Vector3.Zero
                        : Shading.Corners(mesh.Morphed(new Dictionary<string, float> { [names[t]] = 1f }))[face][corner] - rest;
                    Assert.True(position == targets.PositionDeltas[t][index],
                        $"{names[t]} position delta of vertex {index} (Carve {vertex}): {targets.PositionDeltas[t][index]}, Carve's {position}");
                    Assert.True(normal == targets.NormalDeltas[t][index],
                        $"{names[t]} normal delta of vertex {index} (Carve {vertex}, face {face}): {targets.NormalDeltas[t][index]}, Carve's {normal}");
                }
            }
        }
        Assert.All(seen, Assert.True);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Every_delta_the_loader_reads_is_Carves(bool smooth, bool skinned)
    {
        var model = Pair(smooth);
        var parsed = Parse(Export(model, skinned));

        Assert.Empty(parsed.Warnings);
        Assert.NotNull(parsed.MorphTargets);
        Assert.Equal(["Bulk", "Lift", "Bend"], parsed.MorphTargets.TargetNames);
        Assert.Equal([0f, 0f, 0f], parsed.MorphTargets.DefaultWeights);
        AssertDeltas(model, parsed);
    }

    [Fact]
    public void A_name_on_two_meshes_is_one_target_and_each_mesh_moves_by_its_own()
    {
        var model = Pair(smooth: false);
        var parsed = Parse(Export(model, skinned: false));
        var bulk = parsed.MorphTargets!.PositionDeltas[0];
        var positions = parsed.MeshData.Vertices.Select(v => v.Position).ToArray();

        Assert.Single(parsed.MorphTargets.TargetNames, n => n == "Bulk");
        Assert.Contains(Enumerable.Range(0, positions.Length), i => positions[i].X < 2 && bulk[i] != Vector3.Zero);
        Assert.Contains(Enumerable.Range(0, positions.Length), i => positions[i].X > 2 && bulk[i] != Vector3.Zero);
    }

    [Fact]
    public void A_bent_flat_face_keeps_its_own_corners_apart_from_its_coplanar_neighbour()
    {
        var grid = Grid(smooth: false, Vector3.Zero);
        var model = Model.Of("grid", grid);
        var parsed = Parse(Export(model, skinned: false));
        var untargeted = Parse(GlbExport.Write(grid.WithTargets(null), Shading.Triangles(grid), "pair", []));

        // At rest the coplanar faces share their corners; once a target bends them apart they cannot.
        Assert.True(parsed.MeshData.Vertices.Length > untargeted.MeshData.Vertices.Length);
        AssertDeltas(model, parsed);
    }

    [Fact]
    public void A_piece_writes_the_targets_of_its_own_meshes()
    {
        var model = Pair(smooth: true).WithSkeleton(Rig());
        var parsed = Parse(PieceExport.Write(model, ["grid"], "grid").Bytes);

        Assert.Empty(parsed.Warnings);
        Assert.Equal(["Bulk", "Bend"], parsed.MorphTargets!.TargetNames);
        AssertDeltas(PieceExport.Select(model, ["grid"]), parsed);
    }

    [Fact]
    public void Clips_alone_write_no_targets()
    {
        var rig = Rig();
        var lift = RigTestHelpers.MakeClip("lift", 1f, rig,
            new BoneChannelsSpec("Root", null, new ChannelSpec<Vector3>("LINEAR", [new(0f, Vector3.Zero), new(1f, Vector3.UnitY)])));
        var model = Pair(smooth: false).WithSkeleton(rig).WithClips([lift]);
        var root = SharpGLTF.Schema2.ModelRoot.ParseGLB(ClipsExport.Write(model, "clips"));

        // The clips file has no mesh, so nothing to carry a target, and the engine's loader refuses a file with no mesh.
        Assert.Empty(root.LogicalMeshes);
        Assert.Single(root.LogicalAnimations);
    }

    /// <summary>
    /// A model without targets writes the bytes it wrote before T-104: the hashes were taken from the export at
    /// <c>origin/milestone/M-21</c> before the change (2ba26aa), static and skinned, flat and smooth.
    /// </summary>
    [Theory]
    [InlineData(false, false, "0FC832F52A46F63FB8B3119F7E473E02AE9AD21A8BC25F66E31EC99331DFB35D")]
    [InlineData(true, false, "597E30F23E86720D027F9E8756B0106FB0ADC4DC1A402E8F65D75BDC03FEB2A9")]
    [InlineData(false, true, "F527E9650532A8E6657F9AC846744DEB202B4B4F2C884209F7E674FA37865164")]
    [InlineData(true, true, "0B82829DF70AD0873A4B397D2CDD0FB327CD4E353416677B35FDB17149AFA44D")]
    public void A_model_without_targets_exports_the_bytes_it_did(bool smooth, bool skinned, string sha256)
    {
        var model = Model.Of("cube", Cube(smooth, Vector3.Zero).WithTargets(null)).With("grid", Grid(smooth, new Vector3(3, 0, 0)).WithTargets(null));
        var bytes = Export(model, skinned);

        Assert.Null(Parse(bytes).MorphTargets);
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.True(actual == sha256, $"the export hashes to {actual}");
    }
}

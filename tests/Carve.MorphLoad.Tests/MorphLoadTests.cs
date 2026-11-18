using System.Numerics;
using Carve.Core;
using Carve.LoadChecks;
using Empyrean;
using Xunit;
using Model = Carve.Core.Model;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.MorphLoad.Tests;

/// <summary>
/// The arm in a game (M-21, T-106), read as the game reads it: the last step of suite <c>scenario-morph</c>. The run folder comes from
/// <c>CARVE_MORPH_RUN</c>, which <c>scripts/run-morph-scenario.ps1</c> sets; with it unset every test is skipped (the script fails a
/// run that skipped one). <c>game/Content/models/arm.glb</c> is read by <c>IModelGeometryLoader</c> with the content folder mounted.
/// </summary>
[Trait("Tier", "regression")]
public class MorphLoadTests
{
    private static readonly string? Run = Environment.GetEnvironmentVariable("CARVE_MORPH_RUN");

    private static string RunFolder => Run ?? throw new InvalidOperationException("CARVE_MORPH_RUN is unset");

    private static ParsedModel Loaded() => RunContent.Loader(RunFolder).Load(new ContentPath("models/arm.glb"));

    private static Model Built() => RunContent.Saved(Path.Combine(RunFolder, "work", "arm.carve"));

    [SkippableOrRun]
    public void The_arm_loads_with_its_dials_at_zero_and_no_warnings()
    {
        var model = Loaded();
        Assert.Empty(model.Warnings);
        Assert.Equal(["Bulk", "Lean"], model.MorphTargets!.TargetNames);
        Assert.Equal(2, model.MorphTargets.DefaultWeights.Length);
        Assert.All(model.MorphTargets.DefaultWeights, w => Assert.Equal(0f, w));
    }

    [SkippableOrRun]
    public void The_arm_is_skinned_with_its_three_bones_and_Curl()
    {
        var skin = Loaded().Skin!;
        Assert.Equal(["Elbow", "Shoulder", "Wrist"], skin.Skeleton.Bones.Select(b => b.Name).Order().ToArray());
        Assert.Equal(["Curl"], skin.Animations.Select(a => a.Name));
    }

    /// <summary>
    /// Every vertex the loader gives, against the Carve vertex at its position (unique in the arm) and the Carve face of each triangle it
    /// is a corner of: the position delta is the vertex's, the normal delta its corner's normal at weight 1 less at rest, exactly.
    /// </summary>
    [SkippableOrRun]
    public void Every_position_and_normal_delta_equals_Carves()
    {
        var parsed = Loaded();
        var model = Built();
        var skin = parsed.Skin!;
        var positions = skin.SkinnedMeshData.Vertices.Select(v => v.Position).ToArray();
        var normals = skin.SkinnedMeshData.Vertices.Select(v => v.Normal).ToArray();
        var indices = skin.SkinnedMeshData.Indices!.Select(i => (uint)i).ToArray();
        var targets = parsed.MorphTargets!;
        var names = model.TargetNames;
        Assert.Equal(names, targets.TargetNames);

        var at = new Dictionary<Vector3, (PolygonMesh Mesh, int Vertex)>();
        foreach (var (_, mesh) in model.Meshes)
        {
            foreach (var v in mesh.LiveVertices)
            {
                at.Add(mesh.Vertices[v], (mesh, v));
            }
        }

        Assert.All(targets.PositionDeltas, d => Assert.Equal(positions.Length, d.Length));
        Assert.All(targets.NormalDeltas, d => Assert.Equal(positions.Length, d.Length));
        var seen = new bool[positions.Length];
        var moved = 0;
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
                    Assert.True(position == targets.PositionDeltas[t][index], $"{names[t]} position delta of vertex {index} (Carve {vertex}): {targets.PositionDeltas[t][index]}, Carve's {position}");
                    Assert.True(normal == targets.NormalDeltas[t][index], $"{names[t]} normal delta of vertex {index} (Carve {vertex}, face {face}): {targets.NormalDeltas[t][index]}, Carve's {normal}");
                    if (position != Vector3.Zero)
                    {
                        moved++;
                    }
                }
            }
        }

        Assert.All(seen, Assert.True);
        Assert.True(moved > 0, "no delta moved anything: the check proves nothing");
    }

    [SkippableOrRun]
    public void The_export_is_clean()
    {
        var result = File.ReadAllText(Path.Combine(RunFolder, "work", "export-arm.glb.txt"));
        // The smoothed quads are slightly off their planes, which the export says once per face; nothing else may be said.
        Assert.DoesNotContain("replaced", result);
        if (result.Contains("warning(s): "))
        {
            Assert.All(result.Split("warning(s): ")[1].Split("; "), w => Assert.Contains("planar", w));
        }
    }

    [SkippableOrRun]
    public void The_sample_is_the_arm_the_tools_built_by_content()
    {
        // Compared by content, not bytes: each is read, then written the one way, which gives floats back bit for bit.
        var sample = CarveFile.Write(RunContent.Saved(RunContent.SamplePath("arm.carve")));
        Assert.Equal(CarveFile.Write(Built()), sample);
        var lean = CarveFile.Write(RunContent.Saved(RunContent.SamplePath("arm-lean.carve")));
        Assert.Equal(CarveFile.Write(RunContent.Saved(Path.Combine(RunFolder, "work", "arm-lean.carve"))), lean);
    }

    [SkippableOrRun]
    public void Every_render_glb_frame_passes_the_frame_rule_and_the_control_fails_it()
    {
        var rows = File.ReadAllLines(Path.Combine(RunFolder, "differences.tsv")).Skip(1).Select(l => l.Split('\t')).ToArray();
        var good = rows.Where(r => r[1] == "good").ToArray();
        Assert.Equal(3, good.Length);
        Assert.All(good, r => Assert.Equal("False", r[5]));
        var control = Assert.Single(rows, r => r[1] == "control");
        Assert.Equal("True", control[5]);
    }
}

/// <summary>A fact that is skipped when the run folder is not given.</summary>
public sealed class SkippableOrRunAttribute : FactAttribute
{
    public SkippableOrRunAttribute()
    {
        if (Environment.GetEnvironmentVariable("CARVE_MORPH_RUN") is null)
        {
            Skip = "CARVE_MORPH_RUN is unset";
        }
    }
}

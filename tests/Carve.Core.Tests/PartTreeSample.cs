using System.Numerics;

namespace Carve.Core.Tests;

/// <summary>T-92's small part tree, shared by the Core's file tests and the server's save and open tests (linked there).</summary>
public static class PartTreeSample
{
    public static PolygonMesh Big() => TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 2), TestMeshes.CubeFaces);

    /// <summary>
    /// A small tree: Body (a cube, envelope, levels 0 and 1, the live mesh level 1's), Arm (a cube under Body, placed, turned and
    /// moving), Empty (under Body), Copy (an instance of Arm under Body). Parts in pre-order: Body, Arm, Empty, Copy.
    /// </summary>
    public static Model Tree()
    {
        var model = Model.Of("Body", TestMeshes.Cube());
        model = model.WithPart(model.FindPart("Body")! with { Envelope = new PartEnvelope(new(-1, -1, -1), new(3, 3, 3)) })
            .WithLevel("Body", 0)
            .With("Body", Big())
            .WithLevel("Body", 1)
            .With("Arm", TestMeshes.Cube())
            .WithPart(new Part("Arm") { Parent = "Body", Translation = new(0.5f, 1.25f, -0.1f), Rotation = new(0, 0.70710677f, 0, 0.70710677f), Scale = new(1, 2, 0.5f), Moves = true })
            .WithPart(new Part("Empty") { Parent = "Body", Translation = new(0, 2, 0) })
            .WithPart(new Part("Copy") { Parent = "Body", Translation = new(0, 0, 3), InstanceOf = "Arm" });
        return model;
    }
    /// <summary>A Bulge target on a mesh: vertex 0 moved up, vertex 6 out (T-132).</summary>
    public static MorphTargets Bulge(string name = "Bulge") =>
        MorphTargets.Of([MorphTarget.Of(name, new Dictionary<int, Vector3> { [0] = new(0, 0.25f, 0), [6] = new(0.1f, 0, -0.30000001f) })])!;

    /// <summary>
    /// <see cref="Tree"/> with morph targets (T-132): Body's live mesh and its level 1 keep a Bulge (level 0 has none), and Arm's
    /// live mesh has a Wave; the version 9 sample.
    /// </summary>
    public static Model TargetedTree()
    {
        var model = Tree();
        model = model.With("Body", model.Find("Body")!.WithTargets(Bulge())).WithLevel("Body", 1);
        return model.With("Arm", model.Find("Arm")!.WithTargets(Bulge("Wave")));
    }
}

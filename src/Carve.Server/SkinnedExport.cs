using System.Globalization;
using System.Numerics;
using Carve.Core;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;

namespace Carve.Server;

/// <summary>What a skinned export wrote, for the result line.</summary>
public sealed record SkinnedGlb(
    byte[] Bytes, IReadOnlyList<string> Pieces, IReadOnlyList<string> Materials, int Triangles,
    IReadOnlyList<string> Warnings, int BoundToRoot);

/// <summary>
/// A model with a skeleton as one self-contained binary glTF (D-6, A skinned export is one glTF mesh; The rest pose is
/// the bind pose; Export modes, mode <c>model</c>). In the default scene, in this node order: <c>Armature</c> and the
/// bones under it (<see cref="GlbRig.WriteArmature"/>), then one node named for the file, identity transform, holding
/// the one glTF mesh (also named for the file) and <c>skins[0]</c>. The mesh has one primitive per piece and colour:
/// the model's meshes in order, and within a piece one primitive per colour in order of first use by face, the
/// uncoloured faces counting as one colour. Each primitive is indexed triangles of <c>POSITION</c>, <c>NORMAL</c>
/// (flat, the face's own), <c>JOINTS_0</c> and <c>WEIGHTS_0</c>, positions the rest pose in model space, and has a
/// material of its own: <c>&lt;mesh&gt;</c> for uncoloured faces, <c>&lt;mesh&gt;:&lt;group&gt;</c> when a face group
/// holds exactly the primitive's faces (the first such group), else <c>&lt;mesh&gt;:&lt;rrggbb&gt;</c>, which a
/// primitive also takes when its group's name is another primitive's <c>&lt;rrggbb&gt;</c>, so no two share a name; each opaque,
/// metallic 0, roughness 1, base colour as the static export's. Then one animation per clip (<see cref="GlbRig.WriteClips"/>).
/// The same model always writes the same bytes.
/// </summary>
public static class SkinnedExport
{
    // Interim until engine need 4: a skinned file keeps only its first skinned mesh, so every piece is a primitive of one mesh.

    // D-6, Warnings for a game's limits: Orcward's numbers, not general ones; T-51 makes them per game.

    /// <summary>D-6, Warnings for a game's limits: Orcward's starting numbers.</summary>
    public const int MaxTriangles = 5000;

    public const int MaxSubmeshes = 8;

    public const float MinHeight = 1.4f;

    public const float MaxHeight = 2.4f;

    /// <summary>
    /// The bones the engine's Synty preset needs for its required roles (hips, spine root, head, both arms to the hand,
    /// both legs to the foot), from Empyrean.Animation 0.119.0's <c>HumanoidRoleMap.Synty</c> and
    /// <c>HumanoidRoles.Required</c>. Interim until engine need 6: the docs name the preset's bones but not which bone
    /// fills each role, so Head and the arm roles (upper arm Shoulder_*, lower arm Elbow_*) are read from Synty's rig.
    /// </summary>
    public static readonly IReadOnlyList<string> SyntyRequiredBones =
    [
        "Hips", "Spine_01", "Head",
        "Shoulder_L", "Elbow_L", "Hand_L", "Shoulder_R", "Elbow_R", "Hand_R",
        "UpperLeg_L", "LowerLeg_L", "Ankle_L", "UpperLeg_R", "LowerLeg_R", "Ankle_R",
    ];

    /// <summary>Every vertex with no weight, per mesh, in mesh order; empty when every vertex has one.</summary>
    public static IReadOnlyList<(string Mesh, IReadOnlyList<int> Vertices)> Unweighted(Model model) =>
        [.. model.Meshes
            .Select(m => (m.Name, (IReadOnlyList<int>)[.. Enumerable.Range(0, m.Mesh.Vertices.Count).Where(v => m.Mesh.Weights?.Has(v) != true)]))
            .Where(m => m.Item2.Count > 0)];

    /// <summary>
    /// Writes the model, which must have a skeleton and a mesh. Vertices with no weight are bound wholly to the first
    /// root bone (bone 0, since parents come first); the caller refuses them unless the agent allowed it.
    /// </summary>
    public static SkinnedGlb Write(Model model, string name)
    {
        var skeleton = model.Skeleton ?? throw new ArgumentException("the model has no skeleton", nameof(model));
        var (root, scene, joints, skin) = GlbRig.Start(name, skeleton);

        var builder = new MeshBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexJoints4>(name);
        var materials = new List<string>();
        var triangleCount = 0;
        var boundToRoot = 0;
        foreach (var piece in model.Meshes)
        {
            var mesh = piece.Mesh;
            var triangles = Triangulator.Triangulate(mesh);
            var corners = Shading.CornersOf(mesh);
            var primitives = new Dictionary<string, PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexJoints4>>();
            foreach (var (materialName, colour) in Materials(piece))
            {
                primitives[colour ?? ""] = builder.UsePrimitive(GlbExport.Material(materialName, colour));
                materials.Add(materialName);
            }

            foreach (var t in triangles)
            {
                var loop = mesh.Faces[t.Face];
                var at = corners[t.Face];
                var primitive = primitives[GlbExport.ColourOf(mesh, t.Face) ?? ""];
                primitive.AddTriangle(Vertex(mesh, t.A, at[Corner(loop, t.A)]), Vertex(mesh, t.B, at[Corner(loop, t.B)]), Vertex(mesh, t.C, at[Corner(loop, t.C)]));
            }
            triangleCount += triangles.Count;
            boundToRoot += Enumerable.Range(0, mesh.Vertices.Count).Count(v => mesh.Weights?.Has(v) != true);
        }

        var meshNode = scene.CreateNode(name);
        meshNode.Mesh = root.CreateMesh(builder);
        meshNode.Skin = skin;
        GlbRig.WriteClips(root, skeleton, joints, model.Clips);

        var pieces = model.Names.ToList();
        return new SkinnedGlb(root.WriteGLB().ToArray(), pieces, materials, triangleCount,
            Warnings(model, triangleCount, materials.Count), boundToRoot);
    }

    /// <summary>The warnings of D-6's game limits, each past its constant and never at it.</summary>
    public static IReadOnlyList<string> Warnings(Model model, int triangles, int submeshes)
    {
        static string N(int x) => x.ToString(CultureInfo.InvariantCulture);
        static string F(float x) => x.ToString(CultureInfo.InvariantCulture);
        var warnings = new List<string>();
        if (triangles > MaxTriangles)
            warnings.Add($"{N(triangles)} triangles, above the {N(MaxTriangles)} a game character should keep to");
        var colours = model.Meshes.SelectMany(m => m.Mesh.Style?.Colours.Values.Select(c => c.ToUpperInvariant()) ?? []).Distinct().Count();
        if (submeshes > MaxSubmeshes || colours > MaxSubmeshes)
            warnings.Add($"{N(submeshes)} submeshes and {N(colours)} colours, above the {N(MaxSubmeshes)} a game character should keep to; share colours between pieces where you can");
        if (model.Skeleton is { } skeleton && SyntyRequiredBones.All(skeleton.Names.Contains))
        {
            var ys = model.Meshes.SelectMany(m => m.Mesh.Vertices).Select(v => v.Y).ToList();
            if (ys.Count > 0)
            {
                var height = (float)((double)ys.Max() - ys.Min());
                if (height < MinHeight || height > MaxHeight)
                    warnings.Add($"the rest pose is {F(height)} m tall, outside the {F(MinHeight)} to {F(MaxHeight)} m a humanoid on the Synty bones should be");
            }
        }
        return warnings;
    }

    /// <summary>The piece's primitives in order of first use by face: (material name, colour or null for uncoloured).</summary>
    public static List<(string Name, string? Colour)> Materials(NamedMesh piece)
    {
        var mesh = piece.Mesh;
        var order = new List<string?>();
        var faces = new Dictionary<string, List<int>>();
        for (var f = 0; f < mesh.Faces.Count; f++)
        {
            var colour = GlbExport.ColourOf(mesh, f);
            if (!faces.TryGetValue(colour ?? "", out var list))
            {
                faces[colour ?? ""] = list = [];
                order.Add(colour);
            }
            list.Add(f);
        }

        // A group name that is another primitive's colour would give two primitives one name; that primitive keeps its
        // colour name instead. Colours are unique within the piece and mesh names hold no ':', so the names are unique in the file.
        var hexes = order.Where(c => c is not null).Select(c => Hex(c!)).ToList();
        return [.. order.Select(c => (c is null ? piece.Name : $"{piece.Name}:{UnclashedGroup(mesh, faces[c], Hex(c), hexes) ?? Hex(c)}", c))];
    }

    private static string Hex(string colour) => colour.TrimStart('#').ToLowerInvariant();

    private static string? UnclashedGroup(PolygonMesh mesh, List<int> faces, string own, List<string> hexes) =>
        GroupFor(mesh, faces) is { } group && (group == own || !hexes.Contains(group)) ? group : null;

    private static string? GroupFor(PolygonMesh mesh, List<int> faces) =>
        mesh.Style?.Groups.FirstOrDefault(g => g.Faces.Count == faces.Count && g.Faces.Order().SequenceEqual(faces)).Name;

    private static int Corner(IReadOnlyList<int> loop, int vertex)
    {
        for (var i = 0; i < loop.Count; i++) if (loop[i] == vertex) return i;
        throw new ArgumentException("the vertex is not in the face", nameof(vertex));
    }

    private static VertexBuilder<VertexPositionNormal, VertexEmpty, VertexJoints4> Vertex(PolygonMesh mesh, int vertex, Vector3 normal) =>
        new(new VertexPositionNormal(mesh.Vertices[vertex], normal), default(VertexEmpty), Joints(mesh.Weights?.Of(vertex) ?? []));

    /// <summary>
    /// The vertex's influences as JOINTS_0/WEIGHTS_0, narrowed to float with the rounding put on the largest so the four
    /// sum to 1 as nearly as float allows; none binds the vertex wholly to bone 0, the first root bone.
    /// </summary>
    public static VertexJoints4 Joints(IReadOnlyList<Influence> influences)
    {
        if (influences.Count == 0) return new VertexJoints4((0, 1f));
        var weights = influences.Select(i => (float)i.Weight).ToArray();
        var largest = 0;
        for (var i = 1; i < weights.Length; i++) if (weights[i] > weights[largest]) largest = i;
        var rest = 0.0;
        for (var i = 0; i < weights.Length; i++) if (i != largest) rest += weights[i];
        weights[largest] = (float)(1.0 - rest);
        return new VertexJoints4([.. influences.Select((inf, i) => (inf.Bone, weights[i]))]);
    }
}

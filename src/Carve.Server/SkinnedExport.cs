using System.Numerics;
using Carve.Core;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;
using CarveMaterial = Carve.Core.Material;

namespace Carve.Server;

/// <summary>What a skinned export wrote, for the result line.</summary>
public sealed record SkinnedGlb(
    byte[] Bytes, IReadOnlyList<string> Pieces, IReadOnlyList<string> Materials, int Triangles,
    int BoundToRoot, int Primitives = 0);

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

    /// <summary>
    /// T-95 (the person's ruling, M-9 planning): the model a skinned export writes. Each part with geometry becomes a mesh of
    /// its name placed at its world transform (its weights kept), each instance a copy of its source under the instance's
    /// name, in pre-order; empties write nothing. A model whose parts all draw their own mesh at the identity, in mesh order,
    /// is returned as it is, so its bytes are today's.
    /// </summary>
    public static Model Baked(Model model)
    {
        var drawn = model.Parts.Where(p => model.Shown(p) is not null).ToList();
        if (drawn.All(p => p.InstanceOf is null && model.WorldTransform(p.Name)!.Value.IsIdentity)
            && drawn.Select(p => p.Name).SequenceEqual(model.Names))
            return model;
        var meshes = drawn.Select(p =>
        {
            var mesh = model.Shown(p)!;
            var placed = Model.Join([(mesh, model.WorldTransform(p.Name)!.Value)])!;
            return new NamedMesh(p.Name, ReferenceEquals(placed, mesh) ? mesh : placed.WithWeights(mesh.Weights));
        }).ToList();
        var (baked, errors) = Model.Create(meshes, model.Skeleton, model.Clips, model.Materials);
        return baked ?? throw new InvalidOperationException(string.Join("; ", errors.Select(e => e.ToString())));
    }

    /// <summary>The warning a skinned export gives when parts are marked moves, naming them; null when none is.</summary>
    public static string? MovesWarning(Model model) =>
        model.Parts.Where(p => p.Moves).Select(p => p.Name).ToList() is { Count: > 0 } moving
            ? $"moves has no effect on a skinned export, which bakes every part into the skinned mesh: {NameList.Quoted(moving)}"
            : null;

    /// <summary>Every vertex with no weight, per mesh, in mesh order; empty when every vertex has one.</summary>
    public static IReadOnlyList<(string Mesh, IReadOnlyList<int> Vertices)> Unweighted(Model model) =>
        [.. model.Meshes
            .Select(m => (m.Name, (IReadOnlyList<int>)[.. m.Mesh.LiveVertices.Where(v => m.Mesh.Weights?.Has(v) != true)]))
            .Where(m => m.Item2.Count > 0)];

    /// <summary>
    /// Writes the model, which must have a skeleton and a mesh. Vertices with no weight are bound wholly to the first
    /// root bone (bone 0, since parents come first); the caller refuses them unless the agent allowed it.
    /// </summary>
    public static SkinnedGlb Write(Model model, string name)
    {
        if (model.Skeleton is null) throw new ArgumentException("the model has no skeleton", nameof(model));
        var uvs = model.Meshes.Any(m => GlbExport.HasUvs(m.Mesh))
            || GlbExport.Textured(GlbExport.UsedBy(model.Meshes.Select(m => m.Mesh), model.Materials));
        if (GlbTargets.For(model.TargetNames) is { } targets)
            return uvs
                ? Write<Tagged<VertexTexture1>>(model, name, (m, f, v, n, p) => new(new VertexTexture1(GlbExport.UvAt(m, f, v)), targets.Tag(p!, f, v, n)), targets)
                : Write<Tagged<VertexEmpty>>(model, name, (_, f, v, n, p) => new(default, targets.Tag(p!, f, v, n)), targets);
        return uvs
            ? Write<VertexTexture1>(model, name, (m, f, v, _, _) => new VertexTexture1(GlbExport.UvAt(m, f, v)), null)
            : Write<VertexEmpty>(model, name, (_, _, _, _, _) => default, null);
    }

    private static SkinnedGlb Write<TvM>(Model model, string name, Func<PolygonMesh, int, int, Vector3, GlbTargets.Piece?, TvM> texture, GlbTargets? targets)
        where TvM : struct, IVertexMaterial
    {
        var skeleton = model.Skeleton!;
        var (root, scene, joints, skin) = GlbRig.Start(name, skeleton);
        var builder = new MeshBuilder<MaterialBuilder, VertexPositionNormal, TvM, VertexJoints4>(name);
        var materials = new List<string>();
        var primitiveCount = 0;
        var images = new GlbExport.Images();
        var shared = new Dictionary<string, MaterialBuilder>();
        var taken = model.Meshes.SelectMany(m => Model.MaterialsUsedBy(m.Mesh)).ToHashSet();
        var triangleCount = 0;
        var boundToRoot = 0;
        foreach (var piece in model.Meshes)
        {
            var mesh = piece.Mesh;
            var triangles = Triangulator.Triangulate(mesh);
            var corners = Shading.CornersOf(mesh);
            var deltas = targets?.Of(mesh);
            var primitives = new Dictionary<string, PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, TvM, VertexJoints4>>();
            foreach (var p in Primitives(piece, taken))
            {
                if (p.Material is { } m)
                {
                    if (!shared.TryGetValue(m, out var builderOf))
                    {
                        shared[m] = builderOf = GlbExport.Material(model.FindMaterial(m)!, images);
                        materials.Add(m);
                        primitiveCount++; // the mesh builder keeps one primitive per material, so a second piece's faces join the first's
                    }
                    primitives["m" + m] = builder.UsePrimitive(builderOf);
                }
                else
                {
                    primitives["c" + (p.Colour ?? "")] = builder.UsePrimitive(GlbExport.Material(p.Name, p.Colour));
                    materials.Add(p.Name);
                    primitiveCount++;
                }
            }

            foreach (var t in triangles)
            {
                var face = mesh.Faces[t.Face];
                var at = corners[t.Face];
                var primitive = primitives[GlbExport.KeyOf(mesh, t.Face)];
                var (na, nb, nc) = (at[face.CornerOf(t.A)], at[face.CornerOf(t.B)], at[face.CornerOf(t.C)]);
                primitive.AddTriangle(
                    Vertex(mesh, t.A, na, texture(mesh, t.Face, t.A, na, deltas)),
                    Vertex(mesh, t.B, nb, texture(mesh, t.Face, t.B, nb, deltas)),
                    Vertex(mesh, t.C, nc, texture(mesh, t.Face, t.C, nc, deltas)));
            }
            triangleCount += triangles.Count;
            boundToRoot += mesh.LiveVertices.Count(v => mesh.Weights?.Has(v) != true);
        }

        var meshNode = scene.CreateNode(name);
        meshNode.Mesh = root.CreateMesh(builder);
        targets?.Write(root, meshNode.Mesh, GlbTargets.TagsOf(builder.Primitives));
        meshNode.Skin = skin;
        GlbRig.WriteClips(root, skeleton, joints, model.Clips);

        var pieces = model.Names.ToList();
        return new SkinnedGlb(root.WriteGLB().ToArray(), pieces, materials, triangleCount, boundToRoot, primitiveCount);
    }

    /// <summary>The piece's primitives in order of first use by face: (material name, colour or null for uncoloured). Material faces are not in it; see <see cref="Primitives"/>.</summary>
    public static List<(string Name, string? Colour)> Materials(NamedMesh piece) =>
        [.. Primitives(piece, new HashSet<string>()).Where(p => p.Material is null).Select(p => (p.Name, p.Colour))];

    /// <summary>One primitive of a piece: its glTF material's name, its colour (null: uncoloured), and the Carve material's name when it has one.</summary>
    internal sealed record Primitive(string Name, string? Colour, string? Material);

    /// <summary>
    /// The piece's primitives: the colours and the uncoloured faces in order of first use by face, then each Carve material
    /// in order of first use, named for the material. The uncoloured primitive is named for the bare mesh, and when that is
    /// also the name of a material used in the model (<paramref name="taken"/>) it is <c>&lt;mesh&gt;:uncoloured</c> instead
    /// (with <c>_</c> added until no group of the piece has that name), since a material's name holds no ':'.
    /// </summary>
    internal static List<Primitive> Primitives(NamedMesh piece, IReadOnlySet<string> taken)
    {
        var mesh = piece.Mesh;
        var order = new List<string?>();
        var faces = new Dictionary<string, List<int>>();
        var materials = new List<string>();
        foreach (var f in mesh.LiveFaces)
        {
            if (mesh.Style?.MaterialOf(f) is { } material)
            {
                if (!materials.Contains(material)) materials.Add(material);
                continue;
            }
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
        var bare = piece.Name;
        if (taken.Contains(bare))
        {
            var groups = mesh.Style?.Groups.Select(g => g.Name).ToHashSet() ?? [];
            var suffix = "uncoloured";
            while (groups.Contains(suffix) || hexes.Contains(suffix)) suffix += "_";
            bare = $"{piece.Name}:{suffix}";
        }
        var result = order.Select(c => new Primitive(c is null ? bare : $"{piece.Name}:{UnclashedGroup(mesh, faces[c], Hex(c), hexes) ?? Hex(c)}", c, null)).ToList();
        result.AddRange(materials.Select(m => new Primitive(m, null, m)));
        return result;
    }

    private static string Hex(string colour) => colour.TrimStart('#').ToLowerInvariant();

    private static string? UnclashedGroup(PolygonMesh mesh, List<int> faces, string own, List<string> hexes) =>
        GroupFor(mesh, faces) is { } group && (group == own || !hexes.Contains(group)) ? group : null;

    private static string? GroupFor(PolygonMesh mesh, List<int> faces) =>
        mesh.Style?.Groups.FirstOrDefault(g => g.Faces.Count == faces.Count && g.Faces.Order().SequenceEqual(faces)).Name;

    private static VertexBuilder<VertexPositionNormal, TvM, VertexJoints4> Vertex<TvM>(PolygonMesh mesh, int vertex, Vector3 normal, TvM texture)
        where TvM : struct, IVertexMaterial =>
        new(new VertexPositionNormal(mesh.Vertices[vertex], normal), texture, Joints(mesh.Weights?.Of(vertex) ?? []));

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

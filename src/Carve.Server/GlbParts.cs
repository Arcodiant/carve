using System.Globalization;
using System.Numerics;
using Carve.Core;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;

namespace Carve.Server;

/// <summary>
/// T-95: the static <c>.glb</c> of a model with a part marked <c>moves</c> (M-9, Export, the person's ruling of 2026-11-17).
/// The root node is named after the file, at the identity, and holds every static part whose nearest moving ancestor is none,
/// merged, each part's transform to the root folded into its vertices and normals (static instances copied in). Each moving
/// part is a child node named after it, under its nearest moving ancestor or else the root, siblings in pre-order (the
/// person, 2026-11-18). Its transform is its own composed with every static ancestor's up to that node, and its mesh holds
/// its own geometry plus its static descendants', folded the same way. A moving instance's node references the one glTF mesh
/// of its source's own geometry, shared by every moving instance (the source's own node's mesh when that holds nothing
/// else); one with static descendants with geometry gets a mesh of its own (the source's geometry plus theirs: the person,
/// 2026-11-18). A moving empty is a node without a mesh. A folded transform that is not a TRS (a shear) is refused (the
/// person, 2026-11-18). A model with nothing marked <c>moves</c> never comes here: it is written by <see cref="GlbExport"/>.
/// </summary>
internal static class GlbParts
{
    /// <summary>True when some part of <paramref name="model"/> is marked <c>moves</c>, so the file is written as a tree.</summary>
    public static bool HasMoving(Model model) => model.Parts.Any(p => p.Moves);

    /// <summary>One node to write: its name, its transform (null for the root's identity), the index of its glTF mesh or null, and its children.</summary>
    internal sealed record Node(string Name, AffineTransform? Transform, int? Mesh, List<Node> Children);

    /// <summary>The file as planned: the root node, the glTF meshes by index (each a joined Carve mesh and its name), and the moving parts in pre-order.</summary>
    internal sealed record Layout(Node Root, IReadOnlyList<(string Name, PolygonMesh Mesh)> Meshes, IReadOnlyList<string> Moving)
    {
        /// <summary>Every node, the root first, in the order they are written (pre-order).</summary>
        public int NodeCount => Count(Root);

        private static int Count(Node node) => 1 + node.Children.Sum(Count);
    }

    /// <summary>The nearest ancestor of <paramref name="part"/> marked <c>moves</c>, or null for the root.</summary>
    private static Part? NearestMoving(Model model, Part part)
    {
        for (var p = model.ParentOf(part); p is not null; p = model.ParentOf(p))
            if (p.Moves) return p;
        return null;
    }

    /// <summary>The matrix from <paramref name="part"/>'s space to the space of <paramref name="owner"/> (null: the model's), composed as <see cref="Model.WorldTransform"/> composes it.</summary>
    private static Matrix4x4 Relative(Model model, Part part, Part? owner)
    {
        var m = part.LocalTransform;
        for (var p = model.ParentOf(part); p is not null && p != owner; p = model.ParentOf(p))
            m *= p.LocalTransform;
        return m;
    }

    /// <summary>
    /// The node transform of the moving part <paramref name="part"/>: its own composed with every static ancestor's up to
    /// <paramref name="owner"/>, as a TRS, or null with <paramref name="problem"/> when the composition shears.
    /// </summary>
    private static AffineTransform? NodeTransform(Model model, Part part, Part? owner, out string? problem)
    {
        problem = null;
        var scale = part.Scale;
        var rotation = Quaternion.Normalize(part.Rotation);
        var translation = part.Translation;
        for (var p = model.ParentOf(part); p is not null && p != owner; p = model.ParentOf(p))
        {
            var r = Quaternion.Normalize(p.Rotation);
            if (p.Scale.X == p.Scale.Y && p.Scale.Y == p.Scale.Z)
            {
                scale *= p.Scale.X;
                rotation = Quaternion.Normalize(Quaternion.Concatenate(rotation, r));
            }
            else if (rotation.IsIdentity)
            {
                scale *= p.Scale;
                rotation = r;
            }
            else
            {
                // A rotated part under a non-uniform scale: a TRS only when the rotation keeps the scale's axes (a quarter or half turn about one of them).
                var linear = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateScale(p.Scale);
                if (!Trs.Decomposes(linear, out var s, out var q, out _))
                {
                    problem = $"part '{part.Name}' moves, and the non-uniform scale of its static ancestor '{p.Name}' under its rotation makes a shear, "
                        + "which a glTF node's translation, rotation and scale cannot hold; give that ancestor a uniform scale, mark it moves too, or turn the part so the scale's axes stay its own";
                    return null;
                }
                scale = s;
                rotation = Quaternion.Normalize(Quaternion.Concatenate(q, r));
            }
            translation = Vector3.Transform(translation, p.LocalTransform);
        }
        return new AffineTransform(scale, rotation, translation);
    }

    /// <summary>The file's layout, or null with the refusal (in export's words, without "export: ") when a moving part's transform shears.</summary>
    public static Layout? Plan(Model model, string name, out string? refusal)
    {
        refusal = null;
        var parts = model.Parts;
        // Each node's occurrences: the root under the key "", a moving part under its name.
        var held = new Dictionary<string, List<(PolygonMesh Mesh, Matrix4x4 Transform)>>(StringComparer.Ordinal) { [""] = [] };
        foreach (var part in parts.Where(p => p.Moves)) held[part.Name] = [];
        var descendants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            var geometry = model.Shown(part);
            if (part.Moves)
            {
                if (geometry is not null) held[part.Name].Add((geometry, Matrix4x4.Identity));
                continue;
            }
            if (geometry is null) continue;
            var owner = NearestMoving(model, part);
            held[owner?.Name ?? ""].Add((geometry, Relative(model, part, owner)));
            if (owner is not null) descendants.Add(owner.Name);
        }

        var meshes = new List<(string Name, PolygonMesh Mesh)>();
        var meshOf = new Dictionary<string, int?>(StringComparer.Ordinal);
        int? Add(string meshName, List<(PolygonMesh Mesh, Matrix4x4 Transform)> occurrences)
        {
            if (Model.Join(occurrences) is not { } joined) return null;
            meshes.Add((meshName, joined));
            return meshes.Count - 1;
        }

        var shared = new Dictionary<string, int>(StringComparer.Ordinal);
        var root = new Node(name, null, Add(name, held[""]), []);
        var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        foreach (var part in parts.Where(p => p.Moves))
        {
            var owner = NearestMoving(model, part);
            if (NodeTransform(model, part, owner, out var problem) is not { } transform)
            {
                refusal = problem;
                return null;
            }

            // A part's own geometry alone is one glTF mesh, shared by its moving instances and its own node (when that holds nothing else).
            int? mesh;
            var alone = part.InstanceOf ?? part.Name;
            if (!descendants.Contains(part.Name) && model.Find(alone) is { } geometry)
            {
                if (!shared.TryGetValue(alone, out var index)) shared[alone] = index = Add(alone, [(geometry, Matrix4x4.Identity)])!.Value;
                mesh = index;
            }
            else mesh = Add(part.Name, held[part.Name]);

            var node = new Node(part.Name, transform, mesh, []);
            nodes[part.Name] = node;
            (owner is null ? root : nodes[owner.Name]).Children.Add(node);
        }

        return new Layout(root, meshes, [.. parts.Where(p => p.Moves).Select(p => p.Name)]);
    }

    /// <summary>Writes <paramref name="layout"/>: the materials shared across its meshes by name, each mesh written once however many nodes use it.</summary>
    public static byte[] Write(Model model, Layout layout)
    {
        var combined = model.Combined;
        var uvs = GlbExport.HasUvs(combined) || GlbExport.Textured(GlbExport.UsedBy(combined is null ? [] : [combined], model.Materials));
        var images = new GlbExport.Images();
        var materials = new Dictionary<string, MaterialBuilder>(StringComparer.Ordinal);
        var built = layout.Meshes.Select(m => GlbExport.Build(m.Mesh, Shading.Triangles(m.Mesh), m.Name, model.Materials, uvs, images, materials)).ToList();

        var scene = new SceneBuilder();
        var rootBuilder = new NodeBuilder(layout.Root.Name);
        Place(scene, rootBuilder, layout.Root, built);
        var gltf = scene.ToGltf2();

        // Each built mesh's targets go on its glTF mesh, found through the first node that uses it.
        var written = new HashSet<int>();
        void Targets(SharpGLTF.Schema2.Node node, Node planned)
        {
            if (planned.Mesh is { } index && written.Add(index) && built[index].Targets is { } targets)
                targets.Write(gltf, node.Mesh, built[index].Tags!);
            var children = node.VisualChildren.ToList();
            for (var i = 0; i < planned.Children.Count; i++) Targets(children[i], planned.Children[i]);
        }
        Targets(gltf.DefaultScene.VisualChildren.Single(), layout.Root);
        return gltf.WriteGLB().ToArray();
    }

    private static void Place(SceneBuilder scene, NodeBuilder builder, Node node, List<GlbExport.Built> built)
    {
        if (node.Transform is { } transform) builder.LocalTransform = transform;
        if (node.Mesh is { } index) scene.AddRigidMesh(built[index].Mesh, builder);
        else if (builder.Parent is null) scene.AddNode(builder);
        foreach (var child in node.Children) Place(scene, builder.CreateNode(child.Name), child, built);
    }

    /// <summary>The result line's part: how many nodes were written and the moving parts, e.g. <c>, 6 nodes: 'rover' and the moving parts 'Mast', 'Wheel_FL'</c>.</summary>
    public static string Describe(Layout layout) =>
        $", {layout.NodeCount.ToString(CultureInfo.InvariantCulture)} nodes: '{layout.Root.Name}' and the moving part(s) {NameList.Quoted(layout.Moving)}";
}

using System.Numerics;
using Carve.Core;
using Carve.Core.Baking;

namespace Carve.Graphs;

/// <summary>
/// The real <see cref="IMeshMaps"/>: one mesh of a <see cref="Model"/>, baked by <see cref="Carve.Core.Baking"/> on the mesh's
/// UV layout at rest. The layout is made once per size; each map once per size and settings, kept for the source's lifetime.
/// Occlusion is cast against every mesh of the model at their stored positions (M-24's rule). Every map is padded
/// <see cref="Padding"/> texels past its islands with <see cref="Bake.Pad"/>, and a texel no island or padding covers holds
/// the node's neutral value. A layout refusal throws <see cref="MeshLayoutException"/>; faces with no UVs are left out of
/// the bake and named in <see cref="Warnings"/>. Safe to share between threads.
/// </summary>
public sealed class MeshBakeSource : IMeshMaps
{
    /// <summary>How far every map is padded past its islands, in texels.</summary>
    public const int Padding = 8;

    private readonly Model _model;
    private readonly int _index;
    private readonly PolygonMesh _mesh;
    private readonly Dictionary<int, BakeLayout> _layouts = [];
    private readonly Dictionary<(string Map, int Size, double A, double B, string? Name), BakeImage> _maps = [];
    private RayCaster? _caster;

    /// <summary>Throws <see cref="ArgumentException"/>, listing the model's meshes, when <paramref name="mesh"/> is not one.</summary>
    public MeshBakeSource(Model model, string mesh)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        _index = model.Meshes.ToList().FindIndex(m => m.Name == mesh);
        if (_index < 0)
            throw new ArgumentException($"'{mesh}' is not a mesh; the meshes are {string.Join(", ", model.Meshes.Select(m => $"'{m.Name}'"))}", nameof(mesh));
        MeshName = mesh;
        _mesh = model.Meshes[_index].Mesh;
        var style = _mesh.Style ?? FaceStyle.None;
        Groups = [.. style.Groups.Select(g => g.Name)];
        Materials = [.. _mesh.LiveFaces.Select(style.MaterialOf).OfType<string>().Distinct()];
        Unmapped = _mesh.Uvs is { } uvs ? [.. _mesh.LiveFaces.Where(f => !uvs.IsMapped(f))] : [];
        Warnings = Unmapped.Count == 0 ? []
            : [$"{mesh}: {Unmapped.Count} face(s) have no UVs and were skipped: {string.Join(", ", Unmapped.Take(10))}{(Unmapped.Count > 10 ? ", ..." : "")}"];
    }

    public string MeshName { get; }

    /// <summary>The groups a <c>mesh_mask</c> may name, in definition order.</summary>
    public IReadOnlyList<string> Groups { get; }

    /// <summary>The materials a <c>mesh_mask</c> may name: those the mesh's live faces use, in order of their lowest face.</summary>
    public IReadOnlyList<string> Materials { get; }

    /// <summary>Live faces with no UVs, which no map covers (empty for a mesh with no UVs at all, which is refused instead).</summary>
    public IReadOnlyList<int> Unmapped { get; }

    /// <summary>One line naming <see cref="Unmapped"/>, or none.</summary>
    public IReadOnlyList<string> Warnings { get; }

    public BakeImage Curvature(int size, double width) =>
        Map(("curvature", size, width, 0, null), layout => Finish(Bake.Curvature(layout, width), layout, MeshCurvatureNode.Neutral));

    public BakeImage Occlusion(int size, int rays, float reach) =>
        Map(("occlusion", size, rays, reach, null), layout =>
        {
            var caster = _caster ??= new RayCaster([.. _model.Meshes.Select(m => m.Mesh)]);
            return Finish(Bake.Occlusion(layout, caster, _index, rays, reach).Image, layout, MeshOcclusionNode.Neutral);
        });

    public BakeImage Mask(int size, MaskSource source, string name)
    {
        if (!(source == MaskSource.Group ? Groups : Materials).Contains(name))
            throw new MeshMaskException(MeshName, source, name, Groups, Materials);
        return Map(($"mask.{source}", size, 0, 0, name), layout =>
        {
            // Bake.Masks makes them all at once, so every other mask of this size is kept as well.
            BakeImage? wanted = null;
            foreach (var mask in Bake.Masks(layout, _mesh))
            {
                var image = Finish(mask.Image, layout, MeshMaskNode.Neutral);
                if (mask.Source == source && mask.Name == name) wanted = image;
                else _maps.TryAdd(($"mask.{mask.Source}", size, 0, 0, mask.Name), image);
            }
            return wanted!;
        });
    }

    public BakeImage Position(int size) =>
        Map(("position", size, 0, 0, null), layout =>
        {
            var corners = _mesh.LiveFaces.SelectMany(f => _mesh.Faces[f].Corners).Select(v => _mesh.Vertices[v]).ToList();
            var lo = corners.Aggregate(Vector3.Min);
            var span = corners.Aggregate(Vector3.Max) - lo;
            var image = layout.NewImage(3);
            foreach (var t in layout.Texels())
            {
                var p = t.Position - lo;
                // A flat axis has no extent to spread over, so it reads the middle.
                image[t.X, t.Y, 0] = span.X > 0 ? p.X / span.X : 0.5f;
                image[t.X, t.Y, 1] = span.Y > 0 ? p.Y / span.Y : 0.5f;
                image[t.X, t.Y, 2] = span.Z > 0 ? p.Z / span.Z : 0.5f;
            }
            return Finish(image, layout, MeshPositionNode.Neutral);
        });

    private BakeImage Map((string Map, int Size, double A, double B, string? Name) key, Func<BakeLayout, BakeImage> make)
    {
        lock (_maps)
        {
            if (_maps.TryGetValue(key, out var made)) return made;
            return _maps[key] = make(Layout(key.Size));
        }
    }

    /// <summary>The mesh's UV layout at <paramref name="size"/>, made once (T-163 pads a slot's bake with it); throws <see cref="MeshLayoutException"/>.</summary>
    public BakeLayout Layout(int size)
    {
        lock (_maps)
        {
            if (_layouts.TryGetValue(size, out var layout)) return layout;
            var result = BakeLayout.Create(_mesh, size);
            if (result.Layout is null) throw new MeshLayoutException(MeshName, size, result.Error!);
            return _layouts[size] = result.Layout;
        }
    }

    /// <summary>Uncovered texels set to <paramref name="neutral"/>, then padded: so padding takes the islands' values and what lies beyond it reads neutral.</summary>
    internal static BakeImage Finish(BakeImage image, BakeLayout layout, float neutral) =>
        Bake.Pad(Bake.FillUncovered(image, layout, neutral), layout, Padding);
}

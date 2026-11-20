namespace Carve.Core.Baking;

/// <summary>What a <see cref="BakeMask"/> picks faces by.</summary>
public enum MaskSource
{
    Group,
    Material,
}

/// <summary>One mask of <see cref="Bake.Masks"/>: the group or material it is for, and its single-channel image.</summary>
public sealed record BakeMask(MaskSource Source, string Name, BakeImage Image);

public static partial class Bake
{
    /// <summary>
    /// One single-channel mask per group and per material of <paramref name="mesh"/>'s faces, over
    /// <paramref name="layout"/>: a covered texel is 1 where its face is in the group or has the material, else 0, and an
    /// uncovered one 0 (before padding, <see cref="Pad"/>). The order is the groups' stored order
    /// (<see cref="FaceStyle.Groups"/>, every group, so an empty one gives an all-zero mask), then the materials in the
    /// order of the lowest live face id using each. <paramref name="mesh"/> gives the groups and materials, so it may be
    /// <see cref="BakeLayout.Mesh"/> restyled; it must have the layout mesh's faces.
    /// </summary>
    public static IReadOnlyList<BakeMask> Masks(BakeLayout layout, PolygonMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(mesh);
        if (mesh.Faces.Count != layout.Mesh.Faces.Count)
            throw new ArgumentException($"the mesh has {mesh.Faces.Count} faces but the layout's has {layout.Mesh.Faces.Count}", nameof(mesh));
        var style = mesh.Style ?? FaceStyle.None;

        var picks = new List<(MaskSource Source, string Name, bool[] Faces)>();
        foreach (var (name, faces) in style.Groups)
        {
            var member = new bool[mesh.Faces.Count];
            foreach (var f in faces) if ((uint)f < (uint)member.Length) member[f] = true;
            picks.Add((MaskSource.Group, name, member));
        }
        var materials = new Dictionary<string, bool[]>();
        foreach (var f in mesh.LiveFaces)
        {
            if (style.MaterialOf(f) is not { } name) continue;
            if (!materials.TryGetValue(name, out var member))
            {
                materials[name] = member = new bool[mesh.Faces.Count];
                picks.Add((MaskSource.Material, name, member));
            }
            member[f] = true;
        }

        var result = new List<BakeMask>(picks.Count);
        foreach (var (source, name, member) in picks)
        {
            var image = layout.NewImage(1);
            for (var y = 0; y < layout.Size; y++)
            for (var x = 0; x < layout.Size; x++)
                if (layout.FaceAt(x, y) is var f and >= 0 && member[f]) image[x, y] = 1f;
            result.Add(new BakeMask(source, name, image));
        }
        return result;
    }
}

using Carve.Core.Baking;

namespace Carve.Graphs;

/// <summary>
/// Where the mesh-input nodes (<see cref="MeshNodes"/>) read their maps: each answer is a whole <c>size</c> × <c>size</c>
/// image over one mesh's UV layout, padded past its islands and neutral where nothing covers it (<see cref="MeshBakeSource"/>
/// is the real one). A source works each distinct map once per size and keeps it for its own lifetime, so a tile asked
/// twice reads the same image and nothing is baked per tile. Widths and reaches are metres on the mesh.
/// </summary>
public interface IMeshMaps
{
    /// <summary>One channel: <see cref="Bake.Curvature"/> at <paramref name="width"/> metres.</summary>
    BakeImage Curvature(int size, double width);

    /// <summary>One channel: <see cref="Bake.Occlusion"/> with <paramref name="rays"/> rays reaching <paramref name="reach"/> metres.</summary>
    BakeImage Occlusion(int size, int rays, float reach);

    /// <summary>One channel: the <see cref="Bake.Masks"/> image of the group or material <paramref name="name"/>.</summary>
    BakeImage Mask(int size, MaskSource source, string name);

    /// <summary>Three channels: the rest position, each axis 0 to 1 across the mesh's own bounding box.</summary>
    BakeImage Position(int size);
}

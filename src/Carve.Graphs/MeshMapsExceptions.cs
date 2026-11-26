using Carve.Core.Baking;

namespace Carve.Graphs;

/// <summary>Why a mesh-input node could not give its map; a tool turns <see cref="Exception.Message"/> into its error.</summary>
public abstract class MeshMapsException(string message) : Exception(message);

/// <summary>
/// The mesh's UV layout was refused (no UVs, overlapping UVs naming the faces, a size no layout takes).
/// <see cref="Exception.Message"/> is <see cref="BakeLayout.Create(Carve.Core.PolygonMesh, int)"/>'s own error, as it stands.
/// </summary>
public sealed class MeshLayoutException(string mesh, int size, string message) : MeshMapsException(message)
{
    public string Mesh { get; } = mesh;
    public int Size { get; } = size;
}

/// <summary>A <c>mesh_mask</c> named a group or material the mesh does not have; the message lists those it has.</summary>
public sealed class MeshMaskException(string mesh, MaskSource source, string name, IReadOnlyList<string> groups, IReadOnlyList<string> materials)
    : MeshMapsException($"mesh '{mesh}' has no {(source == MaskSource.Group ? "group" : "material")} '{name}'; its groups are {List(groups)} and its materials {List(materials)}")
{
    public string Mesh { get; } = mesh;
    public MaskSource Kind { get; } = source;
    public string Name { get; } = name;
    public IReadOnlyList<string> Groups { get; } = groups;
    public IReadOnlyList<string> Materials { get; } = materials;

    private static string List(IReadOnlyList<string> names) => names.Count == 0 ? "none" : string.Join(", ", names.Select(n => $"'{n}'"));
}

/// <summary>A node's parameters that the file's ranges cannot refuse (a width of 0, a mask naming both or neither); the message names the node.</summary>
public sealed class MeshParameterException(string message) : MeshMapsException(message);

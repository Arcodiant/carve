using Carve.Core;

namespace Carve.Server;

/// <summary>One published state of the store: the mesh and the version it was published as.</summary>
public sealed record ModelSnapshot(PolygonMesh? Mesh, long Version);

/// <summary>
/// The one current mesh. Written on the MCP thread, read on the frame thread, so it publishes
/// immutable values only: a <see cref="PolygonMesh"/> never changes, and the mesh and its
/// version are swapped together as one <see cref="ModelSnapshot"/>.
/// </summary>
public sealed class ModelStore
{
    private readonly object _write = new();
    private ModelSnapshot _current = new(null, 0);

    /// <summary>
    /// The current mesh (null until the first success) and its version. Version is 0 while empty and
    /// rises by exactly 1 on each <see cref="Replace"/>; a reader that kept the last version it drew
    /// redraws when <c>Version</c> differs. Safe to read from any thread, never torn.
    /// </summary>
    public ModelSnapshot Current => Volatile.Read(ref _current);

    /// <summary>Replaces the mesh and returns the snapshot now current.</summary>
    public ModelSnapshot Replace(PolygonMesh mesh)
    {
        lock (_write)
        {
            var next = new ModelSnapshot(mesh, _current.Version + 1);
            Volatile.Write(ref _current, next);
            return next;
        }
    }
}

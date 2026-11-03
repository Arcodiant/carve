using Carve.Core;

namespace Carve.Server;

/// <summary>One published state of the store: the mesh and the version it was published as.</summary>
public sealed record ModelSnapshot(PolygonMesh? Mesh, long Version);

/// <summary>What <see cref="ModelStore.Undo"/> did: the tool whose change was taken back, and the snapshot now current.</summary>
public sealed record UndoResult(string Undone, ModelSnapshot Current);

/// <summary>
/// The one current mesh and the steps behind it. Written on the MCP thread, read on the frame thread, so it publishes
/// immutable values only: a <see cref="PolygonMesh"/> never changes, and the mesh and its
/// version are swapped together as one <see cref="ModelSnapshot"/>.
/// </summary>
public sealed class ModelStore
{
    /// <summary>How many steps back <see cref="Undo"/> can go: the current mesh plus this many older ones are kept.</summary>
    public const int UndoDepth = 100;

    private readonly object _write = new();
    private readonly List<(PolygonMesh Mesh, string Tool)> _history = [];
    private ModelSnapshot _current = new(null, 0);

    /// <summary>
    /// The current mesh (null until the first success) and its version. Version is 0 while empty and
    /// rises by exactly 1 on every change, an undo included; a reader that kept the last version it drew
    /// redraws when <c>Version</c> differs. Safe to read from any thread, never torn.
    /// </summary>
    public ModelSnapshot Current => Volatile.Read(ref _current);

    /// <summary>Makes <paramref name="mesh"/> current as the result of <paramref name="tool"/>, keeping the mesh it replaces for undo.</summary>
    public ModelSnapshot Push(PolygonMesh mesh, string tool)
    {
        lock (_write)
        {
            return PushLocked(mesh, tool);
        }
    }

    /// <summary>Replaces the mesh as a <c>define_mesh</c> step and returns the snapshot now current.</summary>
    public ModelSnapshot Replace(PolygonMesh mesh) => Push(mesh, "define_mesh");

    /// <summary>
    /// Runs <paramref name="edit"/> on the current mesh and, if it succeeds, pushes the result, all under the write lock so
    /// two calls cannot edit the same mesh. Returns null (and pushes nothing) when there is no mesh yet; otherwise the snapshot
    /// now current, with the edit's own result in <paramref name="result"/>.
    /// </summary>
    public ModelSnapshot? Edit(string tool, Func<PolygonMesh, EditResult> edit, out EditResult? result)
    {
        lock (_write)
        {
            result = null;
            var mesh = _current.Mesh;
            if (mesh is null) return null;
            result = edit(mesh);
            return result.Succeeded ? PushLocked(result.Mesh!, tool) : _current;
        }
    }

    /// <summary>Steps back one change, or null when there is nothing older.</summary>
    public UndoResult? Undo()
    {
        lock (_write)
        {
            if (_history.Count < 2) return null;
            var undone = _history[^1].Tool;
            _history.RemoveAt(_history.Count - 1);
            var next = new ModelSnapshot(_history[^1].Mesh, _current.Version + 1);
            Volatile.Write(ref _current, next);
            return new UndoResult(undone, next);
        }
    }

    private ModelSnapshot PushLocked(PolygonMesh mesh, string tool)
    {
        _history.Add((mesh, tool));
        if (_history.Count > UndoDepth + 1) _history.RemoveAt(0);
        var next = new ModelSnapshot(mesh, _current.Version + 1);
        Volatile.Write(ref _current, next);
        return next;
    }
}

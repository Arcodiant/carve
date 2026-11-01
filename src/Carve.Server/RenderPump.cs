using Carve.Core;

namespace Carve.Server;

/// <summary>What the pump asks of the scene, on the frame thread. Each may throw.</summary>
internal interface IRenderScene
{
    /// <summary>Puts this mesh in the scene. If it throws, the scene must not count it as shown.</summary>
    void ShowMesh(PolygonMesh mesh);

    /// <summary>Points the camera and the light for this view of the mesh shown.</summary>
    void ApplyView(RenderView view);

    /// <summary>Draws the mesh shown and answers the request, whatever happens.</summary>
    void Render(RenderRequest request);

    void Report(Exception exception, string message);
}

/// <summary>
/// Decides what the frame thread does next: pure sequencing, so that every interleaving of the
/// tools with the frame thread can be tested without a device. A frame reads the lighting and
/// transforms published earlier in the same loop iteration, so a change of the scene ends the
/// <see cref="Step"/> and the requests waiting on it stay queued for a later one. A request needs
/// the mesh current at its post or a newer one, never the newest, so each step opens by serving a
/// head the shown mesh satisfies: however fast the store moves, the queue moves too.
/// </summary>
internal sealed class RenderPump(RenderQueue queue, ModelStore store, IRenderScene scene)
{
    private long _version;
    private RenderView _view = RenderView.Front;
    private bool _viewApplied = true;
    private string? _meshFault;

    public void Step()
    {
        for (var opening = true; ; opening = false)
        {
            // The peek comes first. A request posted after define_mesh returned is then only seen
            // with the store already showing that mesh or a newer one, including the very first.
            var waiting = queue.TryPeek(out var next);
            var current = store.Current;
            var due = opening && waiting && _version >= next!.PostedVersion;
            if (current.Version != _version && !due)
            {
                ShowMesh(current);
                return;
            }

            if (!waiting)
            {
                return;
            }

            if (_meshFault is { } fault)
            {
                queue.TryTake(out _);
                next!.Fail(new InvalidOperationException(fault));
                continue;
            }

            if (next!.View != _view || !_viewApplied)
            {
                ChangeView(next);
                return;
            }

            queue.TryTake(out _);
            scene.Render(next);
        }
    }

    private void ShowMesh(ModelSnapshot snapshot)
    {
        // Taken as seen even on a failure: the next try is the next mesh, not every frame.
        _version = snapshot.Version;
        try
        {
            scene.ShowMesh(snapshot.Mesh!);
            _meshFault = null;
        }
        catch (Exception e)
        {
            // Until the next mesh, a render is this error, never the last mesh under a new caption.
            _meshFault = $"The mesh from define_mesh could not be put in the scene: {e.GetBaseException().Message}";
            scene.Report(e, _meshFault);
            return;
        }

        try
        {
            scene.ApplyView(_view);
            _viewApplied = true;
        }
        catch (Exception e)
        {
            // The mesh is in, the camera is not to be trusted: the next request sets it again.
            _viewApplied = false;
            scene.Report(e, ViewFault(e));
        }
    }

    private void ChangeView(RenderRequest next)
    {
        try
        {
            _view = next.View;
            _viewApplied = false;
            scene.ApplyView(_view);
            _viewApplied = true;
        }
        catch (Exception e)
        {
            var fault = ViewFault(e);
            queue.TryTake(out _);
            next.Fail(new InvalidOperationException(fault));
            scene.Report(e, fault);
        }
    }

    private string ViewFault(Exception e) =>
        $"The {_view.ToString().ToLowerInvariant()} view could not be set up: {e.GetBaseException().Message}";
}

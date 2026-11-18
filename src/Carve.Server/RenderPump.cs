using System.Numerics;
using Carve.Core;

namespace Carve.Server;

/// <summary>What the pump asks of the scene, on the frame thread. Each may throw.</summary>
internal interface IRenderScene
{
    /// <summary>Puts this mesh in the scene. If it throws, the scene must not count it as shown.</summary>
    void ShowMesh(PolygonMesh mesh);

    /// <summary>Puts <paramref name="mesh"/> in the scene framed on the bounds of <paramref name="frameOn"/> (the rest pose, for a posed model: D-6, framing).</summary>
    void ShowMesh(PolygonMesh mesh, PolygonMesh frameOn) => ShowMesh(mesh);

    /// <summary>As above, with the weight of one bone at each vertex of <paramref name="mesh"/> (NaN where unweighted), for the weights look.</summary>
    void ShowMesh(PolygonMesh mesh, PolygonMesh frameOn, float[] weights) => ShowMesh(mesh, frameOn);

    /// <summary>The model's materials (T-78), given before each mesh of that model is shown, for its faces that use one.</summary>
    void ShowMaterials(IReadOnlyList<Material> materials)
    {
    }

    /// <summary>Draws this loaded <c>.glb</c> in place of the model, framed on its own rest bounds, or (null) the model again.</summary>
    void ShowGlb(GlbScene? glb) { }

    /// <summary>Points the cameras and the light for this spec of the mesh shown, and draws its look if that changed.</summary>
    void ApplyView(RenderSpec spec);

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
    private RenderSpec _spec = new(CameraSpec.ForView(RenderView.Front));
    private bool _viewApplied = true;
    private string? _meshFault;
    private (Vector3 Min, Vector3 Max)? _shownBounds;
    private PolygonMesh? _shown;
    private Model? _shownModel;
    private Pose? _shownPose;
    private string? _shownWeights;
    private GlbScene? _shownGlb;

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

            // A loaded glb (render_glb) is drawn in place of the model. Putting it in, or taking it out, is a change of
            // the scene: it ends the step, and the view is applied again, on the new framing, before anything is drawn.
            if (!ReferenceEquals(next!.Spec.Glb, _shownGlb))
            {
                ShowGlb(next);
                return;
            }

            // The model's own checks are not this request's: it draws the file, not the store.
            if (next.Spec.Glb is not null)
            {
                if (next.Spec != _spec || !_viewApplied)
                {
                    ChangeView(next);
                    return;
                }

                queue.TryTake(out _);
                next.ServedVersion = _version;
                scene.Render(next);
                continue;
            }

            if (_meshFault is { } fault)
            {
                queue.TryTake(out _);
                next!.Fail(new InvalidOperationException(fault));
                continue;
            }

            // The target was checked at the call against the mesh then current; the mesh shown may be a
            // newer one, so it is checked again against that mesh, with the same text.
            if (next!.Spec.Camera.Target is { } target && _shownBounds is { } bounds
                && TargetCheck.Refusal(target, bounds.Min, bounds.Max) is { } refusal)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(refusal));
                continue;
            }

            // The same for a highlight: its ids were checked against the mesh of the call.
            if (_shown is { } shown && next.Spec.Highlight.Unknown(shown) is { Count: > 0 } unknown)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(string.Join("; ", unknown)));
                continue;
            }

            // And for a weights look: its bone was checked against the model of the call. Only this request fails;
            // the mesh shown stays shaded, so a later plain render is served.
            if (next.Spec.Weights is { } bone && _shownModel is { } model && WeightHeat.Refusal(model, bone) is { } noBone)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(noBone));
                continue;
            }

            // The pose is part of what is shown: a request for another one puts the mesh in the scene again, posed.
            if (!Equals(next.Spec.Pose, _shownPose))
            {
                _shownPose = next.Spec.Pose;
                ShowMesh(store.Current);
                continue;
            }

            // So is the bone whose weights are drawn.
            if (next.Spec.Weights != _shownWeights)
            {
                _shownWeights = next.Spec.Weights;
                ShowMesh(store.Current);
                continue;
            }

            if (next.Spec != _spec || !_viewApplied)
            {
                ChangeView(next);
                return;
            }

            queue.TryTake(out _);
            next.ServedVersion = _version;
            scene.Render(next);
        }
    }

    private void ShowMesh(ModelSnapshot snapshot)
    {
        // Taken as seen even on a failure: the next try is the next mesh, not every frame.
        _version = snapshot.Version;
        try
        {
            var drawn = snapshot.Mesh!;
            scene.ShowMaterials(snapshot.Model.Materials);
            // The bone of the last weights look may be gone from a newer model (an undo, open or renaming define_skeleton):
            // then the mesh is shown shaded, and only a weights request for that bone fails, at the head of the queue.
            var heat = _shownWeights is null || WeightHeat.Refusal(snapshot.Model, _shownWeights) is not null
                ? null : WeightHeat.Of(snapshot.Model, _shownWeights);
            if (_shownPose is { } pose && snapshot.Model.Skeleton is not null)
            {
                var posed = Skinning.Posed(snapshot.Model, pose).Combined!;
                if (heat is null) scene.ShowMesh(posed, drawn);
                else scene.ShowMesh(posed, drawn, heat);
            }
            else if (heat is not null)
                scene.ShowMesh(drawn, drawn, heat);
            else
                scene.ShowMesh(drawn);
            var report = MeshReport.Create(snapshot.Mesh!);
            _shownBounds = (report.BoundsMin, report.BoundsMax);
            _shown = snapshot.Mesh;
            _shownModel = snapshot.Model;
            _meshFault = null;
        }
        catch (Exception e)
        {
            // Until the next mesh, a render is this error, never the last mesh under a new caption.
            _meshFault = $"The current mesh could not be put in the scene: {e.GetBaseException().Message}";
            scene.Report(e, _meshFault);
            return;
        }

        try
        {
            scene.ApplyView(_spec);
            _viewApplied = true;
        }
        catch (Exception e)
        {
            // The mesh is in, the camera is not to be trusted: the next request sets it again.
            _viewApplied = false;
            scene.Report(e, ViewFault(e));
        }
    }

    private void ShowGlb(RenderRequest next)
    {
        _viewApplied = false;
        try
        {
            scene.ShowGlb(next.Spec.Glb);
            _shownGlb = next.Spec.Glb;
        }
        catch (Exception e)
        {
            // Back to the model, so a failed file leaves nothing of itself in the scene.
            var fault = $"The file could not be put in the scene: {e.GetBaseException().Message}";
            scene.Report(e, fault);
            if (next.Spec.Glb is not null)
            {
                queue.TryTake(out _);
                next.Fail(new InvalidOperationException(fault));
            }

            try
            {
                scene.ShowGlb(null);
            }
            catch (Exception again)
            {
                scene.Report(again, "The model could not be put back after a file failed.");
            }

            _shownGlb = null;
        }
    }

    private void ChangeView(RenderRequest next)
    {
        try
        {
            _spec = next.Spec;
            _viewApplied = false;
            scene.ApplyView(_spec);
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
        $"The {Name(_spec.Camera)} could not be set up: {e.GetBaseException().Message}";

    private static string Name(CameraSpec camera) =>
        camera == CameraSpec.ForView(RenderView.Front) ? "front view"
        : camera == CameraSpec.ForView(RenderView.Back) ? "back view"
        : "camera";
}

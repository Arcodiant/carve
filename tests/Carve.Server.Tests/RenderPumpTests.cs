using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>
/// The frame thread's sequencing, with the tools' calls landing at exact points inside a step.
/// <see cref="RenderTests"/> has the same on the real <c>carve</c>, where only timing places them.
/// </summary>
[Trait("Tier", "unit")]
public class RenderPumpTests
{
    private static PolygonMesh Mesh() => TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces);

    private sealed class Scene : IRenderScene
    {
        public List<string> Log { get; } = [];

        public PolygonMesh? Shown { get; private set; }

        public Func<PolygonMesh, Exception?> OnShow { get; set; } = _ => null;

        public Func<RenderView, Exception?> OnView { get; set; } = _ => null;

        public Action<RenderRequest> OnRender { get; set; } = _ => { };

        public void ShowMesh(PolygonMesh mesh)
        {
            if (OnShow(mesh) is { } e)
            {
                Log.Add("show failed");
                throw e;
            }

            Shown = mesh;
            Log.Add("show");
        }

        public void ApplyView(RenderSpec spec)
        {
            var view = Describe(spec);
            if (OnView(ViewOf(spec.Camera)) is { } e)
            {
                Log.Add($"view {view} failed");
                throw e;
            }

            Log.Add($"view {view}");
        }

        private static RenderView ViewOf(CameraSpec camera) => camera.Direction.X > 0 ? RenderView.Front : RenderView.Back;

        /// <summary>The M-1 views by name, any other spec whole, so a change of zoom, target, projection or size shows.</summary>
        public static string Describe(RenderSpec spec) =>
            spec == new RenderSpec(CameraSpec.ForView(RenderView.Front)) ? "Front"
            : spec == new RenderSpec(CameraSpec.ForView(RenderView.Back)) ? "Back"
            : spec == new RenderSpec(spec.Camera) ? spec.Camera.ToString()
            : spec.ToString();

        public void Render(RenderRequest request)
        {
            Log.Add($"render {Describe(request.Spec)}");
            Rendered.Add((request, Shown));
            OnRender(request);
        }

        public List<(RenderRequest Request, PolygonMesh? Mesh)> Rendered { get; } = [];

        public void Report(Exception exception, string message) => Reported.Add(message);

        public List<string> Reported { get; } = [];
    }

    /// <summary>Runs something at the very moment the frame thread looks at the queue.</summary>
    private sealed class QueueWithAHook(ModelStore store) : RenderQueue(store)
    {
        public Action? BeforePeek { get; set; }

        public override bool TryPeek(out RenderRequest? request)
        {
            var hook = BeforePeek;
            BeforePeek = null;
            hook?.Invoke();
            return base.TryPeek(out request);
        }
    }

    private readonly ModelStore _store = new();
    private readonly QueueWithAHook _queue;
    private readonly Scene _scene = new();

    public RenderPumpTests() => _queue = new QueueWithAHook(_store);

    private RenderPump Started()
    {
        var pump = new RenderPump(_queue, _store, _scene);
        _store.Replace(Mesh());
        pump.Step();
        _scene.Log.Clear();
        return pump;
    }

    private static string FailureOf(Task task)
    {
        Assert.True(task.IsFaulted);
        return task.Exception!.GetBaseException().Message;
    }

    [Fact]
    public void A_version_change_leaves_the_request_queued_for_a_later_step()
    {
        var pump = Started();
        var mesh = Mesh();
        _store.Replace(mesh);
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));

        pump.Step();
        Assert.Equal(["show", "view Front"], _scene.Log);
        Assert.Empty(_scene.Rendered);

        pump.Step();
        Assert.Same(mesh, Assert.Single(_scene.Rendered).Mesh);
    }

    [Fact]
    public void Each_request_carries_the_version_it_was_drawn_on_so_a_sheet_split_by_an_edit_shows()
    {
        var pump = Started();
        var before = _store.Current.Version;
        var first = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        var second = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        _scene.OnRender = request =>
        {
            // A move landing between two tiles of one sheet.
            _scene.OnRender = r => r.Complete(null!);
            request.Complete(null!);
            _store.Replace(Mesh());
        };

        pump.Step();
        pump.Step();
        pump.Step();

        Assert.Equal(2, _scene.Rendered.Count);
        Assert.Equal(before, _scene.Rendered[0].Request.ServedVersion);
        Assert.Equal(_store.Current.Version, _scene.Rendered[1].Request.ServedVersion);
        Assert.NotEqual(_scene.Rendered[0].Request.ServedVersion, _scene.Rendered[1].Request.ServedVersion);
    }

    [Fact]
    public void The_store_is_rechecked_before_every_request_of_one_drain()
    {
        var pump = Started();
        var first = _store.Current.Mesh;
        var replaced = Mesh();
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        _scene.OnRender = _ =>
        {
            // define_mesh then render, landing while the drain is inside its first render.
            _scene.OnRender = _ => { };
            _store.Replace(replaced);
            _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        };

        pump.Step();
        Assert.Same(first, Assert.Single(_scene.Rendered).Mesh);

        pump.Step();
        Assert.Equal(3, _scene.Rendered.Count);
        Assert.Same(replaced, _scene.Rendered[1].Mesh);
        Assert.Same(replaced, _scene.Rendered[2].Mesh);
    }

    [Fact]
    public void A_first_mesh_and_render_landing_as_the_step_begins_never_render_with_no_mesh()
    {
        var pump = new RenderPump(_queue, _store, _scene);
        var mesh = Mesh();
        _queue.BeforePeek = () =>
        {
            _store.Replace(mesh);
            _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        };

        pump.Step();
        Assert.Empty(_scene.Rendered);

        pump.Step();
        Assert.Same(mesh, Assert.Single(_scene.Rendered).Mesh);
    }

    [Fact]
    public void A_failed_mesh_fails_its_renders_by_name_until_a_new_mesh_shows()
    {
        var pump = Started();
        _scene.OnShow = _ => new OutOfMemoryException("no room");
        _store.Replace(Mesh());
        var waiting = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));

        pump.Step();
        pump.Step();

        var message = FailureOf(waiting);
        Assert.Contains("mesh", message);
        Assert.Contains("no room", message);
        Assert.DoesNotContain("Vulkan", message);
        Assert.Contains(message, _scene.Reported);

        var later = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        pump.Step();
        Assert.Equal(message, FailureOf(later));
        Assert.Empty(_scene.Rendered);
        Assert.Equal(["show failed"], _scene.Log); // Once, not every frame.

        _scene.OnShow = _ => null;
        var mesh = Mesh();
        _store.Replace(mesh);
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        pump.Step();
        pump.Step();
        Assert.Same(mesh, Assert.Single(_scene.Rendered).Mesh);
    }

    /// <summary>A new mesh every frame, as a define_mesh loop gives: 2 frames at most per request.</summary>
    [Theory]
    [InlineData(RenderView.Front)] // The view already applied.
    [InlineData(RenderView.Back)]  // A change of view takes a frame of its own.
    public void A_stream_of_meshes_does_not_starve_a_queued_request(RenderView view)
    {
        var pump = Started();
        _store.Replace(Mesh());
        var posted = _store.Current.Mesh;
        _queue.Post(new RenderSpec(CameraSpec.ForView(view)));
        pump.Step(); // Shows the mesh it was posted at.

        var frames = 0;
        while (_scene.Rendered.Count == 0)
        {
            Assert.True(++frames <= 2, "not served within 2 frames of a stream");
            _store.Replace(Mesh());
            pump.Step();
        }

        Assert.Same(posted, _scene.Rendered[0].Mesh);

        // Then the newer mesh is picked up, for the next request to show.
        _store.Replace(Mesh());
        var newest = _store.Current.Mesh;
        pump.Step();
        Assert.Same(newest, _scene.Shown);
    }

    [Fact]
    public void A_first_mesh_that_fails_is_a_named_failure_too()
    {
        var pump = new RenderPump(_queue, _store, _scene);
        _scene.OnShow = _ => new InvalidOperationException("no room");
        _store.Replace(Mesh());
        var waiting = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));

        pump.Step();
        pump.Step();

        Assert.Contains("no room", FailureOf(waiting));
    }

    [Fact]
    public void A_view_that_fails_fails_its_request_by_name_and_the_next_request_sets_it_again()
    {
        var pump = Started();
        _scene.OnView = _ => new InvalidOperationException("no camera");
        var failed = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Back)));

        pump.Step();

        var message = FailureOf(failed);
        Assert.Contains("back view", message);
        Assert.Contains("no camera", message);
        Assert.DoesNotContain("Vulkan", message);

        _scene.OnView = _ => null;
        var again = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Back)));
        pump.Step();
        Assert.False(again.IsCompleted);
        Assert.Equal("view Back", _scene.Log[^1]);

        pump.Step();
        Assert.Equal("render Back", _scene.Log[^1]);
    }

    [Fact]
    public void A_view_that_fails_with_a_new_mesh_is_set_again_before_anything_is_drawn()
    {
        var pump = Started();
        _scene.OnView = _ => new InvalidOperationException("no camera");
        _store.Replace(Mesh());
        pump.Step();
        Assert.Single(_scene.Reported);

        _scene.OnView = _ => null;
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        pump.Step();
        Assert.Empty(_scene.Rendered);

        pump.Step();
        Assert.Single(_scene.Rendered);
    }

    private static PolygonMesh BigMesh() =>
        TestMeshes.Make(TestMeshes.CubeVertices.Select(v => v * 2), TestMeshes.CubeFaces);

    [Fact]
    public void A_change_of_zoom_alone_changes_the_view()
    {
        var pump = Started();
        var front = CameraSpec.ForView(RenderView.Front);
        var zoomed = front with { Zoom = 2 };
        _queue.Post(new RenderSpec(front));
        pump.Step();
        _queue.Post(new RenderSpec(zoomed));

        pump.Step();
        pump.Step();
        Assert.Equal(["render Front", $"view {zoomed}", $"render {zoomed}"], _scene.Log);
    }

    [Fact]
    public void A_change_of_mode_alone_is_a_scene_change_drawn_on_a_later_step()
    {
        var pump = Started();
        var front = new RenderSpec(CameraSpec.ForView(RenderView.Front));
        var wireframe = front with { Mode = Look.Wireframe };
        _queue.Post(front);
        pump.Step();
        _queue.Post(wireframe);

        pump.Step();
        Assert.Equal(["render Front", $"view {wireframe}"], _scene.Log);
        pump.Step();
        Assert.Equal(["render Front", $"view {wireframe}", $"render {wireframe}"], _scene.Log);
    }

    [Fact]
    public void A_change_of_highlight_alone_is_a_scene_change_and_an_equal_one_is_not()
    {
        var pump = Started();
        var front = new RenderSpec(CameraSpec.ForView(RenderView.Front));
        var picked = front with { Highlight = Highlight.Of([], [], [1, 2]) };
        var same = front with { Highlight = Highlight.Of([], [], [2, 1]) };
        _queue.Post(front);
        pump.Step();
        _queue.Post(picked);

        pump.Step();
        Assert.Equal(["render Front", $"view {picked}"], _scene.Log);
        pump.Step();
        _queue.Post(same);
        pump.Step();
        Assert.Equal(["render Front", $"view {picked}", $"render {picked}", $"render {same}"], _scene.Log);
    }

    [Fact]
    public void A_highlight_the_shown_mesh_lacks_is_refused_as_at_the_call()
    {
        var pump = Started();

        var rendering = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)) { Highlight = Highlight.Of([], [], [99]) });
        pump.Step();

        Assert.Contains("faces[99]", FailureOf(rendering));
        Assert.Empty(_scene.Log);
    }

    [Fact]
    public void A_change_of_size_alone_changes_the_view()
    {
        var pump = Started();
        var front = new RenderSpec(CameraSpec.ForView(RenderView.Front));
        var larger = front with { Size = 1024 };
        _queue.Post(front);
        pump.Step();
        _queue.Post(larger);

        pump.Step();
        Assert.Equal(["render Front", $"view {larger}"], _scene.Log);
        pump.Step();
        Assert.Equal(["render Front", $"view {larger}", $"render {larger}"], _scene.Log);
    }

    [Fact]
    public void A_change_of_projection_alone_changes_the_view()
    {
        var pump = Started();
        var front = new RenderSpec(CameraSpec.ForView(RenderView.Front));
        var parallel = front with { Projection = Projection.Orthographic };
        _queue.Post(front);
        pump.Step();
        _queue.Post(parallel);

        pump.Step();
        Assert.Equal(["render Front", $"view {parallel}"], _scene.Log);
        pump.Step();
        Assert.Equal(["render Front", $"view {parallel}", $"render {parallel}"], _scene.Log);
    }

    [Fact]
    public void A_change_of_target_alone_changes_the_view()
    {
        var pump = Started();
        var front = CameraSpec.ForView(RenderView.Front);
        var aimed = front with { Target = new System.Numerics.Vector3(0.5f, 0.5f, 0.5f) };
        _queue.Post(new RenderSpec(front));
        pump.Step();
        _queue.Post(new RenderSpec(aimed));

        pump.Step();
        pump.Step();
        Assert.Equal(["render Front", $"view {aimed}", $"render {aimed}"], _scene.Log);
    }

    [Fact]
    public void A_new_mesh_re_applies_the_camera_with_its_target()
    {
        var pump = Started();
        var aimed = CameraSpec.ForView(RenderView.Front) with { Target = new System.Numerics.Vector3(0.5f, 0.5f, 0.5f), Zoom = 3 };
        _queue.Post(new RenderSpec(aimed));
        pump.Step();
        pump.Step();
        _scene.Log.Clear();

        _store.Replace(BigMesh());
        pump.Step();
        Assert.Equal(["show", $"view {aimed}"], _scene.Log);
    }

    [Fact]
    public void A_target_outside_the_mesh_shown_fails_the_request_with_the_tools_text()
    {
        var pump = Started(); // the unit cube
        _store.Replace(BigMesh());
        var target = new System.Numerics.Vector3(1.5f, 0.5f, 0.5f);
        var waiting = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front) with { Target = target })); // inside the 2-cube
        _store.Replace(Mesh()); // a define_mesh racing the call: the unit cube is what gets shown

        pump.Step();
        Assert.Equal(["show", "view Front"], _scene.Log);
        pump.Step();
        Assert.Equal(
            "target: (1.5, 0.5, 0.5) is outside the mesh's bounds, (0, 0, 0) to (1, 1, 1).",
            FailureOf(waiting));
        Assert.Empty(_scene.Rendered);
    }

    [Fact]
    public void A_weights_render_then_a_model_without_that_bone_leaves_plain_renders_served()
    {
        var pump = Started();
        _store.DefineSkeleton(RigTestHelpers.Bones("Root", "Tip"), "define_skeleton", out _);
        var front = CameraSpec.ForView(RenderView.Front);
        _queue.Post(new RenderSpec(front) { Weights = "Root" });
        for (var i = 0; i < 4; i++) pump.Step();
        Assert.Equal("Root", Assert.Single(_scene.Rendered).Request.Spec.Weights);

        Assert.NotNull(_store.Undo()); // the skeleton is gone
        pump.Step();
        Assert.Empty(_scene.Reported);

        _queue.Post(new RenderSpec(front));
        for (var i = 0; i < 4; i++) pump.Step();
        Assert.Equal(2, _scene.Rendered.Count);
        Assert.Null(_scene.Rendered[1].Request.Spec.Weights);

        // Only a weights request for the missing bone fails, with the tool's own text; the next plain one is served.
        var stale = _queue.Post(new RenderSpec(front) { Weights = "Root" });
        _queue.Post(new RenderSpec(front));
        for (var i = 0; i < 4; i++) pump.Step();
        Assert.Equal("weights: the model has no skeleton; call define_skeleton first", FailureOf(stale));
        Assert.Equal(3, _scene.Rendered.Count);

        // A renaming define_skeleton: the bone is gone, the skeleton is not.
        _store.DefineSkeleton(RigTestHelpers.Bones("Base", "Tip"), "define_skeleton", out _);
        var renamed = _queue.Post(new RenderSpec(front) { Weights = "Root" });
        _queue.Post(new RenderSpec(front));
        for (var i = 0; i < 4; i++) pump.Step();
        Assert.StartsWith("weights: no bone named 'Root'", FailureOf(renamed));
        Assert.Equal(4, _scene.Rendered.Count);
        Assert.Empty(_scene.Reported);
    }
}

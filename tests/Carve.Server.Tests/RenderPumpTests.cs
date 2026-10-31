using Carve.Core;
using Carve.Core.Tests;

namespace Carve.Server.Tests;

/// <summary>
/// The frame thread's sequencing, with the tools' calls landing at exact points inside a step.
/// <see cref="RenderTests"/> has the same on the real <c>carve</c>, where only timing places them.
/// </summary>
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

        public void ApplyView(RenderView view)
        {
            if (OnView(view) is { } e)
            {
                Log.Add($"view {view} failed");
                throw e;
            }

            Log.Add($"view {view}");
        }

        public void Render(RenderRequest request)
        {
            Log.Add($"render {request.View}");
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
        _queue.Post(RenderView.Front);

        pump.Step();
        Assert.Equal(["show", "view Front"], _scene.Log);
        Assert.Empty(_scene.Rendered);

        pump.Step();
        Assert.Same(mesh, Assert.Single(_scene.Rendered).Mesh);
    }

    [Fact]
    public void The_store_is_rechecked_before_every_request_of_one_drain()
    {
        var pump = Started();
        var first = _store.Current.Mesh;
        var replaced = Mesh();
        _queue.Post(RenderView.Front);
        _queue.Post(RenderView.Front);
        _scene.OnRender = _ =>
        {
            // define_mesh then render, landing while the drain is inside its first render.
            _scene.OnRender = _ => { };
            _store.Replace(replaced);
            _queue.Post(RenderView.Front);
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
            _queue.Post(RenderView.Front);
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
        var waiting = _queue.Post(RenderView.Front);

        pump.Step();
        pump.Step();

        var message = FailureOf(waiting);
        Assert.Contains("mesh", message);
        Assert.Contains("no room", message);
        Assert.DoesNotContain("Vulkan", message);
        Assert.Contains(message, _scene.Reported);

        var later = _queue.Post(RenderView.Front);
        pump.Step();
        Assert.Equal(message, FailureOf(later));
        Assert.Empty(_scene.Rendered);
        Assert.Equal(["show failed"], _scene.Log); // Once, not every frame.

        _scene.OnShow = _ => null;
        var mesh = Mesh();
        _store.Replace(mesh);
        _queue.Post(RenderView.Front);
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
        _queue.Post(view);
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
    public void A_mesh_that_cannot_be_framed_fails_each_request_with_its_own_words_and_the_next_mesh_works()
    {
        const string words = "This mesh can't be framed: side 1, about 4e+06 from the origin.";
        var pump = Started();
        _scene.OnView = _ => new MeshNotFramedException(words);
        _store.Replace(Mesh());
        pump.Step();

        var first = _queue.Post(RenderView.Front);
        var second = _queue.Post(RenderView.Back);
        pump.Step();
        pump.Step();

        Assert.Equal(words, FailureOf(first));
        Assert.Equal(words, FailureOf(second));
        Assert.IsType<MeshNotFramedException>(first.Exception!.GetBaseException());

        _scene.OnView = _ => null;
        _store.Replace(Mesh());
        pump.Step();
        _queue.Post(RenderView.Front);
        pump.Step();
        pump.Step();
        Assert.Single(_scene.Rendered);
    }

    [Fact]
    public void A_first_mesh_that_fails_is_a_named_failure_too()
    {
        var pump = new RenderPump(_queue, _store, _scene);
        _scene.OnShow = _ => new InvalidOperationException("no room");
        _store.Replace(Mesh());
        var waiting = _queue.Post(RenderView.Front);

        pump.Step();
        pump.Step();

        Assert.Contains("no room", FailureOf(waiting));
    }

    [Fact]
    public void A_view_that_fails_fails_its_request_by_name_and_the_next_request_sets_it_again()
    {
        var pump = Started();
        _scene.OnView = _ => new InvalidOperationException("no camera");
        var failed = _queue.Post(RenderView.Back);

        pump.Step();

        var message = FailureOf(failed);
        Assert.Contains("back view", message);
        Assert.Contains("no camera", message);
        Assert.DoesNotContain("Vulkan", message);

        _scene.OnView = _ => null;
        var again = _queue.Post(RenderView.Back);
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
        _queue.Post(RenderView.Front);
        pump.Step();
        Assert.Empty(_scene.Rendered);

        pump.Step();
        Assert.Single(_scene.Rendered);
    }
}

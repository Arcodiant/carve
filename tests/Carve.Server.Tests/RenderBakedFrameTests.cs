using Carve.Core;
using Carve.Core.Tests;
using RenderTargetPixels = Empyrean.RenderTargetPixels;
using TextureFormat = Empyrean.TextureFormat;

namespace Carve.Server.Tests;

/// <summary>
/// RV-82: a render never hands back a frame drawn with empty graph slots. render bakes the store's model and then posts;
/// an edit that lands in between is drawn by the pump on a model no render baked. Here the edit lands at the very moment
/// the frame thread finds the request waiting, and <c>SheetVersions.Baked</c>, render's loop, bakes that model and draws again.
/// </summary>
[Trait("Tier", "unit")]
public sealed class RenderBakedFrameTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "carve-bakedframe-" + Guid.NewGuid().ToString("N"));
    private readonly ModelStore _store = new();
    private readonly GraphBakes _bakes = new();
    private readonly Scene _scene = new();
    private readonly QueueWithAHook _queue;
    private readonly RenderPump _pump;

    public RenderBakedFrameTests()
    {
        Directory.CreateDirectory(_root);
        var graph = Path.Combine(_root, "flat.texgraph");
        File.WriteAllText(graph, """{ "version": 2, "size": 64, "seed": 1, "nodes": { "c": { "type": "colour", "params": { "colour": "#808080" } }, "o": { "type": "output", "inputs": { "in": "c" }, "params": { "role": "base_colour" } } } }""");
        _queue = new QueueWithAHook(_store);
        _pump = new RenderPump(_queue, _store, _scene, _bakes);
        _store.Replace(TestMeshes.Make(TestMeshes.CubeVertices, TestMeshes.CubeFaces));
        Assert.NotNull(_store.DefineMaterial("define_material", new Material("flat", BaseColourTexture: new MaterialGraph(graph, "base_colour", 64)), out _, out var problems));
        Assert.Empty(problems);
        _pump.Step();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>A new model, as a define_material that changes nothing drawn would make: no render has baked it.</summary>
    private void Edit()
    {
        var model = _store.Current.Model;
        _store.OpenModel(model.WithMaterial(model.Materials.Single() with { Roughness = 0.5f }), "define_material");
    }

    /// <summary>What render's <c>RenderTiles</c> does: resolve the store's model, post, and wait for the frame thread.</summary>
    private async Task<(RenderedFrame[]? Frames, string? Error)> BakeAndRender()
    {
        _bakes.Resolve(_store.Current.Model, CancellationToken.None);
        var frame = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));
        for (var i = 0; i < 20 && !frame.IsCompleted; i++) _pump.Step();
        return ([await frame], null);
    }

    [Fact]
    public async Task An_edit_between_the_bake_and_the_frame_is_baked_and_drawn_again_never_returned_with_empty_slots()
    {
        _queue.OnWaiting = Edit;

        var (frames, error) = await SheetVersions.Baked(BakeAndRender, f => f.Unbaked, 3);

        Assert.Null(error);
        // The first frame was drawn on the edit with its graph slot empty; the one returned is drawn with its bake.
        Assert.Equal(2, _scene.Drawn.Count);
        Assert.Null(_scene.Drawn[0].Single().BaseColourTexture);
        Assert.IsType<MaterialTexture>(_scene.Drawn[1].Single().BaseColourTexture);
        Assert.False(Assert.Single(frames!).Unbaked);
        Assert.Equal(_store.Current.Version, frames![0].Version);
    }

    [Fact]
    public async Task An_edit_racing_every_try_is_an_error_not_a_frame_with_empty_slots()
    {
        _queue.OnWaiting = Edit;
        _queue.Again = true;

        var (frames, error) = await SheetVersions.Baked(BakeAndRender, f => f.Unbaked, 3);

        Assert.Null(frames);
        Assert.Equal("The model changed between its bake and its frame on each of 3 tries, so its graph slots would be drawn empty; call render again once the edits have settled.", error);
    }

    [Fact]
    public async Task With_no_edit_the_frame_is_drawn_once_with_its_bakes()
    {
        var (frames, _) = await SheetVersions.Baked(BakeAndRender, f => f.Unbaked, 3);

        Assert.False(Assert.Single(frames!).Unbaked);
        Assert.IsType<MaterialTexture>(Assert.Single(_scene.Drawn).Single().BaseColourTexture);
    }

    /// <summary>
    /// The <c>uvs</c> look (RV-82): its graph image is baked on the snapshot, and the pump serves the request on the model current
    /// then, so an edit between them would lay another model's layout over the image. That frame is stale, and the model is drawn again.
    /// </summary>
    [Fact]
    public async Task A_uvs_frame_drawn_on_a_later_model_than_its_graph_image_is_drawn_again()
    {
        _queue.OnWaiting = Edit;
        var tries = 0;
        async Task<((RenderedFrame Frame, bool Stale)[]? Frames, string? Error)> BakeImageAndRender()
        {
            tries++;
            var bakedOn = _store.Current.Version; // what RunUvs bakes its image on
            var frame = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front), Projection.Orthographic, 256, Look.Uvs, LabelSet.None));
            for (var i = 0; i < 20 && !frame.IsCompleted; i++) _pump.Step();
            var drawn = await frame;
            return ([(drawn, SheetVersions.UvImageStale(graphImage: true, bakedOn, drawn.Version))], null);
        }

        var (frames, error) = await SheetVersions.Baked(BakeImageAndRender, t => t.Stale, 3, SheetVersions.UvImageDrawn);

        Assert.Null(error);
        Assert.Equal(2, tries); // the first frame was drawn on the edit, a model the image was not baked on
        Assert.Equal(_store.Current.Version, Assert.Single(frames!).Frame.Version);
        _queue.Again = true;
        Assert.Equal("The model changed between its bake and its frame on each of 3 tries, so its graph image would lie under another model's layout; call render again once the edits have settled.",
            (await SheetVersions.Baked(BakeImageAndRender, t => t.Stale, 3, SheetVersions.UvImageDrawn)).Error);
    }

    /// <summary>Runs an edit at the moment the frame thread finds a request waiting, once or (<see cref="Again"/>) every time.</summary>
    private sealed class QueueWithAHook(ModelStore store) : RenderQueue(store)
    {
        public Action? OnWaiting { get; set; }

        public bool Again { get; set; }

        public override bool TryPeek(out RenderRequest? request)
        {
            var waiting = base.TryPeek(out request);
            if (waiting && OnWaiting is { } hook)
            {
                if (!Again) OnWaiting = null;
                hook();
            }

            return waiting;
        }
    }

    /// <summary>Records the materials each frame is drawn with and completes it as RenderSystem does.</summary>
    private sealed class Scene : IRenderScene
    {
        private IReadOnlyList<Material> _materials = [];

        public List<IReadOnlyList<Material>> Drawn { get; } = [];

        public void ShowMaterials(IReadOnlyList<Material> materials) => _materials = materials;

        public void ShowMesh(PolygonMesh mesh)
        {
        }

        public void ApplyView(RenderSpec spec)
        {
        }

        public void Render(RenderRequest request)
        {
            Drawn.Add(_materials);
            request.Complete(new RenderedFrame(new RenderTargetPixels(new byte[4], 1, 1, TextureFormat.Rgba8Unorm), 0, 0) { Version = request.ServedVersion, Unbaked = request.ServedUnbaked });
        }

        public void Report(Exception exception, string message) => throw new InvalidOperationException(message, exception);
    }
}

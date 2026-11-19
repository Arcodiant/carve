using System.Numerics;
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

        /// <summary>The materials last given with a mesh, as RenderSystem keeps them to look each face's material up by name.</summary>
        public IReadOnlyList<Material> Materials { get; private set; } = [];

        public void ShowMaterials(IReadOnlyList<Material> materials) => Materials = materials;

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

        public void ShowGlb(GlbScene? glb) => Log.Add(glb is null ? "model" : "glb");

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
    public void Target_weights_draw_the_morphed_mesh_and_the_rest_again_when_they_go()
    {
        var pump = Started();
        var rest = Mesh();
        var target = MorphTarget.FromPositions(rest, "Tall", new Dictionary<int, Vector3> { [2] = rest.Vertices[2] + new Vector3(0, 1, 0) }).Target!;
        _store.Replace(rest.WithTargets(MorphTargets.Of([target])));
        var front = CameraSpec.ForView(RenderView.Front);

        _ = _queue.Post(new RenderSpec(front) { Targets = new TargetWeights(new Dictionary<string, float> { ["Tall"] = 0.5f }) });
        for (var i = 0; i < 6; i++) pump.Step();
        Assert.Equal(rest.Vertices[2].Y + 0.5f, _scene.Shown!.Vertices[2].Y, 1e-6f);
        Assert.Null(_scene.Shown.Targets);

        _ = _queue.Post(new RenderSpec(front));
        for (var i = 0; i < 6; i++) pump.Step();
        Assert.Equal(rest.Vertices[2], _scene.Shown!.Vertices[2]);
    }

    [Fact]
    public void A_pose_and_target_weights_draw_the_morph_first_and_then_the_skin()
    {
        var pump = Started();
        var rest = Mesh();
        var target = MorphTarget.FromPositions(rest, "Tall", new Dictionary<int, Vector3> { [2] = rest.Vertices[2] + new Vector3(0, 1, 0) }).Target!;
        var weights = VertexWeights.None;
        for (var v = 0; v < rest.Vertices.Count; v++) weights = weights.With([v], [new Influence(0, 1)]);
        Assert.NotNull(_store.DefineSkeleton(RigTestHelpers.Bones("Root", "Tip"), "define_skeleton", out _));
        _store.Replace(rest.WithWeights(weights).WithTargets(MorphTargets.Of([target])));
        var shift = new Vector3(3, 0, 0);
        var pose = new Pose(new Dictionary<string, (Quaternion?, Vector3?)> { ["Root"] = (null, new Vector3(0, 1, 0) + shift) });

        _ = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)) { Pose = pose, Targets = new TargetWeights(new Dictionary<string, float> { ["Tall"] = 0.5f }) });
        for (var i = 0; i < 6; i++) pump.Step();

        // Vertex 2 is raised by half its delta (the morph) and carried by the whole bone's shift (the skin); the unmorphed vertex 0 only by the shift.
        Assert.Equal(rest.Vertices[2] + new Vector3(0, 0.5f, 0) + shift, _scene.Shown!.Vertices[2]);
        Assert.Equal(rest.Vertices[0] + shift, _scene.Shown.Vertices[0]);
    }

    /// <summary>
    /// RV-72, by the person's ruling: a posed draw keeps each face's material, looked up by name in the materials render is
    /// given for that draw, as RenderSystem looks it up. Two rigged meshes, the second placed, so a face's material coming from
    /// the wrong mesh, or the materials or the assignment being dropped when posed, shows.
    /// </summary>
    [Fact]
    public void A_posed_draw_keeps_each_faces_material_from_the_models_materials()
    {
        var pump = Started();
        Material wood = new("wood", Roughness: 0.8f), steel = new("steel", Metallic: 1f, Roughness: 0.3f);
        var skeleton = Skeleton.Create([new BoneSpec("Root", null, Vector3.Zero, Quaternion.Identity)]).Skeleton!;
        var weights = VertexWeights.Of(Enumerable.Range(0, 8).Select(v => KeyValuePair.Create(v, new[] { new Influence(0, 1) })));
        var a = Mesh().WithStyle(FaceStyle.None.WithMaterial([1], "wood").WithMaterial([3], "steel")).WithWeights(weights);
        var b = Mesh().WithStyle(FaceStyle.None.WithMaterial([0, 5], "steel").WithMaterial([2], "wood")).WithWeights(weights);
        var (model, errors) = Model.Create([new("A", a), new("B", b)], skeleton, [], [wood, steel],
            [new Part("A"), new Part("B") { Translation = new Vector3(0, 0, 4), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f) }]);
        Assert.True(model is not null, string.Join("; ", errors));
        _store.OpenModel(model!, "open");
        var shift = new Vector3(3, 0, 0);
        var pose = new Pose(new Dictionary<string, (Quaternion?, Vector3?)> { ["Root"] = (null, shift) });

        _ = _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)) { Pose = pose });
        for (var i = 0; i < 6; i++) pump.Step();

        var shown = _scene.Shown!;
        Assert.Equal(a.Vertices[0] + shift, shown.Vertices[0]);
        Material? Drawn(int face) => shown.Style?.MaterialOf(face) is { } name ? _scene.Materials.Single(m => m.Name == name) : null;
        Material?[] expected = [null, wood, null, steel, null, null, steel, null, wood, null, null, steel];
        Assert.Equal(expected.Length, shown.Faces.Count);
        for (var f = 0; f < expected.Length; f++) Assert.True(expected[f] == Drawn(f), $"face {f}: drawn with {Drawn(f)?.Name ?? "no material"}, expected {expected[f]?.Name ?? "none"}");
    }

    [Fact]
    public void Equal_target_weights_are_equal_and_different_ones_are_not()
    {
        TargetWeights W(float w) => new(new Dictionary<string, float> { ["A"] = w });
        Assert.Equal(W(0.5f), W(0.5f));
        Assert.NotEqual(W(0.5f), W(0.25f));
        Assert.NotNull(W(0.5f));
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
    /// <summary>A glb of one triangle, prepared as render_glb prepares a file, through a loader that hands back a parsed model.</summary>
    internal static GlbScene Glb(params string[] warnings)
    {
        var errors = new List<string>();
        return GlbScene.Prepare(new OneModelLoader(warnings), [1, 2, 3], null, 0, errors) ?? throw new InvalidOperationException(string.Join("; ", errors));
    }

    internal sealed class OneModelLoader(string[] warnings) : Empyrean.IModelGeometryLoader
    {
        public Empyrean.ParsedModel Parse(byte[] modelBytes, Empyrean.ModelFormat format) =>
            new(new Empyrean.MeshData<Empyrean.PositionUvNormalVertex>(
                [new() { Position = new Vector3(0, 0, 0) }, new() { Position = new Vector3(2, 0, 0) }, new() { Position = new Vector3(0, 4, 0) }],
                [0, 1, 2], []), [], null!, null!, null!) { Warnings = warnings };

        public Empyrean.ParsedModel Parse(byte[] modelBytes, Empyrean.ModelFormat format, Empyrean.ModelParseOptions options) => Parse(modelBytes, format);

        public Empyrean.ParsedModel Load(Empyrean.ContentPath path) => throw new NotSupportedException();

        public Empyrean.ParsedModel Load(Empyrean.ContentPath path, Empyrean.ModelParseOptions options) => throw new NotSupportedException();

        public Empyrean.ParsedAnimationSet LoadAnimationSet(Empyrean.ContentPath path) => throw new NotSupportedException();

        public Empyrean.ParsedAnimationSet ParseAnimationSet(byte[] modelBytes) => throw new NotSupportedException();
    }

    [Fact]
    public void A_glb_is_put_in_and_taken_out_as_changes_of_the_scene_each_drawn_on_a_later_step()
    {
        var pump = Started();
        _scene.Log.Clear();
        var glb = Glb();
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)) { Glb = glb });
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)));

        // "|" marks the end of each step: putting the glb in, or taking it out, is all its step does.
        for (var i = 0; i < 6; i++)
        {
            pump.Step();
            _scene.Log.Add("|");
        }

        Assert.Equal(2, _scene.Rendered.Count);
        Assert.Same(glb, _scene.Rendered[0].Request.Spec.Glb);
        Assert.Null(_scene.Rendered[1].Request.Spec.Glb);
        Assert.Equal(["glb", "|", "view", "|", "render", "model", "|", "view", "|", "render", "|", "|"], _scene.Log.Select(l => l.Split(' ')[0]));
    }

    [Fact]
    public void A_glb_render_is_served_while_the_model_cannot_be_shown()
    {
        var pump = new RenderPump(_queue, _store, _scene);
        _scene.OnShow = _ => new InvalidOperationException("no device");
        pump.Step();
        _queue.Post(new RenderSpec(CameraSpec.ForView(RenderView.Front)) { Glb = Glb() });
        for (var i = 0; i < 3; i++) pump.Step();
        Assert.Single(_scene.Rendered);
    }

    [Fact]
    public void A_glb_is_framed_on_its_bounds_and_carries_the_loaders_warnings()
    {
        var glb = Glb("a skinned glTF's other meshes were dropped");
        Assert.Equal(new Vector3(0, 0, 0), glb.BoundsMin);
        Assert.Equal(new Vector3(2, 4, 0), glb.BoundsMax);
        Assert.Equal(DrawnSpace.Of(glb.BoundsMin, glb.BoundsMax), glb.Space);
        Assert.Equal((3, 1), (glb.Vertices, glb.Triangles));
        Assert.Null(glb.Palette);
        Assert.Equal("\nThe loader's warnings:\n- a skinned glTF's other meshes were dropped", glb.WarningsText);
        Assert.Equal("", Glb().WarningsText);
    }
}

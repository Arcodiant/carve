using System.Numerics;
using Carve.Core;
using Empyrean;
using Microsoft.Extensions.Logging;

namespace Carve.Server;

/// <summary>
/// Holds the scene and does what <see cref="RenderPump"/> decides, on the frame thread. The scene
/// itself is built here in the first <see cref="Update"/>; a failure to build it is the engine's.
/// </summary>
internal sealed class RenderSystem(
    ISceneManager scenes,
    IEntityManager entities,
    IGraphicsResourceManager resources,
    IOffscreenRenderer renderer,
    IRenderTargetReadback readback,
    IImmediateSurfaceRegistry surfaces,
    RenderComposition composition,
    RenderQueue queue,
    ModelStore store,
    ILogger<RenderSystem> log) : ISystem, IRenderScene, LookHolder<Mesh>.IParts
{
    private const string Scene = "carve";

    /// <summary>The colour of the model's surface: the one value, <see cref="GlbExport.BaseColor"/>, so the game sees what render shows.</summary>
    private static readonly Vector4 Surface = GlbExport.BaseColor;

    private Entity _light;
    private readonly List<Entity> _cameras = [];
    private Entity? _meshEntity;
    private Material? _nextMaterial;
    private Material[]? _nextSubmeshMaterials;
    private Entity? _edgeLines;
    private Entity? _orangeLines;
    private LookHolder<Mesh>? _looks;
    private MeshReport? _report;
    private Vector3 _drawnHalf;
    private DrawnSpace _space;
    private ViewFrame? _frame;
    private bool _composed;
    private RenderPump? _pump;

    private LookHolder<Mesh> Looks => _looks ??= new LookHolder<Mesh>(this);

    public void Initialise()
    {
    }

    public void FixedUpdate()
    {
    }

    public void Update()
    {
        if (_pump is null)
        {
            BuildScene();
            _pump = new RenderPump(queue, store, this);
            return;
        }

        _pump.Step();
    }

    public void Report(Exception exception, string message) => log.LogError(exception, "{Message}", message);

    private void BuildScene()
    {
        scenes.CreateScene(Scene);

        var ambient = scenes.CreateEntity(Scene);
        entities.AddComponent(ambient, new AmbientLight { Color = Vector3.One, Intensity = 0.25f });

        _light = scenes.CreateEntity(Scene);
        entities.AddComponent(_light, new DirectionalLight { Color = Vector3.One, Intensity = 0.9f });
        entities.AddComponent(_light, new Transform3D
        {
            Position = Vector3.Zero,
            Scale = Vector3.One,
            Orientation = ViewFraming.LightOrientation(RenderView.Front),
        });

        // The near and far here are placeholders: each view sets the whole projection by hand,
        // because the headless host does not apply a camera's projection.
        foreach (var size in RenderComposition.Sizes)
        {
            var camera = scenes.CreateEntity(Scene);
            entities.AddCamera3D(camera, ViewFraming.FieldOfViewY, 0.1f, 100f);
            entities.AddComponent(camera, new RenderTag(RenderComposition.CameraTag(size)));
            _cameras.Add(camera);
        }
    }

    public void ShowMesh(Carve.Core.PolygonMesh mesh)
    {
        var report = MeshReport.Create(mesh);
        var space = DrawnSpace.Of(report.BoundsMin, report.BoundsMax);
        Looks.Show(mesh, space);
        _report = report;
        _space = space;
        _drawnHalf = space.Apply(report.BoundsMax);
    }

    public IReadOnlyList<Func<Mesh?>> Make(Carve.Core.PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight)
    {
        var scene = LookMeshes.Compose(mesh, space, look, highlight);
        // The fill's material, put on its entity by Install with the meshes made here. A mesh with coloured faces drawn
        // in the shaded look is a submesh and a material per colour (D-6); anything else is the one mesh it always was.
        _nextMaterial = scene.FlatColour is { } flat ? Material.SolidColor(flat) : Material.LitMesh(Surface);
        _nextSubmeshMaterials = null;
        MeshData<PositionUvNormalVertex> data;
        if (scene.FlatColour is null && mesh.Style is { Colours.Count: > 0 })
        {
            data = MeshBridge.ToSubmeshedMeshData(mesh, space, out var colours);
            _nextSubmeshMaterials = colours
                .Select(c => Material.LitMesh(c is null ? Surface : FaceStyle.ToLinear(c))).ToArray();
        }
        else
        {
            data = MeshBridge.ToMeshData(mesh, space);
        }

        return
        [
            () => scene.VertexColours is { } colours ? resources.CreateMesh(data, colours) : resources.CreateMesh(data),
            () => scene.EdgeLines is { } edgeLines ? resources.CreateMesh(edgeLines) : null,
            () => scene.OrangeLines is { } orangeLines ? resources.CreateMesh(orangeLines) : null,
        ];
    }

    public void Install(Mesh fill, Mesh? edges, Mesh? orange)
    {
        if (_meshEntity is { } entity)
        {
            ref var instance = ref entities.GetComponent<MeshInstance>(entity);
            instance.Mesh = fill;
            instance.Material = _nextMaterial!;
            instance.SubmeshMaterials = _nextSubmeshMaterials!;
        }
        else
        {
            _meshEntity = AddInstance(fill, _nextMaterial!);
            entities.GetComponent<MeshInstance>(_meshEntity.Value).SubmeshMaterials = _nextSubmeshMaterials!;
        }

        _edgeLines = Swap(_edgeLines, edges, LookMeshes.Edge);
        _orangeLines = Swap(_orangeLines, orange, LookMeshes.Orange);
    }

    public void Destroy(Mesh mesh) => resources.Destroy(mesh);

    private Entity AddInstance(Mesh mesh, Material material, bool castsShadow = true)
    {
        var entity = scenes.CreateEntity(Scene);
        entities.AddComponent(entity, new MeshInstance
        {
            Mesh = mesh,
            Material = material,
            Mode = RenderMode.Mode3D,
            RenderLayerMask = RenderLayerMask.All,
            CastsShadow = castsShadow,
        });
        entities.AddComponent(entity, new Transform3D
        {
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = Vector3.One,
        });
        return entity;
    }

    /// <summary>Puts the next lines on a slot's entity; the entity is in the scene only while the look has such lines.</summary>
    private Entity? Swap(Entity? slot, Mesh? next, Vector4 colour)
    {
        if (next is null)
        {
            if (slot is { } gone)
            {
                entities.DestroyEntity(gone);
            }

            return null;
        }

        if (slot is { } entity)
        {
            entities.GetComponent<MeshInstance>(entity).Mesh = next;
            return entity;
        }

        return AddInstance(next, Material.SolidColor(colour), castsShadow: false);
    }

    public void ApplyView(RenderSpec spec)
    {
        Looks.Apply(spec.Mode, spec.Highlight, _space);

        // The target is in the mesh's own coordinates; the camera works in drawn space.
        var camera = spec.Camera;
        var drawn = camera.Target is { } target ? camera with { Target = _space.Apply(target) } : camera;
        var frame = ViewFraming.Frame(_drawnHalf, drawn, spec.Projection);
        _frame = frame;
        foreach (var entity in _cameras)
        {
            entities.GetOrAddComponent<WorldTransform>(entity) = new WorldTransform { Matrix = frame.World };
            entities.GetComponent<Camera>(entity).ProjectionMatrix = frame.Projection;
        }

        entities.GetComponent<Transform3D>(_light).Orientation = frame.LightOrientation;
    }

    /// <summary>Queues what this frame writes on the image, for the next <c>RenderFrame</c> to draw and drain; a plain render queues nothing.</summary>
    private void DrawOverlay(RenderSpec spec)
    {
        if (spec.Labels == LabelSet.None && spec.TileCaption is null)
        {
            return;
        }

        // A tagged surface exists once the host has composed, which a headless host does on its first
        // frame. Only an overlay needs it, so a render with none never draws an extra frame.
        if (!_composed)
        {
            renderer.RenderFrame();
            _composed = true;
        }

        var surface = surfaces.Get(RenderComposition.LabelTag(spec.Size));
        Vector2 Measure(string text) => surface.Text.MeasureText(text, 1);

        var labels = LabelLayout.Plan(Looks.Shown!, _space, _frame!, spec.Camera.Direction, spec.Projection, spec.Labels, spec.Size, Measure, spec.LabelWindow);
        foreach (var label in labels)
        {
            Draw(surface, label);
        }

        if (spec.TileCaption is { } caption)
        {
            Draw(surface, LabelLayout.Caption(caption, Measure));
        }
    }

    private static void Draw(IImmediateSurface surface, OverlayLabel label)
    {
        if (label.Dot is { } dot)
        {
            surface.Primitives.DrawCircle(dot, LabelLayout.DotRadius, LabelLayout.TextColour);
        }

        surface.Primitives.DrawRect(label.Backing, LabelLayout.BackingColour);
        surface.Text.DrawText(label.Text, label.TextAt, LabelLayout.TextColour);
    }

    public void Render(RenderRequest request)
    {
        try
        {
            var vertices = _report!.VertexCount;
            var faces = _report.FaceCount;
            var version = request.ServedVersion;

            DrawOverlay(request.Spec);

            // Complete by the time RenderFrame returns; the continuation runs on the thread pool.
            var read = readback.ReadAsync(composition.Target(request.Spec.Size));
            renderer.RenderFrame();
            _composed = true;
            _ = read.ContinueWith(
                task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        request.Complete(new RenderedFrame(task.Result, vertices, faces) { Version = version });
                    }
                    else
                    {
                        request.Fail(task.Exception?.GetBaseException() ?? new TaskCanceledException());
                    }
                },
                TaskScheduler.Default);
        }
        catch (Exception e)
        {
            request.Fail(e);
        }
    }
}

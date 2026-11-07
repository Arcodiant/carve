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
    RenderComposition composition,
    RenderQueue queue,
    ModelStore store,
    ILogger<RenderSystem> log) : ISystem, IRenderScene
{
    private const string Scene = "carve";

    /// <summary>The colour of the model's surface: the one value, <see cref="GlbExport.BaseColor"/>, so the game sees what render shows.</summary>
    private static readonly Vector4 Surface = GlbExport.BaseColor;

    private Entity _light;
    private readonly List<Entity> _cameras = [];
    private Entity? _meshEntity;
    private Mesh? _mesh;
    private readonly LineSlot _edgeLines = new();
    private readonly LineSlot _orangeLines = new();
    private Carve.Core.PolygonMesh? _shown;
    private Look _look;
    private Highlight _highlight = Highlight.None;
    private MeshReport? _report;
    private Vector3 _drawnHalf;
    private DrawnSpace _space;
    private RenderPump? _pump;

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
        PutLook(mesh, space, _look, _highlight);
        _shown = mesh;
        _report = report;
        _space = space;
        _drawnHalf = space.Apply(report.BoundsMax);
    }

    /// <summary>
    /// Draws a mesh in a look. The new meshes are made first, so a failure leaves the scene as it was,
    /// and each old handle goes once its entity holds the new one.
    /// </summary>
    private void PutLook(Carve.Core.PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight)
    {
        var scene = LookMeshes.Compose(mesh, space, look, highlight);
        var data = MeshBridge.ToMeshData(mesh, space);
        var next = scene.VertexColours is { } colours ? resources.CreateMesh(data, colours) : resources.CreateMesh(data);
        var material = scene.FlatColour is { } flat ? Material.SolidColor(flat) : Material.LitMesh(Surface);
        var edges = scene.EdgeLines is { } edgeLines ? resources.CreateMesh(edgeLines) : null;
        var orange = scene.OrangeLines is { } orangeLines ? resources.CreateMesh(orangeLines) : null;

        if (_meshEntity is { } entity)
        {
            ref var instance = ref entities.GetComponent<MeshInstance>(entity);
            instance.Mesh = next;
            instance.Material = material;
        }
        else
        {
            _meshEntity = AddInstance(next, material);
        }

        var old = _mesh;
        _mesh = next;
        if (old is not null)
        {
            resources.Destroy(old);
        }

        Swap(_edgeLines, edges, LookMeshes.Edge);
        Swap(_orangeLines, orange, LookMeshes.Orange);
    }

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

    /// <summary>Puts the next lines in a slot; the entity is in the scene only while the look has such lines.</summary>
    private void Swap(LineSlot slot, Mesh? next, Vector4 colour)
    {
        var old = slot.Mesh;
        slot.Mesh = next;
        if (next is null)
        {
            if (slot.Entity is { } gone)
            {
                entities.DestroyEntity(gone);
                slot.Entity = null;
            }
        }
        else if (slot.Entity is { } entity)
        {
            entities.GetComponent<MeshInstance>(entity).Mesh = next;
        }
        else
        {
            slot.Entity = AddInstance(next, Material.SolidColor(colour), castsShadow: false);
        }

        if (old is not null)
        {
            resources.Destroy(old);
        }
    }

    /// <summary>The lines of a look in one colour.</summary>
    private sealed class LineSlot
    {
        public Entity? Entity { get; set; }

        public Mesh? Mesh { get; set; }
    }

    public void ApplyView(RenderSpec spec)
    {
        // A camera change alone rebuilds nothing.
        if (_shown is { } shown && (spec.Mode != _look || spec.Highlight != _highlight))
        {
            PutLook(shown, _space, spec.Mode, spec.Highlight);
        }

        _look = spec.Mode;
        _highlight = spec.Highlight;

        // The target is in the mesh's own coordinates; the camera works in drawn space.
        var camera = spec.Camera;
        var drawn = camera.Target is { } target ? camera with { Target = _space.Apply(target) } : camera;
        var frame = ViewFraming.Frame(_drawnHalf, drawn, spec.Projection);
        foreach (var entity in _cameras)
        {
            entities.GetOrAddComponent<WorldTransform>(entity) = new WorldTransform { Matrix = frame.World };
            entities.GetComponent<Camera>(entity).ProjectionMatrix = frame.Projection;
        }

        entities.GetComponent<Transform3D>(_light).Orientation = frame.LightOrientation;
    }

    public void Render(RenderRequest request)
    {
        try
        {
            var vertices = _report!.VertexCount;
            var faces = _report.FaceCount;

            // Complete by the time RenderFrame returns; the continuation runs on the thread pool.
            var read = readback.ReadAsync(composition.Target(request.Spec.Size));
            renderer.RenderFrame();
            _ = read.ContinueWith(
                task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        request.Complete(new RenderedFrame(task.Result, vertices, faces));
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

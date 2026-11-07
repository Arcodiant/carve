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
    private Entity _camera;
    private Entity? _meshEntity;
    private Mesh? _mesh;
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
        _camera = scenes.CreateEntity(Scene);
        entities.AddCamera3D(_camera, ViewFraming.FieldOfViewY, 0.1f, 100f);
        entities.AddComponent(_camera, new RenderTag(RenderComposition.CameraTag));
    }

    public void ShowMesh(Carve.Core.PolygonMesh mesh)
    {
        var report = MeshReport.Create(mesh);
        var space = DrawnSpace.Of(report.BoundsMin, report.BoundsMax);
        var next = resources.CreateMesh(MeshBridge.ToMeshData(mesh, space));

        if (_meshEntity is { } entity)
        {
            entities.GetComponent<MeshInstance>(entity).Mesh = next;
        }
        else
        {
            entity = scenes.CreateEntity(Scene);
            entities.AddComponent(entity, new MeshInstance
            {
                Mesh = next,
                Material = Material.LitMesh(Surface),
                Mode = RenderMode.Mode3D,
                RenderLayerMask = RenderLayerMask.All,
            });
            entities.AddComponent(entity, new Transform3D
            {
                Position = Vector3.Zero,
                Orientation = Quaternion.Identity,
                Scale = Vector3.One,
            });
            _meshEntity = entity;
        }

        // The entity holds the new mesh from here on, so the old handle goes first.
        var old = _mesh;
        _mesh = next;
        _report = report;
        _space = space;
        _drawnHalf = space.Apply(report.BoundsMax);
        if (old is not null)
        {
            resources.Destroy(old);
        }
    }

    public void ApplyView(CameraSpec camera)
    {
        // The target is in the mesh's own coordinates; the camera works in drawn space.
        var drawn = camera.Target is { } target ? camera with { Target = _space.Apply(target) } : camera;
        var frame = ViewFraming.Frame(_drawnHalf, drawn);
        entities.GetOrAddComponent<WorldTransform>(_camera) = new WorldTransform { Matrix = frame.World };
        entities.GetComponent<Camera>(_camera).ProjectionMatrix = frame.Projection;
        entities.GetComponent<Transform3D>(_light).Orientation = frame.LightOrientation;
    }

    public void Render(RenderRequest request)
    {
        try
        {
            var vertices = _report!.VertexCount;
            var faces = _report.FaceCount;

            // Complete by the time RenderFrame returns; the continuation runs on the thread pool.
            var read = readback.ReadAsync(composition.Target);
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

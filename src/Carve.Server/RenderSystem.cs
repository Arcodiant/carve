using System.Numerics;
using Carve.Core;
using Empyrean;

namespace Carve.Server;

/// <summary>
/// Holds the scene and serves render requests on the frame thread. A frame reads the lighting and
/// transforms published earlier in the same loop iteration, so anything that changes the scene
/// (creating it, a new mesh, a new view) returns from <see cref="Update"/> and renders on a later one.
/// </summary>
internal sealed class RenderSystem(
    ISceneManager scenes,
    IEntityManager entities,
    IGraphicsResourceManager resources,
    IOffscreenRenderer renderer,
    IRenderTargetReadback readback,
    RenderComposition composition,
    RenderQueue queue,
    ModelStore store) : ISystem
{
    private const string Scene = "carve";

    private static readonly Vector4 Surface = new(0.72f, 0.72f, 0.70f, 1f);

    private Entity _light;
    private Entity _camera;
    private Entity? _meshEntity;
    private Mesh? _mesh;
    private bool _built;
    private long _version;
    private MeshReport? _report;
    private RenderView _view = RenderView.Front;

    public void Initialise()
    {
    }

    public void FixedUpdate()
    {
    }

    public void Update()
    {
        if (!_built)
        {
            BuildScene();
            _built = true;
            return;
        }

        var current = store.Current;
        if (current.Version != _version)
        {
            ShowMesh(current);
            return;
        }

        while (queue.TryPeek(out var next))
        {
            if (next!.View != _view)
            {
                _view = next.View;
                ApplyView();
                return;
            }

            queue.TryTake(out var request);
            Render(request!);
        }
    }

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
            Orientation = ViewFraming.LightOrientation(_view),
        });

        // The near and far here are placeholders: each view sets the whole projection by hand,
        // because the headless host does not apply a camera's projection.
        _camera = scenes.CreateEntity(Scene);
        entities.AddCamera3D(_camera, ViewFraming.FieldOfViewY, 0.1f, 100f);
        entities.AddComponent(_camera, new RenderTag(RenderComposition.CameraTag));
    }

    private void ShowMesh(ModelSnapshot snapshot)
    {
        var mesh = snapshot.Mesh!;
        var next = resources.CreateMesh(MeshBridge.ToMeshData(mesh));

        if (_meshEntity is { } entity)
        {
            entities.GetComponent<MeshInstance>(entity).Mesh = next;
            resources.Destroy(_mesh!);
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

        _mesh = next;
        _report = MeshReport.Create(mesh);
        _version = snapshot.Version;
        ApplyView();
    }

    private void ApplyView()
    {
        if (_report is not { } report)
        {
            return;
        }

        var frame = ViewFraming.Frame(report.BoundsMin, report.BoundsMax, _view);

        Matrix4x4.Invert(frame.View, out var world);
        entities.GetOrAddComponent<WorldTransform>(_camera) = new WorldTransform { Matrix = world };
        entities.GetComponent<Camera>(_camera).ProjectionMatrix = frame.Projection;
        entities.GetComponent<Transform3D>(_light).Orientation = frame.LightOrientation;
    }

    private void Render(RenderRequest request)
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

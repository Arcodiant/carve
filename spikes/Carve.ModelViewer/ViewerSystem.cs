using System.Numerics;
using System.Text.Json;
using Empyrean;

namespace Carve.ModelViewer;

/// <summary>
/// The game: on its first frame it mounts the content folder and loads the model as any game does,
/// then poses the camera for each view in turn and captures the presented frame.
/// </summary>
internal sealed class ViewerSystem(
    ViewerOptions options,
    ViewerRun run,
    IContentRegistry content,
    IModelLoader models,
    ISceneManager scenes,
    IEntityManager entities,
    IScreenshotService screenshots,
    IGameLifecycle lifecycle) : ISystem
{
    private const string Scene = "viewer";

    /// <summary>Frames left between posing a view and capturing it, so the pose has reached the frame.</summary>
    private const int SettleFrames = 3;

    private Entity _camera;
    private Model? _model;
    private List<ViewFrame> _frames = [];
    private int _next;
    private int _settle;
    private Task? _capture;
    private bool _finished;

    public void Initialise()
    {
    }

    public void FixedUpdate()
    {
    }

    public void Update()
    {
        if (_finished)
        {
            return;
        }

        try
        {
            Step();
        }
        catch (Exception e)
        {
            Stop($"{e.GetType().Name}: {e.Message}");
        }
    }

    private void Step()
    {
        if (_model is null)
        {
            if (Load())
            {
                Pose();
            }

            return;
        }

        if (_capture is not null)
        {
            if (!_capture.IsCompleted)
            {
                return;
            }

            if (_capture.IsFaulted)
            {
                Stop($"capture of view {_frames[_next].View.Name} failed: {_capture.Exception!.GetBaseException().Message}");
                return;
            }

            _capture = null;
            if (++_next == _frames.Count)
            {
                File.WriteAllText(Path.Combine(options.Out, "view.json"), Describe());
                _finished = true;
                lifecycle.RequestStop();
                return;
            }

            Pose();
            return;
        }

        if (_settle-- == 0)
        {
            _capture = screenshots.CaptureAsync(Path.Combine(options.Out, _frames[_next].View.Name + ".png"));
        }
    }

    /// <summary>As a game does: mount, load by content path, one instance at identity. Nothing else touches it.</summary>
    private bool Load()
    {
        Model model;
        try
        {
            if (!Directory.Exists(options.Content))
            {
                throw new DirectoryNotFoundException("the content folder does not exist");
            }

            content.Mount(ContentSources.Directory(options.Content));
            model = models.LoadModel(new ContentPath(options.Model));
        }
        catch (Exception e)
        {
            Stop($"could not load {options.Model} from {options.Content}: {e.Message}");
            return false;
        }

        if (model.Material is null)
        {
            Stop($"{options.Model} declares no material; the viewer shows the file's own and has no other");
            return false;
        }

        Directory.CreateDirectory(options.Out);
        _frames = options.Views
            .Select(view => ViewFrame.Fit(view, model.Mesh.Bounds.Min, model.Mesh.Bounds.Max))
            .ToList();
        BuildScene(model);
        _model = model;
        return true;
    }

    private void BuildScene(Model model)
    {
        scenes.CreateScene(Scene);
        scenes.SetSceneActive(Scene, true);

        var ambient = scenes.CreateEntity(Scene);
        entities.AddComponent(ambient, new AmbientLight { Color = Vector3.One, Intensity = ViewerLook.AmbientIntensity });

        var sun = scenes.CreateEntity(Scene);
        entities.AddComponent(sun, new DirectionalLight { Color = Vector3.One, Intensity = ViewerLook.SunIntensity });
        entities.GetOrAddComponent<WorldTransform>(sun) = new WorldTransform { Matrix = ViewerLook.SunWorld() };

        var instance = scenes.CreateEntity(Scene);
        entities.AddComponent(instance, new MeshInstance
        {
            Mesh = model.Mesh,
            Material = model.Material!,
            SubmeshMaterials = model.SubmeshMaterials,
            Mode = RenderMode.Mode3D,
        });
        entities.AddComponent(instance, new Transform3D
        {
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = Vector3.One,
        });

        // One near and far that hold for every view: each eye is outside the bounding sphere.
        var radius = (model.Mesh.Bounds.Max - model.Mesh.Bounds.Min).Length() * 0.5f;
        var centre = (model.Mesh.Bounds.Min + model.Mesh.Bounds.Max) * 0.5f;
        var furthest = _frames.Max(f => (f.Eye - centre).Length());
        _camera = scenes.CreateEntity(Scene);
        entities.AddCamera3D(_camera, ViewFrame.FieldOfViewY, radius * 0.1f, (furthest + radius) * 1.1f);
    }

    private void Pose()
    {
        entities.GetOrAddComponent<WorldTransform>(_camera) = new WorldTransform { Matrix = _frames[_next].World };
        _settle = SettleFrames;
    }

    private void Stop(string message)
    {
        run.Fail(message);
        _finished = true;
        lifecycle.RequestStop();
    }

    private string Describe()
    {
        var mesh = _model!.Mesh;
        var triangles = (mesh.IndexCount > 0 ? mesh.IndexCount : mesh.VertexCount) / 3;
        // An empty entry means the submesh takes the base material.
        var materials = _model.SubmeshMaterials is { Length: > 0 } submeshes
            ? submeshes.Select(m => m ?? _model.Material!).ToArray()
            : [_model.Material!];

        return JsonSerializer.Serialize(
            new
            {
                model = options.Model,
                triangles,
                bounds = new { min = Numbers(mesh.Bounds.Min), max = Numbers(mesh.Bounds.Max) },
                materials = materials.Select(m => new
                {
                    baseColor = new[] { m.Color.X, m.Color.Y, m.Color.Z, m.Color.W },
                    metallic = m.Metallic,
                    roughness = m.Roughness,
                    pbr = m.Flags.HasFlag(MaterialFlags.Pbr),
                }),
                views = _frames.Select(f => new
                {
                    name = f.View.Name,
                    direction = Numbers(f.View.Direction),
                    eye = Numbers(f.Eye),
                }),
                sun = new
                {
                    toSun = Numbers(ViewerLook.ToSun),
                    intensity = ViewerLook.SunIntensity,
                    ambient = ViewerLook.AmbientIntensity,
                },
            },
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static float[] Numbers(Vector3 v) => [v.X, v.Y, v.Z];
}

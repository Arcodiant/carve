using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    IGameLifecycle lifecycle,
    IConfiguration configuration,
    IServiceProvider services) : ISystem
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
    private Entity _instance;
    private float[]? _weights;
    private WeightPanel? _panel;
    private Label? _panelText;
    private IInputService? _input;
    private bool _animated;

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
                if (Windowed)
                {
                    // The captures are made, with no panel in them; now the window stays for a person to drive.
                    BeginInteractive();
                    return;
                }

                _finished = true;
                lifecycle.RequestStop();
                return;
            }

            Pose();
            return;
        }

        if (_panel is not null)
        {
            Interact();
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
            model = models.LoadModel(new ContentPath(options.Model), generateMipmaps: true);
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
        if (!StartWeights(model))
        {
            return false;
        }

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

        if (options.Sky)
        {
            // The same fixed sky as Carve's render (RenderSky.Sky in src/Carve.Hosting is this expression; the viewer links no Carve code, so it is repeated here), unchanged: its own sun, baked once, and it does not follow the viewer's sun below.
            entities.AddComponent(scenes.CreateEntity(Scene), ProceduralSky.Default);
        }

        var sun = scenes.CreateEntity(Scene);
        entities.AddComponent(sun, new DirectionalLight { Color = Vector3.One, Intensity = ViewerLook.SunIntensity });
        entities.GetOrAddComponent<WorldTransform>(sun) = new WorldTransform { Matrix = ViewerLook.SunWorld() };

        var instance = scenes.CreateEntity(Scene);
        _instance = instance;
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

        if (_weights is not null)
        {
            entities.AddComponent(instance, new MorphWeights { Weights = _weights });
        }

        // One near and far that hold for every view: each eye is outside the bounding sphere.
        var radius = (model.Mesh.Bounds.Max - model.Mesh.Bounds.Min).Length() * 0.5f;
        var centre = (model.Mesh.Bounds.Min + model.Mesh.Bounds.Max) * 0.5f;
        var furthest = _frames.Max(f => (f.Eye - centre).Length());
        _camera = scenes.CreateEntity(Scene);
        entities.AddCamera3D(_camera, ViewFrame.FieldOfViewY, radius * 0.1f, (furthest + radius) * 1.1f);
    }

    /// <summary>With no window the viewer is the headless capture of old; the key is the engine's own switch.</summary>
    private bool Windowed => !(configuration.GetValue<bool?>("Empyrean:Graphics:VirtualSurface") ?? true);

    /// <summary>
    /// The starting weights, as a game sets them: each name resolved once with <c>IndexOfTarget</c> into a dense array.
    /// A name the file lacks, or weights for a file with none, stop the run with the file's own names.
    /// </summary>
    private bool StartWeights(Model model)
    {
        var requested = options.Targets ?? [];
        if (model.MorphTargets is not { } set)
        {
            if (requested.Count > 0)
            {
                Stop($"--targets given, but {options.Model} has no morph targets");
                return false;
            }

            return true;
        }

        var names = set.TargetNames;
        foreach (var (name, _) in requested)
        {
            if (!names.Contains(name))
            {
                Stop($"--targets: {options.Model} has no target named '{name}'; its targets are {string.Join(", ", names.Select(n => $"'{n}'"))}");
                return false;
            }
        }

        _weights = new float[set.TargetCount];
        foreach (var (name, weight) in requested)
        {
            _weights[set.IndexOfTarget(name)] = weight;
        }

        return true;
    }

    /// <summary>The panel, keys and clip of the window: built once the captures are done, so no capture has the panel in it.</summary>
    private void BeginInteractive()
    {
        var model = _model!;
        var clip = model.Skeleton is not null && model.Animations is { Length: > 0 } ? model.Animations[0] : null;
        var names = model.MorphTargets?.TargetNames ?? [];
        if (names.Count == 0 && clip is null)
        {
            _finished = true;
            lifecycle.RequestStop();
            return;
        }

        _input = services.GetRequiredService<IInputService>();
        _panel = new WeightPanel(names, _weights ?? [], clip?.Name);
        var ui = services.GetRequiredService<IUiService>();
        var panel = ui.Root(Scene, null!).Add(new Panel
        {
            Anchor = Anchor.TopLeft,
            Padding = 10f,
            Background = new Vector4(0f, 0f, 0f, 0.55f),
        });
        _panelText = panel.Add(new Label { Text = _panel.Text(), Scale = 1.5f });
        _next = 0;
        Pose();
    }

    private void Interact()
    {
        var input = _input!;
        var panel = _panel!;
        var changed = false;
        if (input.WasKeyPressedThisFrame(Key.Tab))
        {
            panel.Next();
            changed = true;
        }

        var steps = (input.WasKeyPressedThisFrame(Key.Right) ? 1 : 0) - (input.WasKeyPressedThisFrame(Key.Left) ? 1 : 0);
        if (steps != 0)
        {
            panel.Nudge(steps);
            changed = true;
        }

        if (input.WasKeyPressedThisFrame(Key.Digit0) || input.WasKeyPressedThisFrame(Key.Keypad0))
        {
            panel.Reset();
            changed = true;
        }

        if (input.WasKeyPressedThisFrame(Key.Space) && panel.TogglePlay())
        {
            var model = _model!;
            if (!_animated)
            {
                entities.AddComponent(_instance, new Animator { Skeleton = model.Skeleton, Clip = model.Animations![0], Loop = true });
                _animated = true;
            }

            entities.GetComponent<Animator>(_instance).Playing = panel.Playing;
            changed = true;
        }

        if (changed)
        {
            // The weights array is the component's own, written in place; the panel redraws from the same numbers.
            _panelText!.Text = panel.Text();
        }
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
        // Interim until engine need 3: the engine's Material has no name to report (D-1, Engine needs), so view.json lists none.
        // An empty entry means the submesh takes the base material.
        var materials = _model.SubmeshMaterials is { Length: > 0 } submeshes
            ? submeshes.Select(m => m ?? _model.Material!).ToArray()
            : [_model.Material!];

        var json = JsonSerializer.Serialize(
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
                    emissive = Numbers(m.EmissiveColor),
                    occlusion = m.Occlusion,
                    doubleSided = m.DoubleSided,
                    pbr = m.Flags.HasFlag(MaterialFlags.Pbr),
                    flags = m.Flags.ToString(),
                    textures = new
                    {
                        baseColor = Slot(m.Texture),
                        metallicRoughness = Slot(m.MetallicRoughnessTexture),
                        normal = Slot(m.NormalTexture),
                        emissive = Slot(m.EmissiveTexture),
                        occlusion = Slot(m.OcclusionTexture),
                    },
                }),
                views = _frames.Select(f => new
                {
                    name = f.View.Name,
                    direction = Numbers(f.View.Direction),
                    eye = Numbers(f.Eye),
                }),
                sky = options.Sky,
                sun = new
                {
                    toSun = Numbers(ViewerLook.ToSun),
                    intensity = ViewerLook.SunIntensity,
                    ambient = ViewerLook.AmbientIntensity,
                },
            },
            new JsonSerializerOptions { WriteIndented = true });
        if (options.Targets is not { Count: > 0 } requested)
        {
            return json;
        }

        // Only when asked for, so a run without --targets writes the file it always did.
        var node = JsonNode.Parse(json)!.AsObject();
        node["targets"] = new JsonObject(requested.Select(t => KeyValuePair.Create<string, JsonNode?>(t.Key, JsonValue.Create(t.Value))));
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Whether a texture slot is set, and if so its size and format.</summary>
    private static object Slot(Texture? texture) => texture is null
        ? new { set = false, width = 0, height = 0, format = (string?)null }
        : new { set = true, width = (int)texture.Width, height = (int)texture.Height, format = (string?)texture.Format.ToString() };

    private static float[] Numbers(Vector3 v) => [v.X, v.Y, v.Z];
}

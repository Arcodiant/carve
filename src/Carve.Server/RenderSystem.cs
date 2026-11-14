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
    private Entity? _seamLines;
    private readonly Dictionary<Vector4, Material> _lineMaterials = [];
    private Material? _layoutMaterial;
    private UvWindow _uvWindow = UvWindow.Unit;
    private Vector4 _nextEdgeColour = LookMeshes.Edge;
    private LookHolder<Mesh>? _looks;
    private MeshReport? _report;
    private Vector3 _drawnHalf;
    private DrawnSpace _space;
    private ViewFrame? _frame;
    private bool _composed;
    private RenderPump? _pump;
    private float[]? _weights;
    private Texture? _checker;
    private Material? _checkerMaterial;

    private LookHolder<Mesh> Looks => _looks ??= new LookHolder<Mesh>(this);

    /// <summary>
    /// The checker (<see cref="CheckerTexture"/>) as an engine texture, made on the frame thread the first time a look asks
    /// for it, sRGB, sampled nearest with repeat and no mipmaps, and held for the process's life, as the cameras and light
    /// are. A render that never asks for it makes nothing. T-58's layout look draws it again.
    /// </summary>
    internal Texture Checker => _checker ??= resources.CreateTexture(
        CheckerTexture.Pixels.Span, CheckerTexture.Size, CheckerTexture.Size, TextureFormat.Rgba8Srgb, SamplerDescriptions.PixelArt, generateMipmaps: false);

    /// <summary>The checker look's one material: lit from the mesh's normals, the checker times each vertex's colour.</summary>
    private Material CheckerMaterial => _checkerMaterial ??= Material.LitTextured(Checker);

    /// <summary>
    /// The <c>uvs</c> look's material (T-58): the checker unlit, times each vertex's colour, made the first time that look is
    /// drawn and held for the process's life. Without <see cref="MaterialFlags.Lit"/> the surface colour is emitted as it is.
    /// </summary>
    private Material LayoutMaterial => _layoutMaterial ??= new Material(
        MaterialFlags.Textured, Vector4.One, Checker, null!, 0, 1, Vector3.Zero, 1, null!, null!, null!, 0, default, null, 0, false);

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

    public void ShowMesh(Carve.Core.PolygonMesh mesh) => ShowMesh(mesh, mesh);

    public void ShowMesh(Carve.Core.PolygonMesh mesh, Carve.Core.PolygonMesh frameOn) => ShowMesh(mesh, frameOn, null);

    public void ShowMesh(Carve.Core.PolygonMesh mesh, Carve.Core.PolygonMesh frameOn, float[]? weights)
    {
        _weights = weights;
        var report = MeshReport.Create(mesh);
        var framing = ReferenceEquals(mesh, frameOn) ? report : MeshReport.Create(frameOn);
        var space = DrawnSpace.Of(framing.BoundsMin, framing.BoundsMax);
        Looks.Show(mesh, space);
        _report = report;
        _space = space;
        _drawnHalf = space.Apply(framing.BoundsMax);
    }

    public IReadOnlyList<Func<Mesh?>> Make(Carve.Core.PolygonMesh mesh, DrawnSpace space, Look look, Highlight highlight, ElementWindow? faces)
    {
        if (look == Look.Uvs)
        {
            // The layout replaces the model: a flat scene of its own, framed by ApplyView on the window it is drawn in.
            var layout = UvLayout.Compose(mesh, faces ?? new ElementWindow(0, 0, mesh.Faces.Count), highlight);
            _uvWindow = layout.Window;
            _nextMaterial = LayoutMaterial;
            _nextSubmeshMaterials = null;
            _nextEdgeColour = UvLayout.Edge;
            return
            [
                () => resources.CreateMesh(layout.Fill, layout.Colours),
                () => resources.CreateMesh(layout.Edges),
                () => null,
                () => layout.Seams is { } seams ? resources.CreateMesh(seams) : null,
            ];
        }

        _nextEdgeColour = LookMeshes.Edge;
        // The pump puts a mesh in under the previous look before it applies the next: with no weights to hand, draw it shaded for now.
        var scene = LookMeshes.Compose(mesh, space, look == Look.Weights && _weights is null ? Look.Shaded : look, highlight, _weights);
        // The fill's material, put on its entity by Install with the meshes made here. A mesh with coloured faces drawn
        // in the shaded look is a submesh and a material per colour (D-6); anything else is the one mesh it always was.
        _nextMaterial = scene.FlatColour is { } flat ? Material.SolidColor(flat) : Material.LitMesh(Surface);
        _nextSubmeshMaterials = null;
        MeshData<PositionUvNormalVertex> data;
        if (scene.Uvs is { } uvs)
        {
            // The checker: one mesh and one material whatever the face colours, which it replaces.
            _nextMaterial = CheckerMaterial;
            data = MeshBridge.ToMeshData(mesh, space, uvs);
        }
        else if (scene.FlatColour is null && mesh.Style is { Colours.Count: > 0 })
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
            () => null,
        ];
    }

    public void Install(Mesh fill, Mesh? edges, Mesh? orange, Mesh? seams)
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

        _edgeLines = Swap(_edgeLines, edges, _nextEdgeColour);
        _orangeLines = Swap(_orangeLines, orange, LookMeshes.Orange);
        _seamLines = Swap(_seamLines, seams, UvLayout.Seam);
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

    /// <summary>
    /// Puts the next lines on a slot's entity, in <paramref name="colour"/>; the entity is in the scene only while the look has
    /// such lines. One material per colour, made once, so a slot keeps the very material it had while its colour stays.
    /// </summary>
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

        if (!_lineMaterials.TryGetValue(colour, out var material))
        {
            _lineMaterials[colour] = material = Material.SolidColor(colour);
        }

        if (slot is { } entity)
        {
            ref var instance = ref entities.GetComponent<MeshInstance>(entity);
            instance.Mesh = next;
            instance.Material = material;
            return entity;
        }

        return AddInstance(next, material, castsShadow: false);
    }

    public void ApplyView(RenderSpec spec)
    {
        Looks.Apply(spec.Mode, spec.Highlight, _space, spec.Mode == Look.Uvs ? spec.UvFaces : null);
        if (spec.Mode == Look.Uvs)
        {
            ApplyLayoutView();
            return;
        }

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

    /// <summary>The <c>uvs</c> look's camera: straight down -Z with +Y up, orthographic over the window, which fills the frame.</summary>
    private void ApplyLayoutView()
    {
        var side = (float)_uvWindow.Side;
        var eye = new Vector3(0, 0, UvLayout.Eye);
        var view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitY);
        Matrix4x4.Invert(view, out var world);
        var frame = new ViewFrame(eye, world, view, Matrix4x4.CreateOrthographic(side, side, 1, 2 * UvLayout.Eye), ViewFraming.LightOrientation(RenderView.Front));
        _frame = frame;
        foreach (var entity in _cameras)
        {
            entities.GetOrAddComponent<WorldTransform>(entity) = new WorldTransform { Matrix = frame.World };
            entities.GetComponent<Camera>(entity).ProjectionMatrix = frame.Projection;
        }
    }

    /// <summary>Queues what this frame writes on the image, for the next <c>RenderFrame</c> to draw and drain; a plain render queues nothing.</summary>
    private void DrawOverlay(RenderSpec spec)
    {
        if (spec.Mode != Look.Uvs && spec.Labels == LabelSet.None && spec.TileCaption is null && spec.Bones is null)
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

        var labels = spec.Mode == Look.Uvs
            ? LabelLayout.UvLabels(_uvWindow, spec.Size, spec.Labels == LabelSet.Faces ? UvLayout.FaceLabels(Looks.Shown!, spec.UvFaces ?? new ElementWindow(0, 0, Looks.Shown!.Faces.Count)) : [], Measure)
            : LabelLayout.Plan(Looks.Shown!, _space, _frame!, spec.Camera.Direction, spec.Projection, spec.Labels, spec.Size, Measure, spec.LabelWindow);
        foreach (var label in labels)
        {
            Draw(surface, label);
        }

        // The bones come after the ids and before the caption, over the model and over the ids' backings.
        if (spec.Bones is { } bones)
        {
            var (lines, joints) = BoneOverlay.Plan(bones, _space, _frame!, spec.Size);
            foreach (var (from, to) in lines)
            {
                surface.Primitives.DrawLine(from, to, BoneOverlay.LineWidth, BoneOverlay.Colour);
            }

            foreach (var joint in joints)
            {
                surface.Primitives.DrawCircle(joint, BoneOverlay.JointRadius, BoneOverlay.Colour);
            }

            if (bones.Labels)
            {
                foreach (var label in LabelLayout.BoneLabels(bones.Names, bones.Heads, _space, _frame!, spec.Size, Measure))
                {
                    Draw(surface, label);
                }
            }
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

        foreach (var piece in label.BackingPieces ?? [label.Backing])
        {
            surface.Primitives.DrawRect(piece, LabelLayout.BackingColour);
        }
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

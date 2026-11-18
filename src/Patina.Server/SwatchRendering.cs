using System.Numerics;
using Carve.Hosting;
using Empyrean;
using Microsoft.Extensions.Logging;
using Patina.Graphs.Imaging;

namespace Patina.Server;

/// <summary>
/// What swatch asks of the frame thread: the outputs to dress the sphere in (RGBA, <paramref name="Size"/> a side, by
/// role), its turn about y, and the labels to draw into the atlas (null for the sphere view alone).
/// </summary>
public sealed record SwatchSpec(IReadOnlyDictionary<OutputRole, byte[]> Images, int Size, float Angle, IReadOnlyList<string>? Labels);

/// <summary>The sphere view and, when labels were asked, the label atlas and each label's measured extent; RGBA, sRGB.</summary>
public sealed record SwatchFrame(byte[] Sphere, byte[]? Atlas, IReadOnlyList<Vector2>? Extents);

public sealed class SwatchRequest(SwatchSpec spec) : OffscreenRequest<SwatchSpec, SwatchFrame>(spec);

/// <summary>
/// The sphere view's fixed camera: on +z looking at the sphere's centre, +y up, carve's 0.6 rad field of view, square.
/// The scenario's sphere check works its rays from these numbers.
/// </summary>
public static class SwatchCamera
{
    public static readonly Vector3 Eye = new(0, 0, 2);

    public const float FieldOfViewY = 0.6f;

    public const float Near = 0.1f;

    public const float Far = 10f;

    public static Matrix4x4 View => Matrix4x4.CreateLookAt(Eye, Vector3.Zero, Vector3.UnitY);

    public static Matrix4x4 Projection => Matrix4x4.CreatePerspectiveFieldOfView(FieldOfViewY, 1f, Near, Far);
}

/// <summary>
/// The test switch <c>PATINA_SWATCH_BREAK</c>, read once: each value breaks swatch in one way so the scenario's checks can
/// be shown failing (T-114's table of breaks). Unset, as everywhere but those runs, nothing changes.
/// </summary>
public static class SwatchBreak
{
    public static readonly string? Value = Environment.GetEnvironmentVariable("PATINA_SWATCH_BREAK") is { Length: > 0 } b ? b : null;

    /// <summary>The sheet's columns in reverse role order.</summary>
    public static bool RoleOrder => Value == "role-order";

    /// <summary>The sphere's u flipped (every texture mirrored left to right before upload).</summary>
    public static bool FlipU => Value == "u-flip";

    /// <summary>The tiled views shifted half a tile.</summary>
    public static bool TiledShift => Value == "tiled-shift";

    /// <summary>The roughness image bound as the sphere's base colour.</summary>
    public static bool RoughnessAsBase => Value == "roughness-as-base";
}

/// <summary>patina's offscreen pipeline: the sphere's 512 target and the 512 label atlas, both on Carve's slate.</summary>
internal sealed class SwatchComposition(IGraphicsResourceManager resources) : IRenderPipelineConfigurator
{
    public const string SphereTag = "swatch-sphere";

    public const string AtlasCameraTag = "swatch-atlas";

    public const string AtlasTag = "swatch-labels";

    private OffscreenTarget? _sphere;
    private OffscreenTarget? _atlas;

    public OffscreenTarget Sphere => _sphere ??= new OffscreenTarget(resources, SwatchImages.SphereCell);

    public OffscreenTarget Atlas => _atlas ??= new OffscreenTarget(resources, LabelAtlas.Size);

    public void Compose(RenderPipelineBuilder builder, RenderPipelineContext context)
    {
        Sphere.Declare(builder, context, "patina-sphere", SwatchImages.Background, SphereTag);
        // No camera carries the atlas's tag, so its scene stage draws nothing: black, then the labels' text in white.
        Atlas.Declare(builder, context, "patina-atlas", new Vector4(0, 0, 0, 1), AtlasCameraTag, AtlasTag);
    }
}

/// <summary>
/// The sphere scene, on the frame thread: the engine's UV sphere wearing a <see cref="Material.Pbr"/> made of the outputs,
/// lit by <see cref="RenderSky"/> alone, seen by <see cref="SwatchCamera"/>. One request a frame.
/// </summary>
internal sealed class SwatchSystem(
    ISceneManager scenes,
    IEntityManager entities,
    IGraphicsResourceManager resources,
    IOffscreenRenderer renderer,
    IRenderTargetReadback readback,
    IImmediateSurfaceRegistry surfaces,
    IAssetResidency residency,
    SwatchComposition composition,
    OffscreenQueue<SwatchRequest> queue,
    ILogger<SwatchSystem> log) : ISystem
{
    private const string Scene = "patina";

    private Entity? _sphere;
    private bool _composed;
    private Texture[] _textures = [];
    private IAssetHolder? _holder;

    public void Initialise()
    {
    }

    public void FixedUpdate()
    {
    }

    public void Update()
    {
        if (_sphere is null)
        {
            Build();
            return;
        }
        if (queue.TryTake(out var request) && request is not null)
        {
            Serve(request);
        }
    }

    private void Build()
    {
        scenes.CreateScene(Scene);
        var sky = scenes.CreateEntity(Scene);
        entities.AddComponent(sky, RenderSky.Sky);

        // The headless host does not apply a camera's projection, so it is set here by hand.
        var camera = scenes.CreateEntity(Scene);
        entities.AddCamera3D(camera, SwatchCamera.FieldOfViewY, SwatchCamera.Near, SwatchCamera.Far);
        entities.AddComponent(camera, new RenderTag(SwatchComposition.SphereTag));
        Matrix4x4.Invert(SwatchCamera.View, out var world);
        entities.GetOrAddComponent<WorldTransform>(camera) = new WorldTransform { Matrix = world };
        entities.GetComponent<Camera>(camera).ProjectionMatrix = SwatchCamera.Projection;

        var sphere = scenes.CreateEntity(Scene);
        entities.AddComponent(sphere, new MeshInstance
        {
            Mesh = resources.GetPrimitiveMesh(PrimitiveMesh.Sphere),
            Material = Material.Pbr(Vector4.One, 0, 1, null!, null!, null!, null!, null!, Vector3.Zero, 1, false),
            Mode = RenderMode.Mode3D,
            RenderLayerMask = RenderLayerMask.None,
            CastsShadow = false,
        });
        entities.AddComponent(sphere, new Transform3D { Position = Vector3.Zero, Orientation = Quaternion.Identity, Scale = Vector3.One });
        _sphere = sphere;
    }

    private void Serve(SwatchRequest request)
    {
        try
        {
            var spec = request.Spec;
            Dress(spec);
            var instance = _sphere!.Value;
            // The world matrix is written too: the transform system has run this tick already, and the frame drawn below
            // would otherwise show the last request's turn.
            var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, spec.Angle * MathF.PI / 180);
            entities.GetComponent<Transform3D>(instance).Orientation = turn;
            entities.GetOrAddComponent<WorldTransform>(instance) = new WorldTransform { Matrix = Matrix4x4.CreateFromQuaternion(turn) };

            Task<RenderTargetPixels>? atlas = null;
            List<Vector2>? extents = null;
            if (spec.Labels is { } labels)
            {
                // A tagged surface exists once the host has composed, which a headless host does on its first frame.
                if (!_composed)
                {
                    renderer.RenderFrame();
                    _composed = true;
                }
                var surface = surfaces.Get(SwatchComposition.AtlasTag);
                extents = [];
                for (var row = 0; row < labels.Count && row < LabelAtlas.Rows; row++)
                {
                    extents.Add(surface.Text.MeasureText(labels[row], 1));
                    surface.Text.DrawText(labels[row], new Vector2(LabelAtlas.Inset, row * LabelAtlas.Pitch + LabelAtlas.Inset), Vector4.One);
                }
                atlas = readback.ReadAsync(composition.Atlas.Resolved);
            }

            var sphere = OffscreenFrame.ReadAsync(renderer, readback, composition.Sphere.Resolved);
            _composed = true;
            Task all = atlas is null ? sphere : Task.WhenAll(sphere, atlas);
            _ = all.ContinueWith(
                task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        request.Complete(new SwatchFrame(sphere.Result.ToDecodedImage().Pixels, atlas?.Result.ToDecodedImage().Pixels, extents));
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
            log.LogError(e, "swatch: the sphere render failed");
            request.Fail(e);
        }
    }

    /// <summary>
    /// The sphere's material from this request's outputs: base colour sRGB, roughness as metallic-roughness, normal and
    /// occlusion linear, uploaded by <see cref="TextureUpload"/> (mips on). A missing role leaves the material's default
    /// (white, metallic 0, roughness 1, flat, unoccluded). The last request's material and textures are released first:
    /// nothing draws them once the frame that answered it is done.
    /// </summary>
    private void Dress(SwatchSpec spec)
    {
        _holder?.Dispose();
        foreach (var texture in _textures) resources.Destroy(texture);
        var made = new List<Texture>();

        Texture? Upload(OutputRole role, bool srgb)
        {
            var source = SwatchBreak.RoughnessAsBase && role == OutputRole.BaseColour ? OutputRole.Roughness : role;
            if (!spec.Images.TryGetValue(source, out var rgba)) return null;
            if (SwatchBreak.FlipU) rgba = SwatchImages.Mirrored(rgba, spec.Size);
            var texture = TextureUpload.Create(resources, rgba, spec.Size, spec.Size, srgb);
            made.Add(texture);
            return texture;
        }

        var material = Material.Pbr(Vector4.One, 0, 1,
            Upload(OutputRole.BaseColour, true)!, Upload(OutputRole.Roughness, false)!, Upload(OutputRole.Normal, false)!,
            null!, Upload(OutputRole.Occlusion, false)!, Vector3.Zero, 1, false);
        _textures = [.. made];
        _holder = residency.CreateHolder("patina swatch sphere");
        _holder.Hold(material);
        ref var instance = ref entities.GetComponent<MeshInstance>(_sphere!.Value);
        instance.Material = material;
        instance.RenderLayerMask = RenderLayerMask.All;
    }
}

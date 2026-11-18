using System.Numerics;
using System.Security.Cryptography;
using Carve.Core;
using Empyrean;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StbImageSharp;
using CarveMaterial = Carve.Core.Material;
using Material = Empyrean.Material;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>
/// The engine headless in this process, hosted as <c>Program.cs</c> hosts it (ruling 5): Carve's <see cref="RenderComposition"/>,
/// its lights and sky from <see cref="RenderLights"/>, a camera per size, and one mesh entity. Each shot is applied in one
/// update and rendered and read back in the next, as <c>RenderPump</c> does (the light's transform is published a loop
/// iteration later).
/// </summary>
internal static class InProcessEngine
{
    public sealed record Kit(IGraphicsResourceManager Resources, IModelLoader Models, ITextureLoader Textures, IAssetResidency Residency);

    /// <summary>One frame; <paramref name="Before"/>, when given, runs on the frame thread just before the shot is applied.</summary>
    public sealed record Shot(Mesh Mesh, Material[] Submeshes, Vector3 Half, RenderView View, Action? Before = null);

    public sealed record Frame(byte[] Raw, DecodedImage Image);

    private sealed record Plan(Func<Kit, IReadOnlyList<Shot>> Shots, List<Frame> Frames)
    {
        public Exception? Failure { get; set; }
    }

    public static IReadOnlyList<Frame> Run(Func<Kit, IReadOnlyList<Shot>> shots)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Empyrean:Time:FixedDeltaTime"] = "0.05",
            ["Empyrean:Time:PaceToFixedStep"] = "true",
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var services = builder.Services;
        services.AddEmpyreanCore();
        services.AddEmpyreanGraphicsHeadlessCore();
        services.AddEmpyreanConfiguration(builder.Configuration);
        services.AddSingleton<RenderComposition>();
        services.UseRenderPipeline<RenderComposition>();
        var plan = new Plan(shots, []);
        services.AddSingleton(plan);
        services.AddSystem<HostSystem>();
        using var host = builder.Build();
        var loop = host.Services.GetRequiredService<IGameLoop>();
        loop.Initialise();
        loop.Run();
        if (plan.Failure is { } failure) throw new InvalidOperationException("the in-process engine host failed", failure);
        return plan.Frames;
    }

    private sealed class HostSystem(
        ISceneManager scenes, IEntityManager entities, IGraphicsResourceManager resources, IOffscreenRenderer renderer,
        IRenderTargetReadback readback, IModelLoader models, ITextureLoader textures, IAssetResidency residency, RenderComposition composition,
        IGameLifecycle life, Plan plan) : ISystem
    {
        private const string Scene = "carve";
        private Entity _light;
        private readonly List<Entity> _cameras = [];
        private Entity? _mesh;
        private IReadOnlyList<Shot>? _shots;
        private int _next;
        private bool _armed;

        public void Initialise()
        {
        }

        public void FixedUpdate()
        {
        }

        public void Update()
        {
            try
            {
                if (_shots is null)
                {
                    scenes.CreateScene(Scene);
                    _light = RenderLights.Add(scenes, entities, Scene, ViewFraming.LightOrientation(RenderView.Front));
                    foreach (var size in RenderComposition.Sizes)
                    {
                        var camera = scenes.CreateEntity(Scene);
                        entities.AddCamera3D(camera, ViewFraming.FieldOfViewY, 0.1f, 100f);
                        entities.AddComponent(camera, new RenderTag(RenderComposition.CameraTag(size)));
                        _cameras.Add(camera);
                    }
                    _shots = plan.Shots(new Kit(resources, models, textures, residency));
                    return;
                }

                if (_next >= _shots.Count)
                {
                    life.RequestStop();
                    return;
                }

                if (!_armed)
                {
                    Apply(_shots[_next]);
                    _armed = true;
                    return;
                }

                var read = readback.ReadAsync(composition.Target(RenderSpec.DefaultSize));
                renderer.RenderFrame();
                var pixels = read.GetAwaiter().GetResult();
                plan.Frames.Add(new Frame(pixels.Data.ToArray(), pixels.ToDecodedImage()));
                _armed = false;
                _next++;
            }
            catch (Exception e)
            {
                plan.Failure = e;
                _next = int.MaxValue;
                _shots = [];
                life.RequestStop();
            }
        }

        private void Apply(Shot shot)
        {
            shot.Before?.Invoke();
            if (_mesh is { } entity)
            {
                ref var instance = ref entities.GetComponent<MeshInstance>(entity);
                instance.Mesh = shot.Mesh;
                instance.SubmeshMaterials = shot.Submeshes;
            }
            else
            {
                var made = scenes.CreateEntity(Scene);
                entities.AddComponent(made, new MeshInstance
                {
                    Mesh = shot.Mesh,
                    Material = Material.LitMesh(GlbExport.BaseColor),
                    SubmeshMaterials = shot.Submeshes,
                    Mode = RenderMode.Mode3D,
                    RenderLayerMask = RenderLayerMask.All,
                    CastsShadow = true,
                });
                entities.AddComponent(made, new Transform3D { Position = Vector3.Zero, Orientation = Quaternion.Identity, Scale = Vector3.One });
                _mesh = made;
            }

            var frame = ViewFraming.Frame(shot.Half, shot.View);
            foreach (var camera in _cameras)
            {
                entities.GetOrAddComponent<WorldTransform>(camera) = new WorldTransform { Matrix = frame.World };
                entities.GetComponent<Camera>(camera).ProjectionMatrix = frame.Projection;
            }
            entities.GetComponent<Transform3D>(_light).Orientation = frame.LightOrientation;
        }
    }
}

/// <summary>
/// T-78's equality test: a cube whose six faces carry six materials covering every field and texture slot, exported through
/// T-76's code and loaded by the engine's loader, draws byte-identically with the loader's materials and with
/// <see cref="PbrMaterials"/>'s, at <c>front</c> and <c>back</c>; and the host's frame is the real <c>render</c>'s.
/// </summary>
[Trait("Tier", "regression")]
public sealed class PbrEqualityTests() : FileToolTests("carve-pbr-equality")
{
    private static readonly string Frames =
        Directory.CreateDirectory(Path.Combine(Paths.RepoRoot, "artifacts", "material-tests")).FullName;

    private const string Cube =
        """{"vertices":[[0,0,0],[1,0,0],[1,1,0],[0,1,0],[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"faces":[[0,3,2,1],[4,5,6,7],[0,1,5,4],[3,7,6,2],[0,4,7,3],[1,2,6,5]]}""";

    private static readonly (string File, byte[] Png)[] Pngs =
    [
        ("marker.png", TestPng.Marker),
        ("mr.png", TestPng.Halves((0, 60, 255), (0, 230, 0))),
        ("normal.png", TestPng.Halves((128, 128, 255), (200, 128, 200), across: true)),
        ("emissive.png", TestPng.Halves((255, 255, 255), (0, 0, 0))),
        ("occlusion.png", TestPng.Halves((255, 255, 255), (64, 64, 64), across: true)),
    ];

    /// <summary>The six materials as <c>define_material</c> arguments, face i taking material i; face 5 is left unmapped.</summary>
    private static readonly string[] Definitions =
    [
        """{"name":"painted","base_colour":"#FFFFFF","roughness":0.8,"base_colour_texture":"marker.png"}""",
        """{"name":"steel","base_colour":"#C0C0C0","metallic":1,"roughness":0.35,"occlusion":0.4}""",
        """{"name":"patchy","base_colour":"#B08050","metallic":1,"roughness":1,"metallic_roughness_texture":"mr.png"}""",
        """{"name":"bumpy","base_colour":"#80A0C0","roughness":0.5,"normal_texture":"normal.png"}""",
        """{"name":"glow","base_colour":"#303030","emissive":"#FF8020","emissive_texture":"emissive.png"}""",
        """{"name":"aged","base_colour":"#A0C080","occlusion":0.5,"occlusion_texture":"occlusion.png","double_sided":true,"base_colour_texture":"marker.png"}""",
    ];

    private CarveMaterial[] Materials()
    {
        MaterialTexture T(string file) => MaterialTexture.Load(Path.Combine(Root, file), out _)!;
        return
        [
            new("painted", "#FFFFFF", Roughness: 0.8f, BaseColourTexture: T("marker.png")),
            new("steel", "#C0C0C0", 1f, 0.35f, Occlusion: 0.4f),
            new("patchy", "#B08050", 1f, 1f, MetallicRoughnessTexture: T("mr.png")),
            new("bumpy", "#80A0C0", Roughness: 0.5f, NormalTexture: T("normal.png")),
            new("glow", "#303030", Emissive: "#FF8020", EmissiveTexture: T("emissive.png")),
            new("aged", "#A0C080", Occlusion: 0.5f, DoubleSided: true, OcclusionTexture: T("occlusion.png"), BaseColourTexture: T("marker.png")),
        ];
    }

    private static readonly Vector2[] FaceUv = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

    private static PolygonMesh Mesh(CarveMaterial[] materials)
    {
        var mesh = PolygonMesh.Create(
            [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
            [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]]).Mesh!;
        var style = FaceStyle.None;
        for (var f = 0; f < 6; f++) style = style.WithMaterial([f], materials[f].Name);
        var uvs = MeshUvs.None.WithFaces(Enumerable.Range(0, 5).Select(f => KeyValuePair.Create(f, (IReadOnlyList<Vector2>?)FaceUv)));
        return mesh.WithStyle(style).WithUvs(uvs);
    }

    private static void SameMaterial(Material loaded, Material built, string what)
    {
        Assert.False(ReferenceEquals(loaded, built), what);
        Assert.Equal(loaded.Flags, built.Flags);
        Assert.Equal(loaded.Color, built.Color);
        Assert.Equal(loaded.Metallic, built.Metallic);
        Assert.Equal(loaded.Roughness, built.Roughness);
        Assert.Equal(loaded.EmissiveColor, built.EmissiveColor);
        Assert.Equal(loaded.Occlusion, built.Occlusion);
        Assert.Equal(loaded.DoubleSided, built.DoubleSided);
        Assert.Equal(loaded.AlphaCutoff, built.AlphaCutoff);
        foreach (var (slot, a, b) in new[]
        {
            ("base", loaded.Texture, built.Texture), ("normal", loaded.NormalTexture, built.NormalTexture),
            ("metallic-roughness", loaded.MetallicRoughnessTexture, built.MetallicRoughnessTexture),
            ("emissive", loaded.EmissiveTexture, built.EmissiveTexture), ("occlusion", loaded.OcclusionTexture, built.OcclusionTexture),
        })
        {
            Assert.True((a is null) == (b is null), $"{what} {slot}: loader {(a is null ? "none" : "a texture")}, function {(b is null ? "none" : "a texture")}");
            if (a is null) continue;
            Assert.False(ReferenceEquals(a, b), $"{what} {slot}: the same texture");
            Assert.Equal((a.Width, a.Height, a.Format, a.MipLevels), (b!.Width, b.Height, b.Format, b.MipLevels));
        }
    }

    [Fact]
    public async Task The_function_draws_byte_identically_to_the_loaders_materials_and_the_host_is_render()
    {
        foreach (var (file, png) in Pngs) File.WriteAllBytes(Path.Combine(Root, file), png);
        var materials = Materials();
        var mesh = Mesh(materials);
        var glb = GlbExport.Write(mesh, Shading.Triangles(mesh), "cube", materials);
        File.WriteAllBytes(Path.Combine(Frames, "equality-cube.glb"), glb);
        var report = MeshReport.Create(mesh);
        var space = DrawnSpace.Of(report.BoundsMin, report.BoundsMax);
        var half = space.Apply(report.BoundsMax);
        var data = MeshBridge.ToSubmeshedMeshData(mesh, space, out var colours, out var names);
        Assert.Equal(materials.Select(m => m.Name), names);
        Assert.All(colours, Assert.Null);

        Material[]? loaded = null, built = null;
        var frames = InProcessEngine.Run(kit =>
        {
            loaded = kit.Models.LoadModel(glb, ModelFormat.Gltf, generateMipmaps: true).SubmeshMaterials!;
            var pbr = new PbrMaterials(kit.Resources, kit.Textures);
            built = materials.Select(pbr.Of).ToArray();
            var gpu = kit.Resources.CreateMesh(data);
            return
            [
                new(gpu, loaded!, half, RenderView.Front), new(gpu, built!, half, RenderView.Front),
                new(gpu, loaded!, half, RenderView.Back), new(gpu, built!, half, RenderView.Back),
            ];
        });

        Assert.Equal(6, loaded!.Length);
        for (var i = 0; i < 6; i++) SameMaterial(loaded[i], built![i], materials[i].Name);
        // One PNG used by two materials is one texture of the function's.
        Assert.Same(built![0].Texture, built[5].Texture);

        var log = new List<string>();
        foreach (var (at, view) in new[] { (0, "front"), (2, "back") })
        {
            var (fromLoader, fromFunction) = (frames[at], frames[at + 1]);
            var differing = Enumerable.Range(0, fromLoader.Raw.Length).Count(i => fromLoader.Raw[i] != fromFunction.Raw[i]);
            log.Add($"{view}: loader {Hash(fromLoader.Raw)} function {Hash(fromFunction.Raw)} differing bytes {differing}");
            Keep(fromLoader.Image, $"equality-{view}-loader");
            Keep(fromFunction.Image, $"equality-{view}-function");
            Assert.True(differing == 0, $"{view}: {differing} bytes differ, the first at byte {FirstDiff(fromLoader.Raw, fromFunction.Raw)}");
            // A positive check: the cube is drawn, so the frames are not equal by both being empty.
            var drawn = Enumerable.Range(0, 512 * 512).Count(i => Pixel(fromFunction.Image, i) != Pixel(fromFunction.Image, 0));
            log.Add($"{view}: {drawn} pixels drawn");
            Assert.True(drawn > 20000, $"{view}: only {drawn} pixels drawn");
        }
        File.WriteAllLines(Path.Combine(Frames, "equality.txt"), log);

        // The harness is render (ruling 5): the same cube through the real carve, pixel for pixel after the same encode.
        using var carve = new CarveProcess([], workingDirectory: Root);
        await carve.InitializeAsync();
        Ok(await carve.CallToolAsync("define_mesh", Cube));
        Ok(await carve.CallToolAsync("set_uvs", """{"faces":[{"face":0,"uv":[[0,1],[1,1],[1,0],[0,0]]},{"face":1,"uv":[[0,1],[1,1],[1,0],[0,0]]},{"face":2,"uv":[[0,1],[1,1],[1,0],[0,0]]},{"face":3,"uv":[[0,1],[1,1],[1,0],[0,0]]},{"face":4,"uv":[[0,1],[1,1],[1,0],[0,0]]}]}"""));
        for (var i = 0; i < 6; i++)
        {
            Ok(await carve.CallToolAsync("define_material", Definitions[i]));
            Ok(await carve.CallToolAsync("set_material", $$"""{"faces":[{{i}}],"material":"{{materials[i].Name}}"}"""));
        }
        foreach (var (at, view) in new[] { (1, "front"), (3, "back") })
        {
            var result = await carve.CallToolAsync("render", $$"""{"view":"{{view}}"}""");
            if (IsError(result)) Assert.Fail(TextOf(result));
            var png = Convert.FromBase64String(result.GetProperty("content")[0].GetProperty("data").GetString()!);
            File.WriteAllBytes(Path.Combine(Frames, $"equality-{view}-render.png"), png);
            var rendered = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
            var host = frames[at].Image;
            Assert.Equal((host.Width, host.Height), (rendered.Width, rendered.Height));
            var first = FirstDiff(host.Pixels, rendered.Data);
            Assert.True(first < 0, $"{view}: the host's frame differs from render's at pixel {first / 4} (x {first / 4 % 512}, y {first / 4 / 512})");
        }
    }

    private static int FirstDiff(byte[] a, byte[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++) if (a[i] != b[i]) return i;
        return a.Length == b.Length ? -1 : Math.Min(a.Length, b.Length);
    }

    private static (int R, int G, int B) Pixel(DecodedImage image, int i) => (image.Pixels[i * 4], image.Pixels[i * 4 + 1], image.Pixels[i * 4 + 2]);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes))[..16];

    private static void Keep(DecodedImage image, string name)
    {
        using var file = File.Create(Path.Combine(Frames, name + ".png"));
        new StbImageWriteSharp.ImageWriter().WritePng(image.Pixels, image.Width, image.Height, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, file);
    }
}

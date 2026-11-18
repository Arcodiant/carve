using System.Numerics;
using Carve.Core;
using Empyrean;
using CarveMaterial = Carve.Core.Material;
using Material = Empyrean.Material;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>
/// <see cref="PbrMaterials"/>' caches (RV-47): a PNG used as colour and as data is two textures, one per colour space; and a
/// material no model uses is released, its holder disposed and then every texture no kept material samples destroyed, which
/// the engine's own counts show.
/// </summary>
[Trait("Tier", "regression")]
public sealed class PbrReleaseTests
{
    private static MaterialTexture Marker(string name) =>
        MaterialTexture.FromPng(Path.Combine(Path.GetTempPath(), name), TestPng.Marker, out var problem) ?? throw new InvalidOperationException(problem);

    /// <summary>The marker PNG as base colour (sRGB) and as occlusion map (linear), in one material.</summary>
    private static readonly CarveMaterial Both = new("both", BaseColourTexture: Marker("marker.png"), OcclusionTexture: Marker("marker.png"));

    private static readonly CarveMaterial Plain = new("plain", "#80A0C0", Roughness: 0.5f);

    [Fact]
    public void One_PNG_as_colour_and_as_data_is_two_textures_one_per_colour_space()
    {
        Material? built = null;
        var count = 0;
        InProcessEngine.Run(kit =>
        {
            var pbr = new PbrMaterials(kit.Resources, kit.Textures, kit.Residency);
            built = pbr.Of(Both);
            count = pbr.TextureCount;
            return [];
        });

        Assert.Equal(2, count);
        Assert.NotSame(built!.Texture, built.OcclusionTexture);
        Assert.Equal(TextureFormat.Rgba8Srgb, built.Texture!.Format);
        Assert.Equal(TextureFormat.Rgba8Unorm, built.OcclusionTexture!.Format);
    }

    [Fact]
    public void A_material_no_longer_used_is_released_with_its_textures_once_nothing_draws_it()
    {
        var mesh = PolygonMesh.Create(
            [new(0, 0, 0), new(1, 0, 0), new(1, 1, 0), new(0, 1, 0), new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
            [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [3, 7, 6, 2], [0, 4, 7, 3], [1, 2, 6, 5]]).Mesh!
            .WithStyle(FaceStyle.None.WithMaterial([0, 1, 2, 3, 4, 5], "m"));
        var report = MeshReport.Create(mesh);
        var space = DrawnSpace.Of(report.BoundsMin, report.BoundsMax);
        var half = space.Apply(report.BoundsMax);
        var data = MeshBridge.ToSubmeshedMeshData(mesh, space, out _, out _);

        var log = new List<(string When, int Textures, int Held, int Materials, int TableUsed)>();
        InProcessEngine.Run(kit =>
        {
            var pbr = new PbrMaterials(kit.Resources, kit.Textures, kit.Residency);
            void Note(string when) => log.Add((when, kit.Resources.Residency.TextureCount, pbr.TextureCount, pbr.MaterialCount,
                kit.Resources.Residency.MaterialTableUsed));
            var gpu = kit.Resources.CreateMesh(data);
            var textured = new[] { pbr.Of(Both) };
            var plain = new[] { pbr.Of(Plain) };
            Note("built");
            var shots = new List<InProcessEngine.Shot>
            {
                // Both drawn, so each takes its material-table entry; then the entity draws only the plain one.
                new(gpu, textured, half, RenderView.Front),
                new(gpu, plain, half, RenderView.Front),
                new(gpu, plain, half, RenderView.Front, () =>
                {
                    Note("before");
                    pbr.Keep([Plain]);
                    Note("kept");
                }),
            };
            // Frames enough for the released entry and textures to wait out the frames in flight.
            for (var i = 0; i < 6; i++) shots.Add(new(gpu, plain, half, RenderView.Front));
            shots.Add(new(gpu, plain, half, RenderView.Front, () => Note("after")));
            return shots;
        });

        var (built, before, kept, after) = (log[0], log[1], log[2], log[3]);
        Assert.Equal((2, 2), (built.Held, built.Materials));
        Assert.Equal((0, 1), (kept.Held, kept.Materials));
        // The engine destroyed the two textures, and reclaimed the textured material's table entry.
        Assert.Equal(before.Textures - 2, kept.Textures);
        Assert.Equal(before.Textures - 2, after.Textures);
        Assert.Equal(before.TableUsed - 1, after.TableUsed);
    }
}

using System.Numerics;
using Carve.Core.Baking;
using Xunit;

namespace Carve.Core.Tests;

[Trait("Tier", "unit")]
public class OcclusionTests
{
    private static readonly Vector3[] V = TestMeshes.CubeVertices;

    // The unit cube's -y face wound to face +y, mapped (x, z) over the whole square.
    private static readonly int[] Floor = [4, 5, 1, 0];

    // The unit cube's +x face wound to face -x, towards the floor.
    private static readonly int[] Wall = [5, 6, 2, 1];

    private static Vector2[] FloorUvs() => Floor.Select(i => new Vector2(V[i].X, V[i].Z)).ToArray();

    private static BakeLayout Lay(PolygonMesh mesh, int size)
    {
        var result = BakeLayout.Create(mesh, size);
        Assert.True(result.Succeeded, result.Error);
        return result.Layout!;
    }

    private static PolygonMesh FloorMesh(params int[][] more) =>
        TestMeshes.Make(V, [Floor, .. more]).WithUvs(MeshUvs.None.WithFace(0, FloorUvs()));

    private static OcclusionBake Occlude(PolygonMesh mesh, int size, int rays, float reach, params PolygonMesh[] others) =>
        Bake.Occlusion(Lay(mesh, size), new RayCaster([mesh, .. others]), 0, rays, reach);

    /// <summary>The mean of each column of covered texels, x from 0 to 1.</summary>
    private static double[] Columns(BakeImage image) =>
        Enumerable.Range(0, image.Width).Select(x => Enumerable.Range(0, image.Height).Average(y => (double)image[x, y])).ToArray();

    [Fact]
    public void The_tree_finds_the_same_closest_hit_as_every_triangle_tried()
    {
        // A deterministic scatter of 60 small tilted quads in the unit box, so the tree has depth.
        var vertices = new List<Vector3>();
        var faces = new List<int[]>();
        for (var k = 0; k < 60; k++)
        {
            float H(int i) => (Bake.Hash(k * 7 + i) & 0xFFFF) / 65535f;
            var c = new Vector3(H(0), H(1), H(2));
            var u = Vector3.Normalize(new Vector3(H(3) - 0.5f, H(4) - 0.5f, H(5) - 0.5f)) * 0.12f;
            var w = Vector3.Normalize(Vector3.Cross(u, new Vector3(0.3f, 1, 0.2f))) * 0.09f;
            var b = vertices.Count;
            vertices.AddRange([c - u - w, c + u - w, c + u + w, c - u + w]);
            faces.Add([b, b + 1, b + 2, b + 3]);
        }
        var mesh = TestMeshes.Make(vertices, faces);
        var caster = new RayCaster([mesh]);
        var triangles = Triangulator.Triangulate(mesh);
        Assert.Equal(triangles.Count, caster.Triangles);

        var hits = 0;
        for (var r = 0; r < 3000; r++)
        {
            float H(int i) => (Bake.Hash(100_000 + r * 5 + i) & 0xFFFF) / 65535f;
            var origin = new Vector3(H(0), H(1), H(2)) * 1.4f - new Vector3(0.2f);
            var direction = Vector3.Normalize(new Vector3(H(3) - 0.5f, H(4) - 0.5f, H(5) - 0.5f));
            const float reach = 0.8f;

            var best = reach;
            var bestTriangle = -1;
            for (var t = 0; t < triangles.Count; t++)
            {
                var (a, b, c) = (mesh.Vertices[triangles[t].A], mesh.Vertices[triangles[t].B], mesh.Vertices[triangles[t].C]);
                if (RayCaster.Intersect(origin, direction, a, b - a, c - a, out var d) && d < best) (best, bestTriangle) = (d, t);
            }

            var found = caster.Cast(origin, direction, reach, out var hit);
            Assert.Equal(bestTriangle >= 0, found);
            if (!found) continue;
            hits++;
            Assert.Equal(best, hit.Distance);
            Assert.Equal(bestTriangle, hit.Triangle);
            Assert.Equal(triangles[bestTriangle].Face, hit.Face);
        }
        Assert.InRange(hits, 300, 2700);
    }

    [Fact]
    public void Both_sides_of_a_triangle_block()
    {
        var caster = new RayCaster([FloorMesh()]);
        Assert.True(caster.Cast(new(0.5f, 1, 0.5f), -Vector3.UnitY, 2, out var down));
        Assert.True(caster.Cast(new(0.5f, -1, 0.5f), Vector3.UnitY, 2, out var up));
        Assert.Equal(1, down.Distance, 5);
        Assert.Equal(1, up.Distance, 5);
        Assert.False(caster.Cast(new(0.5f, -1, 0.5f), Vector3.UnitY, 0.99f, out _));
    }

    [Fact]
    public void The_directions_are_unit_and_above_the_horizon_and_their_mean_is_cosine_weighted()
    {
        foreach (var rays in new[] { 1, 32, Bake.MaxRays })
        {
            var set = Bake.OcclusionDirections(rays);
            Assert.All(set, d => Assert.Equal(1, d.Length(), 5));
            Assert.All(set, d => Assert.True(d.Z >= MathF.Sqrt(0.5f / rays) - 1e-6f));
        }
        // The mean height of a cosine-weighted hemisphere is 2/3.
        Assert.Equal(2.0 / 3, Bake.OcclusionDirections(1024).Average(d => d.Z), 3);
    }

    [Fact]
    public void A_texel_on_the_floor_inside_a_closed_box_reads_0()
    {
        var inward = TestMeshes.CubeFaces.Select(f => f.Reverse().ToArray()).ToArray();
        var floorFace = Array.FindIndex(inward, f => f.SequenceEqual(Floor));
        Assert.True(floorFace >= 0);
        var mesh = TestMeshes.Make(V, inward).WithUvs(MeshUvs.None.WithFace(floorFace, FloorUvs()));
        Assert.True(mesh.FaceNormal(floorFace).Y > 0.99f);
        var layout = Lay(mesh, 64);
        var bake = Bake.Occlusion(layout, new RayCaster([mesh]), 0, 32, 1000);
        foreach (var t in layout.Texels()) Assert.True(bake.Image[t.X, t.Y] < 0.5f / 255, $"({t.X}, {t.Y}) reads {bake.Image[t.X, t.Y]}");
        Assert.Equal(0, bake.SelfHits);
    }

    [Fact]
    public void A_lone_quad_reads_1_and_never_hits_itself()
    {
        var mesh = FloorMesh();
        Assert.True(mesh.FaceNormal(0).Y > 0.99f);
        var bake = Occlude(mesh, 64, 64, 10);
        Assert.All(Lay(mesh, 64).Texels(), t => Assert.Equal(1f, bake.Image[t.X, t.Y]));
        Assert.Equal(0, bake.SelfHits);
        Assert.Equal(64L * 64 * 64, bake.Rays);
    }

    [Fact]
    public void A_quad_beside_a_wall_reads_lower_near_the_wall_and_rises_with_distance()
    {
        var bake = Occlude(FloorMesh(Wall), 64, 256, 0.5f);
        var columns = Columns(bake.Image);

        // The wall is at x = 1, the right of the image. Beyond reach of it the floor is open.
        Assert.True(columns[^1] < 0.75, $"next to the wall: {columns[^1]}");
        Assert.Equal(1, columns[..30].Min(), 6);
        for (var x = 1; x < columns.Length; x++) Assert.True(columns[x] <= columns[x - 1] + 1e-9, $"column {x} ({columns[x]}) above column {x - 1} ({columns[x - 1]})");
        Assert.True(columns[^1] < columns[^8] && columns[^8] < columns[^16]);
    }

    [Fact]
    public void The_falloff_is_one_minus_t_over_reach_squared()
    {
        // One ray leaves at height cos 45 degrees (radius sqrt(0.5) on the disc), so it meets a ceiling 0.25 above at t = 0.25 / sqrt(0.5),
        // and occludes by 1 - (t / 0.5)^2 = 0.5.
        var ceiling = TestMeshes.Make([new(-5, 0.25f, -5), new(5, 0.25f, -5), new(5, 0.25f, 5), new(-5, 0.25f, 5)], [[0, 1, 2, 3]]);
        var bake = Occlude(FloorMesh(), 64, 1, 0.5f, ceiling);
        var t = Lay(FloorMesh(), 64).Texels().First();
        var f = (0.25 - bake.Offset) / Math.Sqrt(0.5) / 0.5; // the origin is lifted by the offset
        Assert.Equal(f * f, bake.Image[t.X, t.Y], 5);
        Assert.Equal(0.5, f * f, 2);
    }

    [Fact]
    public void Occluders_narrow_what_darkens()
    {
        var floor = FloorMesh();
        var wall = TestMeshes.Make(V, [Wall]);
        var layout = Lay(floor, 64);
        var both = Bake.Occlusion(layout, new RayCaster([floor, wall]), 0, 32, 0.5f);
        var alone = Bake.Occlusion(layout, new RayCaster([floor]), 0, 32, 0.5f);
        Assert.True(Columns(both.Image)[^1] < 0.8);
        Assert.All(layout.Texels(), t => Assert.Equal(1f, alone.Image[t.X, t.Y]));
    }

    [Fact]
    public void Two_bakes_are_identical()
    {
        var mesh = FloorMesh(Wall);
        var a = Occlude(mesh, 128, 32, 0.5f).Image.Values.ToArray();
        var b = Occlude(mesh, 128, 32, 0.5f).Image.Values.ToArray();
        Assert.Equal(a, b);
    }

    [Fact]
    public void The_orm_carries_occlusion_roughness_and_metallic_exactly()
    {
        var mesh = FloorMesh();
        var layout = Lay(mesh, 64);
        var occlusion = layout.NewImage(1);
        foreach (var t in layout.Texels()) occlusion[t.X, t.Y] = (t.X + t.Y) / 200f;
        var orm = Bake.Orm(layout, occlusion, [0.36f], [1f]);
        Assert.Equal(3, orm.Channels);
        foreach (var t in layout.Texels())
        {
            Assert.Equal(occlusion[t.X, t.Y], orm[t.X, t.Y, 0]);
            Assert.Equal(0.36f, orm[t.X, t.Y, 1]);
            Assert.Equal(1f, orm[t.X, t.Y, 2]);
        }
        Assert.Throws<ArgumentException>(() => Bake.Orm(layout, layout.NewImage(3), [0.36f], [1f]));
    }

    [Fact]
    public void Rays_and_reach_out_of_range_are_refused()
    {
        var mesh = FloorMesh();
        var layout = Lay(mesh, 64);
        var caster = new RayCaster([mesh]);
        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.Occlusion(layout, caster, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.Occlusion(layout, caster, 0, Bake.MaxRays + 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.Occlusion(layout, caster, 0, 32, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Bake.Occlusion(layout, caster, 0, 32, float.NaN));
    }

    [Fact]
    public void The_offset_scales_with_the_model()
    {
        var small = FloorMesh();
        var big = TestMeshes.Make(V.Select(v => v * 100), [Floor]).WithUvs(MeshUvs.None.WithFace(0, FloorUvs()));
        var a = Bake.OcclusionOffset(Lay(small, 64), new RayCaster([small]));
        var b = Bake.OcclusionOffset(Lay(big, 64), new RayCaster([big]));
        // The mesh holds all eight cube corners, so its box is the unit cube's.
        Assert.Equal(Bake.OffsetScale * MathF.Sqrt(3), a, 9);
        Assert.Equal(100 * a, b, 4);
    }

    [Fact]
    public void No_ray_of_the_wrench_meets_its_own_face()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "samples", "wrench.carve"))) dir = dir.Parent!;
        var wrench = CarveFile.Read(File.ReadAllText(Path.Combine(dir.FullName, "samples", "wrench.carve"))).Mesh!;
        var bake = Bake.Occlusion(Lay(wrench, 256), new RayCaster([wrench]), 0, 8, 0.035f);
        Assert.Equal(0, bake.SelfHits);
        Assert.InRange(bake.Offset, 1e-6f, 1e-5f);
    }
}

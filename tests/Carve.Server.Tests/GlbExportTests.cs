using System.Numerics;
using System.Text;
using System.Text.Json;
using Carve.Core;
using Carve.Core.Tests;
using Empyrean;
using Microsoft.Extensions.DependencyInjection;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server.Tests;

/// <summary>
/// The .glb read back by Empyrean's own glTF parser, with no process and no GPU: the triangles, normals and winding are
/// FlatShading's bit for bit, in order, and the material is the one matte 'carve'.
/// </summary>
[Trait("Tier", "unit")]
public class GlbExportTests
{
    /// <summary>The engine's CPU model parser, built without a GPU or a host.</summary>
    internal static readonly IModelGeometryLoader Parser = BuildParser();

    private static IModelGeometryLoader BuildParser()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmpyreanCore();
        services.AddEmpyreanGeometry();
        return services.BuildServiceProvider().GetRequiredService<IModelGeometryLoader>();
    }

    public static TheoryData<string> Meshes => ["cube", "lblock", "crate"];

    private static PolygonMesh Mesh(string name) => name switch
    {
        "cube" => TestMeshes.Cube(),
        "lblock" => TestMeshes.LBlock(),
        "crate" => TestMeshes.Crate(),
        _ => throw new ArgumentException(name),
    };

    private static byte[] Glb(PolygonMesh mesh, string name) => GlbExport.Write(Shading.Triangles(mesh), name);

    private static string Bits(Vector3 v) =>
        $"{BitConverter.SingleToInt32Bits(v.X):X8} {BitConverter.SingleToInt32Bits(v.Y):X8} {BitConverter.SingleToInt32Bits(v.Z):X8} ({v.X:R}, {v.Y:R}, {v.Z:R})";

    [Theory]
    [MemberData(nameof(Meshes))]
    public void The_engine_parser_reads_back_FlatShadings_triangles_bit_for_bit_in_order(string name)
    {
        var mesh = Mesh(name);
        var expected = Shading.Triangles(mesh);
        var parsed = Parser.Parse(Glb(mesh, name), ModelFormat.Gltf);
        var vertices = parsed.MeshData.Vertices;
        var indices = parsed.MeshData.Indices!;

        Assert.Empty(parsed.Warnings ?? []);
        Assert.Equal(expected.Count * 3, indices.Length);

        // SharpGLTF keeps the order triangles were added in, so this compares in order. The parser makes each
        // triangle clockwise from outside, the engine's winding: Carve's (A, B, C) reads back as (A, C, B), the
        // order MeshBridge draws.
        for (var t = 0; t < expected.Count; t++)
        {
            var e = expected[t];
            Vector3[] corners = [e.A, e.C, e.B];
            for (var k = 0; k < 3; k++)
            {
                var v = vertices[(int)indices[t * 3 + k]];
                Assert.True(Bits(corners[k]) == Bits(v.Position), $"triangle {t} corner {k}: position {Bits(v.Position)}, expected {Bits(corners[k])}");
                Assert.True(Bits(e.Normal) == Bits(v.Normal), $"triangle {t} corner {k}: normal {Bits(v.Normal)}, expected {Bits(e.Normal)}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Meshes))]
    public void One_matte_material_carve(string name)
    {
        var parsed = Parser.Parse(Glb(Mesh(name), name), ModelFormat.Gltf);

        var material = Assert.Single(parsed.SubmeshMaterials)!;
        Assert.Equal(new Vector4(0.72f, 0.72f, 0.70f, 1f), material.BaseColor);
        Assert.True(material.Pbr);
        Assert.Equal(0f, material.Metallic);
        Assert.Equal(1f, material.Roughness);
        Assert.False(material.DoubleSided);
    }

    [Theory]
    [MemberData(nameof(Meshes))]
    public void Two_exports_of_one_mesh_are_byte_identical(string name)
    {
        var mesh = Mesh(name);
        Assert.Equal(Glb(mesh, name), Glb(Mesh(name), name));
    }

    /// <summary>The glTF JSON chunk of a .glb.</summary>
    private static JsonElement Json(byte[] glb)
    {
        Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
        var length = BitConverter.ToInt32(glb, 12);
        Assert.Equal("JSON", Encoding.ASCII.GetString(glb, 16, 4));
        return JsonDocument.Parse(glb.AsMemory(20, length)).RootElement;
    }

    [Fact]
    public void One_scene_node_and_mesh_named_for_the_file_with_one_indexed_primitive_of_position_and_normal()
    {
        var json = Json(Glb(TestMeshes.Crate(), "crate"));

        Assert.Equal(1, json.GetProperty("scenes").GetArrayLength());
        var node = Assert.Single(json.GetProperty("nodes").EnumerateArray());
        Assert.Equal("crate", node.GetProperty("name").GetString());
        foreach (var transform in new[] { "matrix", "translation", "rotation", "scale" })
            Assert.False(node.TryGetProperty(transform, out _), $"node has {transform}");

        var mesh = Assert.Single(json.GetProperty("meshes").EnumerateArray());
        Assert.Equal("crate", mesh.GetProperty("name").GetString());
        var primitive = Assert.Single(mesh.GetProperty("primitives").EnumerateArray());
        Assert.Equal(["NORMAL", "POSITION"], primitive.GetProperty("attributes").EnumerateObject().Select(a => a.Name).Order().ToArray());
        Assert.True(primitive.TryGetProperty("indices", out _));
        Assert.Equal(4, primitive.TryGetProperty("mode", out var mode) ? mode.GetInt32() : 4);

        var material = Assert.Single(json.GetProperty("materials").EnumerateArray());
        Assert.Equal("carve", material.GetProperty("name").GetString());
    }
}

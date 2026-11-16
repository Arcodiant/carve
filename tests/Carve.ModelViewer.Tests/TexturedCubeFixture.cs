using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Memory;
using SharpGLTF.Scenes;

namespace Carve.ModelViewer.Tests;

/// <summary>
/// The fixture cube again, in one metallic material that sets every texture slot, each of its own size and of a
/// flat colour, written with SharpGLTF straight (the shape of what T-76's export writes, with no call to it).
/// </summary>
internal static class TexturedCubeFixture
{
    public const float Metallic = 1f;
    public const float Roughness = 0.35f;
    public const float Occlusion = 0.8f;
    public static readonly Vector4 BaseColor = new(0.8f, 0.8f, 0.8f, 1f);
    public static readonly Vector3 Emissive = new(0.02f, 0.01f, 0f);

    /// <summary>Each slot's size, in the order the report lists them.</summary>
    public static readonly (string Slot, int Width, int Height)[] Sizes =
    [
        ("baseColour", 4, 4), ("metallicRoughness", 2, 2), ("normal", 8, 8), ("emissive", 4, 2), ("occlusion", 2, 4),
    ];

    private static readonly (Vector3 Normal, Vector3[] Corners)[] Faces =
    [
        (Vector3.UnitX, [new(1, 0, 1), new(1, 0, 0), new(1, 1, 0), new(1, 1, 1)]),
        (-Vector3.UnitX, [new(0, 0, 0), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0)]),
        (Vector3.UnitY, [new(0, 1, 1), new(1, 1, 1), new(1, 1, 0), new(0, 1, 0)]),
        (-Vector3.UnitY, [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)]),
        (Vector3.UnitZ, [new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)]),
        (-Vector3.UnitZ, [new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0)]),
    ];

    private static readonly Vector2[] Uvs = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

    public static byte[] Write()
    {
        var material = new MaterialBuilder("metal")
            .WithMetallicRoughnessShader()
            .WithBaseColor(BaseColor)
            .WithMetallicRoughness(Metallic, Roughness)
            .WithEmissive(Emissive)
            .WithDoubleSide(true);
        Attach(material, KnownChannel.BaseColor, Sizes[0], (200, 200, 200));
        Attach(material, KnownChannel.MetallicRoughness, Sizes[1], (0, 255, 255));
        Attach(material, KnownChannel.Normal, Sizes[2], (128, 128, 255));
        Attach(material, KnownChannel.Emissive, Sizes[3], (255, 255, 255));
        Attach(material, KnownChannel.Occlusion, Sizes[4], (255, 255, 255));
        material.GetChannel(KnownChannel.Occlusion).Parameters[KnownProperty.OcclusionStrength] = Occlusion;

        var mesh = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>("cube");
        var primitive = mesh.UsePrimitive(material);
        foreach (var (normal, c) in Faces)
        {
            VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty> Corner(int i) =>
                new(new VertexPositionNormal(c[i], normal), new VertexTexture1(Uvs[i]));
            primitive.AddQuadrangle(Corner(0), Corner(1), Corner(2), Corner(3));
        }

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, Matrix4x4.Identity).WithName("cube");
        return scene.ToGltf2().WriteGLB().ToArray();
    }

    private static void Attach(MaterialBuilder material, KnownChannel channel, (string Slot, int Width, int Height) size, (byte R, byte G, byte B) colour) =>
        material.UseChannel(channel).UseTexture().WithPrimaryImage(ImageBuilder.From(new MemoryImage(Png(size.Width, size.Height, colour)), size.Slot));

    /// <summary>A flat-colour opaque PNG, 8-bit RGB.</summary>
    private static byte[] Png(int width, int height, (byte R, byte G, byte B) colour)
    {
        var raw = new byte[height * (1 + 3 * width)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = y * (1 + 3 * width) + 1 + 3 * x;
                (raw[at], raw[at + 1], raw[at + 2]) = colour;
            }
        }

        using var data = new MemoryStream();
        using (var z = new ZLibStream(data, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 2;
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", data.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream to, string type, byte[] body)
    {
        var typed = Encoding.ASCII.GetBytes(type).Concat(body).ToArray();
        var number = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, body.Length);
        to.Write(number);
        to.Write(typed);
        var crc = 0xFFFFFFFFu;
        foreach (var b in typed)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        to.Write(number);
    }
}

using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Carve.Core.Tests;

/// <summary>
/// T-91's identity guard: every <see cref="TestMeshes"/> model and every sample's model gives the same
/// <see cref="Model.Combined"/>, by content, as before the part tree. Each expected digest was taken from the code as it was
/// before T-91 (commit a494e1f) by <see cref="Content"/>, which reads everything a combined mesh carries through the public
/// API: positions and tombstones to the bit, face loops and holes, the shading mode and hard edges, every drawn triangle with
/// its corner and face normals, which faces show corner normals, colours, groups, materials, UVs, seams and morph targets.
/// </summary>
[Trait("Tier", "unit")]
public class CombinedIdentityGuardTests
{
    /// <summary>The digest of each model's combined mesh, from the code before T-91.</summary>
    private static readonly Dictionary<string, string> Before = new()
    {
        ["testmeshes/cube"] = "AE482C609721462DC0BF0AC010874866FF5C7E5474D3FB6FD0BBDB6F829B6218",
        ["testmeshes/barrel"] = "D9A7AF010B26205D12E92FCEB913352928E609578A42CA743F05FC72B4F13C10",
        ["testmeshes/seamed-barrel"] = "F7518A8FFD48B5CE2BF1E7059AD5B0C5423B71F26D22C4753949F0923FBE419B",
        ["testmeshes/tube"] = "A17E35449485A85485F94B62A50F02B4EECFFE85CA65CFB53C7D53586B2363DD",
        ["testmeshes/grid"] = "99CC4CAC2184892011DBFBA58787DC8EB41C9BB84063FFF1E607838487367ECC",
        ["testmeshes/bent-strip"] = "EE91E14FAA347DBEF0DFC10EA1D68A27AF8E43D88C8D1D248822ECEB8BCC0FE8",
        ["testmeshes/leaky-shed"] = "66B469512562B41A3A631F29A36658C82665FB3F4B831782E78FC8D6C9215749",
        ["testmeshes/crate"] = "15A2F595BD124D806662B4FFB3D2A8BAE5386C7AB720EF20907DABDE349C8AE0",
        ["testmeshes/l-block"] = "1E8080F623362FEBD15C9D125C50370349451FA5F2473219E76DDAA583B11F81",
        ["testmeshes/smooth-cube"] = "F2D9D0647F420D9926A99D3A6D445406E69F3849ECF57A3B36A8C6ED039E141E",
        ["testmeshes/all"] = "71877965EB2A2CE6ECF6FD60586432BA8562C23FE5898351E7A8C057CD2262F9",
        ["samples/leaky-shed"] = "66B469512562B41A3A631F29A36658C82665FB3F4B831782E78FC8D6C9215749",
        ["samples/gardener"] = "656FC69CC88C670CDAF68F3FED2CCD177859F49BA828193AF09252DD5D19DDC4",
        ["samples/gardener-soft"] = "0962B283559FDA3240067A30A895A500FB418FBD072F1AB8432E2B198CDAF68F",
        ["samples/gatehouse"] = "BF150E6F3AA104562140BF3E32F53DB370C00CEE6EE3CB074E78BA62EE6ABDD3",
        ["samples/barrel"] = "4CD21243AF8B49BB54B2F977DF0800132D9FA2EA8A17E194813572567BA21FBC",
        ["samples/barrel-textured"] = "8D669F75D102E9B52FCB9877FBDA0B40098645E67F4F70BC99931EF9093ED52A",
        ["samples/arm"] = "C9F166525C59451641B776981E15710EDA90B4219876969B89EA5A783AABC96A",
        ["samples/arm-lean"] = "3763F842A1CA7D1E18D35A44D181CE414BBCDFE2A5522632EB14AC8CF087F362",
    };

    private static string RepoFile(string path)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Carve.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, path);
    }

    private static PolygonMesh Smooth(PolygonMesh mesh) => mesh.WithShade(MeshShade.Of(ShadingMode.Smooth, []));

    /// <summary>Every model the guard covers, by a name.</summary>
    public static IEnumerable<(string Name, Model Model)> Models()
    {
        var meshes = new (string, PolygonMesh)[]
        {
            ("cube", TestMeshes.Cube()), ("barrel", TestMeshes.Barrel()), ("seamed-barrel", TestMeshes.SeamedBarrel()),
            ("tube", TestMeshes.Tube()), ("grid", TestMeshes.Grid(3, 2)), ("bent-strip", TestMeshes.BentStrip()),
            ("leaky-shed", TestMeshes.LeakyShed()), ("crate", TestMeshes.Crate()), ("l-block", TestMeshes.LBlock()),
            ("smooth-cube", Smooth(TestMeshes.Cube())),
        };
        foreach (var (name, mesh) in meshes) yield return ($"testmeshes/{name}", Model.Of(name, mesh));
        // Every mesh in one model: the joining path, ids offset mesh by mesh, a smooth piece beside flat ones.
        yield return ("testmeshes/all", meshes.Aggregate(Model.Empty, (m, x) => m.With(x.Item1, x.Item2)));
        foreach (var sample in new[] { "leaky-shed", "gardener", "gardener-soft", "gatehouse", "barrel", "barrel-textured", "arm", "arm-lean" })
        {
            var file = RepoFile($"samples/{sample}.carve");
            var read = CarveFile.Read(File.ReadAllText(file), Path.GetDirectoryName(file));
            Assert.True(read.Succeeded, $"{sample}: {string.Join("; ", read.Errors)}");
            yield return ($"samples/{sample}", read.Model!);
        }
    }

    [Fact]
    public void Every_existing_model_combines_as_before()
    {
        var actual = Models().ToDictionary(m => m.Name, m => Digest(m.Model.Combined!));
        Assert.Equal(Before.Keys.Order(), actual.Keys.Order());
        foreach (var (name, digest) in actual) Assert.True(Before[name] == digest, $"{name}: combined differs from before T-91");
    }

    /// <summary>The SHA-256 of <see cref="Content"/>.</summary>
    public static string Digest(PolygonMesh mesh) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Content(mesh))));

    private static string Bits(float f) => BitConverter.SingleToInt32Bits(f).ToString("x8", CultureInfo.InvariantCulture);
    private static string Bits(Vector3 v) => $"{Bits(v.X)},{Bits(v.Y)},{Bits(v.Z)}";
    private static string Bits(Vector2 v) => $"{Bits(v.X)},{Bits(v.Y)}";

    /// <summary>Everything a combined mesh carries, as text, read through the public API.</summary>
    public static string Content(PolygonMesh mesh)
    {
        var s = new StringBuilder();
        for (var v = 0; v < mesh.Vertices.Count; v++) s.Append("v ").Append(Bits(mesh.Vertices[v])).Append(mesh.IsVertexDeleted(v) ? " x" : "").Append('\n');
        for (var f = 0; f < mesh.Faces.Count; f++)
        {
            var face = mesh.Faces[f];
            s.Append("f ").Append(face.IsDeleted ? "x" : string.Join(",", face.Loop));
            foreach (var hole in face.Holes) s.Append(" h ").Append(string.Join(",", hole));
            s.Append(Shading.FaceHasCornerNormals(mesh, f) ? " c" : "").Append('\n');
        }
        s.Append("shade ").Append(mesh.ShadingMode).Append(' ').Append(string.Join(";", mesh.HardEdges)).Append('\n');
        foreach (var t in Shading.Triangles(mesh))
            s.Append("t ").Append(t.Face).Append(' ').Append(Bits(t.A)).Append(' ').Append(Bits(t.B)).Append(' ').Append(Bits(t.C))
                .Append(" n ").Append(Bits(t.NormalA)).Append(' ').Append(Bits(t.NormalB)).Append(' ').Append(Bits(t.NormalC))
                .Append(' ').Append(Bits(t.Normal)).Append('\n');
        if (mesh.Style is { } style)
        {
            foreach (var (f, c) in style.Colours.OrderBy(p => p.Key)) s.Append("colour ").Append(f).Append(' ').Append(c).Append('\n');
            foreach (var (f, m) in style.Materials.OrderBy(p => p.Key)) s.Append("material ").Append(f).Append(' ').Append(m).Append('\n');
            foreach (var (g, faces) in style.Groups) s.Append("group ").Append(g).Append(' ').Append(string.Join(",", faces)).Append('\n');
        }
        if (mesh.Uvs is { } uvs)
        {
            foreach (var f in uvs.MappedFaces) s.Append("uv ").Append(f).Append(' ').Append(string.Join(" ", uvs.Of(f)!.Select(Bits))).Append('\n');
            s.Append("seams ").Append(string.Join(";", uvs.Seams)).Append('\n');
        }
        foreach (var target in mesh.Targets?.All ?? [])
            s.Append("target ").Append(target.Name).Append(' ')
                .Append(string.Join(" ", target.Deltas.OrderBy(p => p.Key).Select(p => $"{p.Key}:{Bits(p.Value)}"))).Append('\n');
        s.Append("weights ").Append(mesh.Weights is null ? "none" : string.Join(";", mesh.Weights.Vertices.Select(v => $"{v}:" + string.Join(",", mesh.Weights.Of(v))))).Append('\n');
        return s.ToString();
    }
}

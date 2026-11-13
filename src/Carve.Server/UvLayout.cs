using System.Globalization;
using System.Numerics;
using Carve.Core;
using Empyrean;
using PolygonMesh = Carve.Core.PolygonMesh;

namespace Carve.Server;

/// <summary>
/// The square of UV space the <c>uvs</c> look shows (T-58): centre (<paramref name="CentreU"/>, <paramref name="CentreV"/>)
/// and side <paramref name="Side"/>, u right and v down on the image. Pure, so the rule is tested with no device.
/// </summary>
public readonly record struct UvWindow(double CentreU, double CentreV, double Side)
{
    /// <summary>The window side over the bounds' larger side: the margin is a sixteenth of the window on each side.</summary>
    public const double Growth = 8.0 / 7.0;

    /// <summary>The unit square alone.</summary>
    public static UvWindow Unit { get; } = new(0.5, 0.5, Growth);

    /// <summary>
    /// The unit square grown to take in every corner UV (holes too) of <paramref name="faces"/> that is mapped, kept square
    /// about the bounds' centre, then widened by <see cref="Growth"/>.
    /// </summary>
    public static UvWindow Of(PolygonMesh mesh, IEnumerable<int> faces)
    {
        double u0 = 0, v0 = 0, u1 = 1, v1 = 1;
        foreach (var face in faces)
        {
            if (mesh.Uvs?.Of(face) is not { } corners) continue;
            foreach (var uv in corners)
            {
                u0 = Math.Min(u0, uv.X);
                v0 = Math.Min(v0, uv.Y);
                u1 = Math.Max(u1, uv.X);
                v1 = Math.Max(v1, uv.Y);
            }
        }

        return new UvWindow((u0 + u1) / 2, (v0 + v1) / 2, Math.Max(u1 - u0, v1 - v0) * Growth);
    }

    /// <summary>Where a UV lands on a frame <paramref name="size"/> pixels square, in pixels from the top-left.</summary>
    public Vector2 Pixel(Vector2 uv, int size) =>
        new((float)(((uv.X - CentreU) / Side + 0.5) * size), (float)(((uv.Y - CentreV) / Side + 0.5) * size));

    /// <summary>Where a UV is drawn: x = u - cu, y = -(v - cv), at depth <paramref name="z"/>; the camera looks down -Z with +Y up.</summary>
    public Vector3 World(Vector2 uv, float z) => new((float)(uv.X - CentreU), (float)-(uv.Y - CentreV), z);
}

/// <summary>The <c>uvs</c> look ready for the engine: the ground and faces in one textured mesh with its vertex colours, the white lines and the seams.</summary>
public sealed record UvScene(
    MeshData<PositionUvNormalVertex> Fill,
    Rgba8[] Colours,
    MeshData<PositionVertex> Edges,
    MeshData<PositionVertex>? Seams,
    UvWindow Window);

/// <summary>
/// Pure CPU: the <c>uvs</c> look of one mesh (T-58), its faces <paramref name="faces"/> of the drawn mesh. The checker is
/// tiled dimmed over the window (the ground), each mapped face is the checker at full brightness at its own UVs, and its
/// edges are white with the marked seams yellow over them. Nothing here touches a device.
/// </summary>
public static class UvLayout
{
    /// <summary>Depths: ground, faces, white lines, seams; the camera is at <see cref="Eye"/>.</summary>
    public const float GroundZ = 0, FaceZ = 0.01f, EdgeZ = 0.02f, SeamZ = 0.03f, Eye = 5;

    /// <summary>
    /// The ground's vertex colour, linear: 40% of the displayed value, i.e. the sRGB decode of 0.4 (0.1329), to a byte (34).
    /// The texture is sampled in linear light and the target encoded to sRGB, so a dimmed texel shows at about 40%.
    /// </summary>
    public static readonly Rgba8 GroundVertex = Rgba8.FromVector(new Vector3(SrgbToLinear(0.4f)));

    /// <summary>The white lines and the unit square's border, linear.</summary>
    public static readonly Vector4 Edge = Vector4.One;

    /// <summary>A marked seam: #FFD800, linear (1, 0.6867, 0).</summary>
    public static readonly Vector4 Seam = new(1, SrgbToLinear(0xD8 / 255f), 0, 1);

    private static readonly Rgba8 OrangeVertex = Rgba8.FromVector(new Vector3(LookMeshes.Orange.X, LookMeshes.Orange.Y, LookMeshes.Orange.Z));

    public static float SrgbToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>The faces of <paramref name="faces"/> the mesh has, live and mapped, ascending.</summary>
    public static IReadOnlyList<int> Mapped(PolygonMesh mesh, ElementWindow faces) =>
        Enumerable.Range(faces.FirstFace, Math.Max(0, Math.Min(faces.FaceCount, mesh.Faces.Count - faces.FirstFace)))
            .Where(f => f >= 0 && !mesh.IsFaceDeleted(f) && mesh.Uvs?.IsMapped(f) == true).ToList();

    public static UvWindow Window(PolygonMesh mesh, ElementWindow faces) => UvWindow.Of(mesh, Mapped(mesh, faces));

    public static UvScene Compose(PolygonMesh mesh, ElementWindow faces, Highlight highlight)
    {
        var mapped = Mapped(mesh, faces);
        var window = UvWindow.Of(mesh, mapped);
        var vertices = new List<PositionUvNormalVertex>();
        var colours = new List<Rgba8>();

        // The ground: one quad over the whole window, its UVs the window's corners, so the sampler's repeat tiles it.
        var (h, cu, cv) = (window.Side / 2, window.CentreU, window.CentreV);
        Vector2[] ground = [new((float)(cu - h), (float)(cv - h)), new((float)(cu + h), (float)(cv - h)), new((float)(cu + h), (float)(cv + h)), new((float)(cu - h), (float)(cv + h))];
        Add(window, vertices, colours, ground[0], ground[1], ground[2], GroundVertex, GroundZ, null);
        Add(window, vertices, colours, ground[0], ground[2], ground[3], GroundVertex, GroundZ, null);

        foreach (var face in mapped)
        {
            var corners = mesh.Uvs!.Of(face)!;
            var loop = mesh.Faces[face];
            // A highlighted face is solid orange: every corner samples the white cell.
            var picked = highlight.Faces.Contains(face);
            foreach (var t in Triangulator.TriangulateFace(mesh, face))
            {
                Add(window, vertices, colours, corners[loop.CornerOf(t.A)], corners[loop.CornerOf(t.B)], corners[loop.CornerOf(t.C)],
                    picked ? OrangeVertex : Rgba8.White, FaceZ, picked ? CheckerTexture.WhiteCentre : null);
            }
        }

        var indices = Enumerable.Range(0, vertices.Count).Select(i => (uint)i).ToArray();
        var fill = new MeshData<PositionUvNormalVertex>(vertices.ToArray(), indices);
        var (edges, seams) = Lines(mesh, mapped);
        var white = edges.Select(e => (e.A, e.B)).Concat(Border).SelectMany(e => new[] { window.World(e.A, EdgeZ), window.World(e.B, EdgeZ) });
        var yellow = seams.SelectMany(e => new[] { window.World(e.A, SeamZ), window.World(e.B, SeamZ) });
        return new UvScene(fill, colours.ToArray(), LineList(white)!.Value, LineList(yellow), window);
    }

    /// <summary>The unit square's four sides.</summary>
    public static readonly (Vector2 A, Vector2 B)[] Border =
        [(new(0, 0), new(1, 0)), (new(1, 0), new(1, 1)), (new(1, 1), new(0, 1)), (new(0, 1), new(0, 0))];

    /// <summary>
    /// Every edge of each mapped face at that face's corner UVs, outer loop and holes, so an edge two faces share is drawn
    /// once per side; and those whose vertex pair is a marked seam, likewise on each side.
    /// </summary>
    public static (List<(Vector2 A, Vector2 B)> Edges, List<(Vector2 A, Vector2 B)> Seams) Lines(PolygonMesh mesh, IEnumerable<int> mapped)
    {
        var edges = new List<(Vector2, Vector2)>();
        var seams = new List<(Vector2, Vector2)>();
        foreach (var face in mapped)
        {
            var corners = mesh.Uvs!.Of(face)!;
            var shape = mesh.Faces[face];
            for (var hole = -1; hole < shape.Holes.Count; hole++)
            {
                var loop = shape.LoopAt(hole);
                for (var i = 0; i < loop.Count; i++)
                {
                    var j = (i + 1) % loop.Count;
                    var segment = (corners[shape.CornerAt(hole, i)], corners[shape.CornerAt(hole, j)]);
                    edges.Add(segment);
                    if (mesh.Uvs.IsSeam(loop[i], loop[j])) seams.Add(segment);
                }
            }
        }

        return (edges, seams);
    }

    /// <summary>
    /// Where a face's id is written: the area centroid of its outer UV loop, or the mean of its corners where that loop has
    /// no area.
    /// </summary>
    public static Vector2 LabelPoint(IReadOnlyList<Vector2> loop)
    {
        double area = 0, x = 0, y = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            Vector2 a = loop[i], b = loop[(i + 1) % loop.Count];
            var cross = (double)a.X * b.Y - (double)b.X * a.Y;
            area += cross;
            x += (a.X + (double)b.X) * cross;
            y += (a.Y + (double)b.Y) * cross;
        }

        if (Math.Abs(area) < 1e-12)
        {
            var mean = loop.Aggregate(Vector2.Zero, (s, p) => s + p) / loop.Count;
            return mean;
        }

        return new Vector2((float)(x / (3 * area)), (float)(y / (3 * area)));
    }

    /// <summary>Each mapped face's id, relative to the first face of the range, at <see cref="LabelPoint"/> of its outer loop.</summary>
    public static IEnumerable<(string Text, Vector2 Uv)> FaceLabels(PolygonMesh mesh, ElementWindow faces) =>
        Mapped(mesh, faces).Select(f =>
        {
            var corners = mesh.Uvs!.Of(f)!;
            var shape = mesh.Faces[f];
            var outer = Enumerable.Range(0, shape.Loop.Count).Select(i => corners[shape.CornerAt(-1, i)]).ToList();
            return ((f - faces.FirstFace).ToString(CultureInfo.InvariantCulture), LabelPoint(outer));
        });

    /// <summary>
    /// One triangle, wound toward the camera whatever its UV winding, so a flipped face is drawn; it is reversed for the
    /// engine as <see cref="MeshBridge"/> reverses: clockwise in drawn x, y seen from +Z.
    /// </summary>
    private static void Add(UvWindow window, List<PositionUvNormalVertex> vertices, List<Rgba8> colours, Vector2 a, Vector2 b, Vector2 c, Rgba8 colour, float z, Vector2? texel)
    {
        var (pa, pb, pc) = (window.World(a, z), window.World(b, z), window.World(c, z));
        var cross = ((double)pb.X - pa.X) * ((double)pc.Y - pa.Y) - ((double)pb.Y - pa.Y) * ((double)pc.X - pa.X);
        if (cross == 0) return;
        if (cross > 0) (pb, pc, b, c) = (pc, pb, c, b);
        foreach (var (p, uv) in new[] { (pa, a), (pb, b), (pc, c) })
        {
            vertices.Add(new PositionUvNormalVertex { Position = p, Normal = Vector3.UnitZ, TexCoord = texel ?? uv });
            colours.Add(colour);
        }
    }

    private static MeshData<PositionVertex>? LineList(IEnumerable<Vector3> ends)
    {
        var vertices = ends.Select(p => new PositionVertex { Position = p }).ToArray();
        if (vertices.Length == 0) return null;
        var indices = Enumerable.Range(0, vertices.Length).Select(i => (uint)i).ToArray();
        return new MeshData<PositionVertex>(vertices, indices) { Topology = Empyrean.MeshTopology.LineList };
    }
}

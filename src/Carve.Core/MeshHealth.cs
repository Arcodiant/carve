using System.Numerics;

namespace Carve.Core;

/// <summary>Something <see cref="MeshHealth"/> found wrong that <see cref="MeshReport"/> does not warn of. <see cref="Element"/> names it, e.g. <c>shells[1]</c>, <c>vertices[10]</c> or <c>faces[8]</c>.</summary>
public sealed record HealthProblem(string Check, string Element, string Message);

/// <summary>
/// A connected component of faces joined by shared edges. <paramref name="Faces"/> ascend.
/// <paramref name="Closed"/>: every edge of the shell has exactly two faces. <paramref name="ConsistentWinding"/>: no edge is
/// crossed twice the same way. <paramref name="SignedVolume"/> is null unless both hold; it is inside-out only below -1e-6 x the shell's bounding-box diagonal cubed (see <see cref="MeshHealth"/>), so a flat sheet's rounding-noise volume of either sign is not.
/// </summary>
public sealed record MeshShell(IReadOnlyList<int> Faces, bool Closed, bool ConsistentWinding, double? SignedVolume);

/// <summary>
/// What a mesh measures. Bounds and size are <see cref="MeshReport"/>'s, of the vertices the faces use.
/// <paramref name="Volume"/> is the sum of the shells' signed volumes, null unless every shell is closed and consistently wound.
/// </summary>
public sealed record HealthStats(
    int Vertices, int Faces, int Edges, int Triangles, int Shells, bool Closed,
    Vector3 BoundsMin, Vector3 BoundsMax, Vector3 Size, double Area, double? Volume, UvStats? Uvs = null);

/// <summary>How many live faces are mapped (have UVs) and how many are not; <see cref="HealthStats.Uvs"/> is null for a mesh with no UVs.</summary>
public sealed record UvStats(int Mapped, int Unmapped);

/// <summary>A mesh's health: what is wrong with it by element id, what it measures, and what a game will make of it.</summary>
public sealed class MeshHealth
{
    /// <summary>A shell's face ids are written out up to this many; past it the rest are a count.</summary>
    private const int ListedFaces = 5;

    private MeshHealth(IReadOnlyList<MeshShell> shells, IReadOnlyList<HealthProblem> problems, IReadOnlyList<MeshWarning> warnings, IReadOnlyList<string> notes, HealthStats stats)
    {
        Shells = shells;
        Problems = problems;
        Warnings = warnings;
        Notes = notes;
        Stats = stats;
    }

    /// <summary>Ordered by lowest face id; <c>shells[k]</c> in a problem is a position here.</summary>
    public IReadOnlyList<MeshShell> Shells { get; }

    /// <summary><c>inside-out</c>, then <c>coincident-vertices</c>, then <c>duplicate-face</c>.</summary>
    public IReadOnlyList<HealthProblem> Problems { get; }

    /// <summary><see cref="MeshReport"/>'s warnings, as they are, then a mapped mesh's UV warnings: unmapped, flipped and zero-area faces by id, then each overlapping pair.</summary>
    public IReadOnlyList<MeshWarning> Warnings { get; }

    /// <summary>Informational, never a problem: size, base, origin and shell count.</summary>
    public IReadOnlyList<string> Notes { get; }

    public HealthStats Stats { get; }

    /// <summary>No problems and no warnings.</summary>
    public bool Ok => Problems.Count == 0 && Warnings.Count == 0;

    public static MeshHealth Create(PolygonMesh mesh) => Create(mesh, []);

    /// <summary>
    /// As <see cref="Create(PolygonMesh)"/>, with the model's <paramref name="materials"/> beside the mesh: a face whose material
    /// has a texture and which has no UVs is warned of by the material's name (replacing the generic unmapped warning, in a mesh
    /// with no UVs too), and a material the mesh uses with an occlusion strength but no occlusion map is warned of (T-75).
    /// </summary>
    public static MeshHealth Create(PolygonMesh mesh, IReadOnlyList<Material> materials)
    {
        var report = MeshReport.Create(mesh);
        var triangles = Triangulator.Triangulate(mesh);
        var shells = FindShells(mesh, triangles);

        var problems = new List<HealthProblem>();
        for (var k = 0; k < shells.Count; k++)
        {
            if (IsInsideOut(mesh, shells[k]))
                problems.Add(new HealthProblem("inside-out", $"shells[{k}]", MeshEdits.Invariant(
                    $"closed shell of {shells[k].Faces.Count} faces ({DescribeFaces(shells[k].Faces)}) is inside-out: its faces are wound clockwise seen from outside, so a game culls them and sees nothing")));
        }
        AddCoincidentVertices(mesh, problems);
        AddDuplicateFaces(mesh, problems);

        var size = report.BoundsMax - report.BoundsMin;
        var stats = new HealthStats(
            report.VertexCount, report.FaceCount, report.EdgeCount, triangles.Count, shells.Count, report.Closed,
            report.BoundsMin, report.BoundsMax, size,
            mesh.LiveFaces.Sum(mesh.FaceArea),
            shells.All(s => s.SignedVolume is not null) ? shells.Sum(s => s.SignedVolume!.Value) : null);

        var texturedFaces = new Dictionary<int, string>();
        var materialWarnings = new List<MeshWarning>();
        foreach (var name in Model.MaterialsUsedBy(mesh))
        {
            if (materials.FirstOrDefault(m => m.Name == name) is not { } material) continue;
            if (material.HasTexture)
                foreach (var (face, used) in mesh.Style!.Materials.Where(p => p.Value == name))
                    texturedFaces[face] = used;
            if (material.OcclusionHasNoEffect)
                materialWarnings.Add(new(MeshWarningKind.MaterialNoEffect, $"materials['{name}']",
                    MeshEdits.Invariant($"occlusion is {material.Occlusion:R} but the material has no occlusion_texture, so it has no effect (glTF scales only the map)")));
        }
        var (uvWarnings, uvStats) = MeshUvHealth.Create(mesh, texturedFaces.Count == 0 ? null : texturedFaces);
        uvWarnings.AddRange(materialWarnings);
        if (uvStats is not null) stats = stats with { Uvs = uvStats };
        IReadOnlyList<MeshWarning> warnings = uvWarnings.Count == 0 ? report.Warnings : [.. report.Warnings, .. uvWarnings];

        return new MeshHealth(shells, problems, warnings, BuildNotes(report, size, shells.Count), stats);
    }

    private static List<MeshShell> FindShells(PolygonMesh mesh, IReadOnlyList<MeshTriangle> triangles)
    {
        var parent = Enumerable.Range(0, mesh.Faces.Count).ToArray();
        int Find(int f)
        {
            while (parent[f] != f) f = parent[f] = parent[parent[f]];
            return f;
        }

        var edges = mesh.Topology.Edges;
        foreach (var edge in edges)
        {
            for (var u = 1; u < edge.Faces.Count; u++)
            {
                // The lower root wins, so a component's root is its lowest face.
                var a = Find(edge.Faces[0].Face);
                var b = Find(edge.Faces[u].Face);
                parent[Math.Max(a, b)] = Math.Min(a, b);
            }
        }

        var open = new HashSet<int>();
        var mixed = new HashSet<int>();
        foreach (var edge in edges)
        {
            var root = Find(edge.Faces[0].Face);
            if (edge.Faces.Count != 2) open.Add(root);
            var forward = edge.Faces.Count(u => u.LoToHi);
            if (forward >= 2 || edge.Faces.Count - forward >= 2) mixed.Add(root);
        }

        var byFace = triangles.ToLookup(t => t.Face);
        return mesh.LiveFaces
            .GroupBy(Find)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var faces = g.ToArray();
                var closed = !open.Contains(g.Key);
                var consistent = !mixed.Contains(g.Key);
                return new MeshShell(faces, closed, consistent, closed && consistent ? SignedVolume(mesh, faces, byFace) : null);
            })
            .ToList();
    }

    /// <summary>
    /// Inside-out only past rounding noise: the signed volume below <see cref="InsideOutFraction"/> times the cube of the
    /// diagonal of the shell's bounds, so a flat two-sided sheet, whose volume is a tiny number of either sign, is not.
    /// </summary>
    private static bool IsInsideOut(PolygonMesh mesh, MeshShell shell)
    {
        if (shell.SignedVolume is not { } volume || volume >= 0) return false;
        var used = shell.Faces.SelectMany(f => mesh.Faces[f].Loop).Select(i => mesh.Vertices[i]).ToList();
        var min = used.Aggregate(Vector3.Min);
        var max = used.Aggregate(Vector3.Max);
        var diagonal = Math.Sqrt(Math.Pow((double)max.X - min.X, 2) + Math.Pow((double)max.Y - min.Y, 2) + Math.Pow((double)max.Z - min.Z, 2));
        return volume < -InsideOutFraction * diagonal * diagonal * diagonal;
    }

    /// <summary>The person's ruling of 2026-11-07 (T-23): a shell is inside-out below -1e-6 x (its bounds' diagonal)^3.</summary>
    private const double InsideOutFraction = 1e-6;

    /// <summary>The sum over a shell's triangles of <c>dot(a, cross(b, c)) / 6</c>, in double, relative to the shell's first vertex.</summary>
    private static double SignedVolume(PolygonMesh mesh, IReadOnlyList<int> faces, ILookup<int, MeshTriangle> byFace)
    {
        var verts = mesh.Vertices;
        var o = verts[mesh.Faces[faces[0]].Loop[0]];
        (double X, double Y, double Z) From(int i) => ((double)verts[i].X - o.X, (double)verts[i].Y - o.Y, (double)verts[i].Z - o.Z);

        var sum = 0.0;
        foreach (var face in faces)
        {
            foreach (var t in byFace[face])
            {
                var (a, b, c) = (From(t.A), From(t.B), From(t.C));
                sum += a.X * (b.Y * c.Z - b.Z * c.Y) + a.Y * (b.Z * c.X - b.X * c.Z) + a.Z * (b.X * c.Y - b.Y * c.X);
            }
        }
        return sum / 6;
    }

    /// <summary><c>faces 30 to 35</c> when contiguous, else the ids, the first five and a count of the rest when there are more.</summary>
    private static string DescribeFaces(IReadOnlyList<int> faces)
    {
        if (faces[^1] - faces[0] == faces.Count - 1) return MeshEdits.Invariant($"faces {faces[0]} to {faces[^1]}");
        var listed = string.Join(", ", faces.Take(ListedFaces).Select(f => MeshEdits.Invariant($"{f}")));
        return faces.Count > ListedFaces
            ? MeshEdits.Invariant($"faces {listed} and {faces.Count - ListedFaces} more")
            : $"faces {listed}";
    }

    /// <summary>
    /// Exact: the three floats equal as floats, with no tolerance, so -0 is 0 as it is for <c>select</c> (the person's ruling of
    /// 2026-11-07, T-23). A later duplicate names the first vertex at that position.
    /// </summary>
    private static void AddCoincidentVertices(PolygonMesh mesh, List<HealthProblem> problems)
    {
        var first = new Dictionary<(int, int, int), int>();
        // Adding +0 turns -0 into 0 and changes no other float, so equal floats have equal bits.
        static int Bits(float f) => BitConverter.SingleToInt32Bits(f + 0f);
        foreach (var j in mesh.LiveVertices)
        {
            var v = mesh.Vertices[j];
            var key = (Bits(v.X), Bits(v.Y), Bits(v.Z));
            if (first.TryGetValue(key, out var i))
                problems.Add(new HealthProblem("coincident-vertices", $"vertices[{j}]", $"vertex is at the same position as vertices[{i}]"));
            else
                first[key] = j;
        }
    }

    /// <summary>The same set of vertex ids in any order or rotation. A face never repeats a vertex, so the sorted ids are the set.</summary>
    private static void AddDuplicateFaces(PolygonMesh mesh, List<HealthProblem> problems)
    {
        var first = new Dictionary<string, int>();
        foreach (var j in mesh.LiveFaces)
        {
            var key = string.Join(',', mesh.Faces[j].Corners.Order());
            if (first.TryGetValue(key, out var i))
                problems.Add(new HealthProblem("duplicate-face", $"faces[{j}]", $"face has the same vertices as faces[{i}]"));
            else
                first[key] = j;
        }
    }

    private static List<string> BuildNotes(MeshReport report, Vector3 size, int shells)
    {
        var min = report.BoundsMin;
        var max = report.BoundsMax;
        var notes = new List<string>
        {
            MeshEdits.Invariant($"size {size.X:G} x {size.Y:G} x {size.Z:G} m"),
            min.Y == 0 ? "base at y = 0" : MeshEdits.Invariant($"base at y = {min.Y:G}; a prop usually stands on y = 0"),
            // On the bounds counts as inside: a prop standing on y = 0 has its origin on them.
            min.X <= 0 && max.X >= 0 && min.Y <= 0 && max.Y >= 0 && min.Z <= 0 && max.Z >= 0 ? "origin inside the bounds" : "origin outside the bounds",
        };
        if (shells > 1) notes.Add(MeshEdits.Invariant($"{shells} separate shells; a model is usually one"));
        return notes;
    }
}

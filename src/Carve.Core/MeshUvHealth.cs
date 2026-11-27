using System.Numerics;

namespace Carve.Core;

/// <summary>What <see cref="MeshHealth"/> says of a mesh's UVs (T-60). Only a mesh with some mapped face is looked at.</summary>
internal static class MeshUvHealth
{
    /// <summary>Two faces overlap in UV space when their triangles' common area is more than this (about a fifteenth of a texel of the 256 checker). The planner's, T-60.</summary>
    internal const double OverlapTolerance = 1e-6;

    private static MeshWarning TexturedUnmapped(int face, string material) =>
        new(MeshWarningKind.UvUnmapped, $"faces[{face}]", $"material '{material}' has a texture but the face has no UVs, so it samples the texture's corner");

    private readonly record struct Tri(double Ax, double Ay, double Bx, double By, double Cx, double Cy);

    /// <summary>Unmapped, flipped and zero-area faces by face id, then each overlapping pair once, the lower id first, except a pair whose faces both have a material and the materials differ.</summary>
    internal static (List<MeshWarning> Warnings, UvStats? Stats) Create(PolygonMesh mesh, IReadOnlyDictionary<int, string>? textured = null)
    {
        var uvs = mesh.Uvs;
        if (uvs is null || uvs.MappedFaces.Count == 0)
            return (textured is null ? [] : mesh.LiveFaces.Where(textured.ContainsKey).Select(f => TexturedUnmapped(f, textured[f])).ToList(), null);

        var warnings = new List<MeshWarning>();
        var live = mesh.LiveFaces.ToArray();
        var mapped = 0;
        var boxes = new Dictionary<int, (double MinU, double MinV, double MaxU, double MaxV)>();
        var triangles = new Dictionary<int, List<Tri>>();
        foreach (var f in live)
        {
            if (uvs.Of(f) is not { } uv)
            {
                // A face whose material has a texture says so, and that one warning replaces the generic one (T-75).
                warnings.Add(textured is not null && textured.TryGetValue(f, out var material)
                    ? TexturedUnmapped(f, material)
                    : new(MeshWarningKind.UvUnmapped, $"faces[{f}]", "no UVs, so the face samples the texture's corner"));
                continue;
            }
            mapped++;
            var box = (MinU: uv.Min(p => (double)p.X), MinV: uv.Min(p => (double)p.Y), MaxU: uv.Max(p => (double)p.X), MaxV: uv.Max(p => (double)p.Y));
            var (du, dv) = (box.MaxU - box.MinU, box.MaxV - box.MinV);
            var area = UvArea.Signed(mesh.Faces[f], uv);
            // As for faces in space: the area is more than the tolerance times the bounds' diagonal squared, else it is none.
            if (!(Math.Abs(area) > PolygonMesh.ZeroAreaTolerance * (du * du + dv * dv)))
            {
                warnings.Add(new(MeshWarningKind.UvZeroArea, $"faces[{f}]", "UVs of zero area, so the face is one smeared texel"));
                continue; // a smear has no sense of winding and no area to overlap
            }
            if (area > 0)
                warnings.Add(new(MeshWarningKind.UvFlipped, $"faces[{f}]", "UVs flipped, so the texture is mirrored on the face"));
            boxes[f] = box;
            triangles[f] = TrianglesOf(mesh, f, uv);
        }

        var ids = boxes.Keys.Order().ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            var a = boxes[ids[i]];
            for (var j = i + 1; j < ids.Length; j++)
            {
                var b = boxes[ids[j]];
                // Two faces with different materials are not warned of (T-80's ruling 5), even when the materials share a PNG
                // and so show the same texels; the same material, or a face with no material, still warns.
                if (mesh.Style?.MaterialOf(ids[i]) is { } ma && mesh.Style.MaterialOf(ids[j]) is { } mb && ma != mb) continue;
                if (a.MaxU <= b.MinU || b.MaxU <= a.MinU || a.MaxV <= b.MinV || b.MaxV <= a.MinV) continue;
                var common = 0.0;
                foreach (var s in triangles[ids[i]])
                    foreach (var t in triangles[ids[j]])
                        common += Intersection(s, t);
                if (common > OverlapTolerance)
                    warnings.Add(new(MeshWarningKind.UvOverlap, $"faces[{ids[i]}]",
                        $"UVs overlap faces[{ids[j]}], so the two faces show the same texels; mirrored halves sharing a texture area are deliberate and still warned of",
                        Other: $"faces[{ids[j]}]"));
            }
        }
        return (warnings, new UvStats(mapped, live.Length - mapped));
    }

    /// <summary>The face's triangles in UV space, split as the face is in space (holes bridged); empty if it cannot be.</summary>
    private static List<Tri> TrianglesOf(PolygonMesh mesh, int f, IReadOnlyList<Vector2> uv)
    {
        var face = mesh.Faces[f];
        // The face renumbered by corner, on its corners' positions, so a triangle comes back as corner indices.
        var positions = face.Corners.Select(i => mesh.Vertices[i]).ToArray();
        var index = 0;
        var loop = face.Loop.Select(_ => index++).ToArray();
        var holes = face.Holes.Select(h => h.Select(_ => index++).ToArray()).ToArray();
        var renumbered = new Face(loop, holes);
        IReadOnlyList<int> outline = renumbered.HasHoles ? Triangulator.Bridge(positions, renumbered)?.Ids ?? [] : loop;
        if (outline.Count == 0 || !Triangulator.TryTriangulate(positions, outline, out var tris)) return [];

        var result = new List<Tri>();
        foreach (var (a, b, c) in tris)
        {
            var (p, q, r) = (uv[a], uv[b], uv[c]);
            var cross = ((double)q.X - p.X) * ((double)r.Y - p.Y) - ((double)q.Y - p.Y) * ((double)r.X - p.X);
            if (cross == 0) continue;
            // Counter-clockwise in (u, v) whatever the face's winding, so the clip's inside test is one way.
            result.Add(cross > 0 ? new(p.X, p.Y, q.X, q.Y, r.X, r.Y) : new(p.X, p.Y, r.X, r.Y, q.X, q.Y));
        }
        return result;
    }

    /// <summary>The area two counter-clockwise triangles share: one clipped against the other's three edges (Sutherland-Hodgman), in double.</summary>
    private static double Intersection(Tri s, Tri t)
    {
        var poly = new List<(double X, double Y)> { (s.Ax, s.Ay), (s.Bx, s.By), (s.Cx, s.Cy) };
        var clip = new (double X, double Y)[] { (t.Ax, t.Ay), (t.Bx, t.By), (t.Cx, t.Cy) };
        for (var e = 0; e < 3 && poly.Count > 0; e++)
        {
            var (a, b) = (clip[e], clip[(e + 1) % 3]);
            double Side((double X, double Y) p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            var next = new List<(double X, double Y)>();
            for (var i = 0; i < poly.Count; i++)
            {
                var (p, q) = (poly[i], poly[(i + 1) % poly.Count]);
                var (sp, sq) = (Side(p), Side(q));
                if (sp >= 0) next.Add(p);
                if ((sp > 0 && sq < 0) || (sp < 0 && sq > 0))
                {
                    var k = sp / (sp - sq);
                    next.Add((p.X + (q.X - p.X) * k, p.Y + (q.Y - p.Y) * k));
                }
            }
            poly = next;
        }
        var sum = 0.0;
        for (var i = 0; i < poly.Count; i++)
        {
            var (p, q) = (poly[i], poly[(i + 1) % poly.Count]);
            sum += p.X * q.Y - q.X * p.Y;
        }
        return Math.Abs(sum) / 2;
    }
}

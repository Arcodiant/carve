using System.Numerics;
using Carve.Scenario.Cuts;
using Carve.Scenario.Shed;

namespace Carve.Scenario.Gatehouse;

/// <summary>One pixel the scenario asks about: a point on a face, and whether that face is narrow on screen.</summary>
internal readonly record struct Probe(int Face, string Name, Vector3 At, bool Narrow);

/// <summary>
/// What the gatehouse scenario looks at, worked apart from the product so that the Core test (GatehouseGeometry) and the
/// scenario share it: the three directions, the box a stage is framed on, and the probes of a stage's mesh. A probe is the
/// middle of a face and a point near each of its corners, so the corners are asked about as well as the middles; whether
/// the ray test is sure of its pixel, and which face it shows, is <see cref="Solid.Seen"/> or, for a face only a few pixels
/// wide, <see cref="Solid.SeenNarrow"/> (T-67's rule).
/// </summary>
internal static class GatehousePlan
{
    public const int Size = 512;

    /// <summary>The directions (from the model toward the eye) of the planned frames.</summary>
    public static readonly Vector3 FromFront = new(0.6f, 0.35f, 1);
    public static readonly Vector3 FromBack = new(-0.6f, 0.35f, -1);
    public static readonly Vector3 Straight = new(0, 0, 1);

    /// <summary>
    /// What the ray test is sure of for each stage (1 to 7) and view (front, back, straight): the number of sure probes and the
    /// faces they show. Worked in Core by <c>GatehouseGeometry</c> from the agreed geometry, and held by the scenario's frames.
    /// </summary>
    public static readonly IReadOnlyDictionary<(int Stage, string View), (int Sure, string Faces)> Expected = new Dictionary<(int, string), (int, string)>
    {
        [(1, "front")] = (25, "1 5"), [(1, "back")] = (25, "0 4"), [(1, "straight")] = (10, "1"),
        [(2, "front")] = (70, "1 5 8 9 12 13 14"), [(2, "back")] = (79, "0 4 6 7 10 11 16"), [(2, "straight")] = (30, "1 8 9"),
        [(3, "front")] = (42, "1 5 8 9 14"), [(3, "back")] = (51, "0 4 16"), [(3, "straight")] = (20, "1 8 9"),
        [(4, "front")] = (52, "1 5 8 9 14 26"), [(4, "back")] = (60, "0 4 16 27"), [(4, "straight")] = (22, "1 8 9"),
        [(5, "front")] = (57, "1 5 8 9 14 26 28"), [(5, "back")] = (69, "0 4 16 27"), [(5, "straight")] = (29, "1 8 9 28"),
        [(6, "front")] = (68, "1 5 8 9 14 26 28 29"), [(6, "back")] = (81, "0 4 16 27 30"), [(6, "straight")] = (41, "1 8 9 28 29"),
        [(7, "front")] = (78, "1 5 8 9 14 26 28 31 35"), [(7, "back")] = (90, "0 4 16 27 34 35"), [(7, "straight")] = (29, "1 8 9 28"),
    };

    /// <summary>A face whose shortest edge is under this is narrow on screen (the cornice's ends, the window's reveal).</summary>
    private const float NarrowEdge = 0.35f;

    /// <summary>How far a corner probe is moved toward the face's middle.</summary>
    private const float Inset = 0.3f;

    /// <summary>The box <c>render</c> frames: the vertices the live faces use.</summary>
    public static (Vector3 Min, Vector3 Max) Bounds(Solid solid)
    {
        var used = Enumerable.Range(0, solid.Faces.Length).Where(f => !solid.IsDeleted(f))
            .SelectMany(f => solid.Faces[f].Loop).Distinct().Select(v => solid.Vertices[v]).ToArray();
        return (used.Aggregate(Vector3.Min), used.Aggregate(Vector3.Max));
    }

    /// <summary>Points in the window's hole, in the front's plane (z = 0.15): the six places a straight look through it shows the background.</summary>
    public static IEnumerable<Probe> WindowBackground() =>
        new[] { (0.7f, 1.15f), (0.45f, 0.95f), (0.95f, 0.95f), (0.95f, 1.35f), (0.45f, 1.35f), (0.7f, 1.55f) }
            .Select(p => new Probe(-1, $"window ({p.Item1}, {p.Item2})", new Vector3(p.Item1, p.Item2, 0.15f), false));

    /// <summary>
    /// The edges a stage added: those of its mesh whose middle is on no edge of the stage before. An edge cut short or in two
    /// (a ring cut through a vertical edge, a plane cut across a face) lies along an old edge and is not new.
    /// </summary>
    public static (int Lo, int Hi)[] NewEdges(Solid before, Solid after)
    {
        var old = before.Edges();
        return after.Edges().Where(e =>
        {
            var middle = (after.Vertices[e.Lo] + after.Vertices[e.Hi]) / 2;
            return !old.Any(o => Solid.SegmentDistance(middle, before.Vertices[o.Lo], before.Vertices[o.Hi]) < 1e-4f);
        }).ToArray();
    }

    /// <summary>The nudge of the in-sight rule, as a fraction of the mesh's bounds diagonal.</summary>
    private const float Nudge = 1e-3f;

    /// <summary>
    /// The edges in sight (the person's ruling of 2026-11-12): an edge is in sight when a ray from the eye to the edge's
    /// middle, nudged into one of its two faces, first hits that face. The nudge is 1e-3 of the mesh's bounds diagonal, from
    /// the middle toward that face's centroid, in the face's plane. Computed from the geometry, not from a pixel; for an
    /// orthographic camera the ray runs along the view direction.
    /// </summary>
    public static (int Lo, int Hi)[] InSight(Solid solid, Camera camera, Vector3 toEye, bool orthographic = false)
    {
        var (min, max) = Bounds(solid);
        var nudge = Nudge * (max - min).Length();
        var faces = new Dictionary<(int, int), List<int>>();
        for (var f = 0; f < solid.Faces.Length; f++)
        {
            if (solid.IsDeleted(f))
            {
                continue;
            }

            foreach (var l in solid.Faces[f].Holes.Prepend(solid.Faces[f].Loop))
            {
                for (var k = 0; k < l.Length; k++)
                {
                    var (a, b) = (l[k], l[(k + 1) % l.Length]);
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    if (!faces.TryGetValue(key, out var list))
                    {
                        faces[key] = list = [];
                    }

                    list.Add(f);
                }
            }
        }

        return faces.Where(e => e.Value.Distinct().Any(f =>
        {
            var middle = (solid.Vertices[e.Key.Item1] + solid.Vertices[e.Key.Item2]) / 2;
            var corners = solid.Faces[f].Loop.Select(v => solid.Vertices[v]).ToArray();
            var centroid = corners.Aggregate(Vector3.Zero, (a, b) => a + b) / corners.Length;
            var at = middle + (Vector3.Normalize(centroid - middle) * nudge);
            var (origin, direction) = orthographic ? (at + (Vector3.Normalize(toEye) * 100), -Vector3.Normalize(toEye)) : (camera.Eye, Vector3.Normalize(at - camera.Eye));
            return solid.FirstHit(origin, direction) == f;
        })).Select(e => e.Key).OrderBy(e => e).ToArray();
    }

    public static IEnumerable<Probe> Probes(Solid solid)
    {
        for (var f = 0; f < solid.Faces.Length; f++)
        {
            if (solid.IsDeleted(f))
            {
                continue;
            }

            var loop = solid.Faces[f].Loop;
            var corners = loop.Select(v => solid.Vertices[v]).ToArray();
            var middle = corners.Aggregate(Vector3.Zero, (a, b) => a + b) / corners.Length;
            var shortest = corners.Select((c, k) => Vector3.Distance(c, corners[(k + 1) % corners.Length])).Where(d => d > 1e-4f).Min();
            var narrow = shortest < NarrowEdge;
            yield return new Probe(f, "middle", middle, narrow);
            for (var k = 0; k < corners.Length; k++)
            {
                // A corner between two collinear neighbours is not a corner of the shape.
                var (a, b) = (corners[(k + corners.Length - 1) % corners.Length], corners[(k + 1) % corners.Length]);
                if (Vector3.Cross(corners[k] - a, b - corners[k]).Length() < 1e-5f)
                {
                    continue;
                }

                yield return new Probe(f, $"corner {loop[k]}", Vector3.Lerp(corners[k], middle, Inset), narrow);
            }
        }
    }

    /// <summary>What the ray test is sure of at a probe's pixel: the face, -1 for the background, -2 when not sure.</summary>
    public static int Seen(Solid solid, Camera camera, Probe probe)
    {
        var (x, y) = camera.Pixel(probe.At);
        return probe.Narrow ? solid.SeenNarrow(camera, Size, x, y) : solid.Seen(camera, Size, x, y);
    }
}

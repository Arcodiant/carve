using System.Numerics;
using static Carve.Core.MeshEdits;

namespace Carve.Core;

/// <summary>
/// A greyscale height field (T-223): <see cref="Width"/> by <see cref="Height"/> values, row-major with the top row first,
/// texel (x, y) centred at uv ((x + 0.5) / width, (y + 0.5) / height), as <see cref="Baking.BakeImage"/> and M-7's
/// textures are, so uv (0, 0) is the top-left corner.
/// </summary>
public sealed class HeightField
{
    private readonly float[] _values;

    public HeightField(int width, int height, float[] values)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (values.Length != width * height) throw new ArgumentException("one value per texel", nameof(values));
        Width = width;
        Height = height;
        _values = values;
    }

    public int Width { get; }
    public int Height { get; }

    public float this[int x, int y] => _values[y * Width + x];

    /// <summary>Bilinear at <paramref name="uv"/>, wrapping in both directions.</summary>
    public double Sample(Vector2 uv)
    {
        double fx = uv.X * (double)Width - 0.5, fy = uv.Y * (double)Height - 0.5;
        double x0 = Math.Floor(fx), y0 = Math.Floor(fy);
        double tx = fx - x0, ty = fy - y0;
        int ax = Wrap((long)x0, Width), bx = Wrap((long)x0 + 1, Width), ay = Wrap((long)y0, Height), by = Wrap((long)y0 + 1, Height);
        double top = this[ax, ay] * (1 - tx) + this[bx, ay] * tx;
        double bottom = this[ax, by] * (1 - tx) + this[bx, by] * tx;
        return top * (1 - ty) + bottom * ty;
    }

    private static int Wrap(long v, int n) => (int)(((v % n) + n) % n);

    /// <summary>
    /// About how many features (a rise and a fall) the field has across, in the wider of its two directions: the mean,
    /// over rows and over columns, of how often a line crosses the field's mean, halved.
    /// </summary>
    public double FeaturesAcross()
    {
        var mean = _values.Average(v => (double)v);
        double Crossings(int n, Func<int, float> at)
        {
            var count = 0;
            for (var i = 0; i < n; i++)
                if ((at(i) > mean) != (at((i + 1) % n) > mean)) count++;
            return count / 2.0;
        }
        var rows = Enumerable.Range(0, Height).Average(y => Crossings(Width, x => this[x, y]));
        var columns = Enumerable.Range(0, Width).Average(x => Crossings(Height, y => this[x, y]));
        return Math.Max(rows, columns);
    }
}

/// <summary>Where <see cref="Displacement.Displace"/> reads a vertex's height h, 0 to 1.</summary>
public abstract record DisplaceSource;

/// <summary>A height field read at the vertex's UVs; <see cref="Name"/> names it in messages.</summary>
public sealed record FieldSource(HeightField Field, string Name) : DisplaceSource;

/// <summary>
/// Seeded 3D gradient noise summed over <see cref="Octaves"/> (fBm: each octave twice the frequency and half the
/// amplitude of the one before), read at the vertex's position over <see cref="Scale"/> metres, the first octave's cell.
/// </summary>
public sealed record NoiseSource(int Seed, double Scale, int Octaves) : DisplaceSource;

/// <summary>What a displace did beside its edit: the largest offset, in metres, and its warnings.</summary>
public sealed record DisplaceReport(double Largest, IReadOnlyList<string> Warnings);

/// <summary>
/// T-223's displace: each selected vertex moves along its vertex normal (or a fixed axis) by
/// <c>amount * (h - midlevel)</c>. Only positions change; ids, faces, UVs, groups and every other layer are kept.
/// </summary>
public static class Displacement
{
    public const int MaxOctaves = 8;

    /// <summary>
    /// Displaces the union of <paramref name="vertices"/> and every corner of <paramref name="faces"/>; the whole mesh's
    /// vertices (those on a live face) when both are null. <paramref name="report"/> is set when the edit succeeds.
    /// </summary>
    public static EditResult Displace(PolygonMesh mesh, IReadOnlyCollection<int>? vertices, DisplaceSource source,
        float amount, float midlevel, Vector3? axis, out DisplaceReport? report, IReadOnlyCollection<int>? faces = null, bool pinBorder = false)
    {
        report = null;
        var errors = new List<MeshError>();
        if (!float.IsFinite(amount)) errors.Add(new MeshError("amount", "is not finite"));
        else if (amount == 0) errors.Add(new MeshError("amount", "is zero: nothing would change"));
        if (!(midlevel >= 0 && midlevel <= 1)) errors.Add(new MeshError("midlevel", Invariant($"{midlevel} is outside 0 to 1")));
        Vector3? direction = null;
        if (axis is { } a)
        {
            if (!float.IsFinite(a.X) || !float.IsFinite(a.Y) || !float.IsFinite(a.Z)) errors.Add(new MeshError("axis", "is not finite"));
            else if (a == Vector3.Zero) errors.Add(new MeshError("axis", "is zero: it gives no direction"));
            else direction = Vector3.Normalize(a);
        }
        if (source is NoiseSource n)
        {
            if (!(n.Scale > 0 && double.IsFinite(n.Scale))) errors.Add(new MeshError("source.noise.scale", Invariant($"{n.Scale} is not above 0")));
            if (n.Octaves < 1 || n.Octaves > MaxOctaves) errors.Add(new MeshError("source.noise.octaves", Invariant($"{n.Octaves} is outside 1 to {MaxOctaves}")));
        }

        var selected = new SortedSet<int>();
        if (vertices is null && faces is null) selected.UnionWith(mesh.LiveFaces.SelectMany(f => mesh.Faces[f].Corners));
        else
        {
            errors.AddRange(UnknownElements(mesh, vertices ?? [], [], faces ?? []));
            selected.UnionWith((vertices ?? []).Where(v => mesh.VertexProblem(v) is null));
            selected.UnionWith((faces ?? []).Where(f => mesh.FaceProblem(f) is null).SelectMany(f => mesh.Faces[f].Corners));
        }
        if (pinBorder && (vertices is not null || faces is null))
            errors.Add(new MeshError("pin_border", "needs faces or group: a selection of vertices, or none, has no border"));
        else if (pinBorder && errors.Count == 0)
        {
            // A corner of any live face outside the selected faces stays where it is.
            var inside = faces!.ToHashSet();
            selected.ExceptWith(mesh.LiveFaces.Where(f => !inside.Contains(f)).SelectMany(f => mesh.Faces[f].Corners));
        }
        if (errors.Count == 0 && selected.Count == 0)
            errors.Add(new MeshError("selection", pinBorder ? "matches no vertex off the border: nothing to displace" : "matches no vertex: nothing to displace"));
        if (errors.Count > 0) return EditResult.Refused(errors);

        // Normals are taken from the mesh before any vertex moves: each face's unit normal weighted by its area.
        var normals = new Vector3[mesh.Vertices.Count];
        foreach (var f in mesh.LiveFaces)
        {
            var weighted = mesh.FaceNormal(f) * (float)mesh.FaceArea(f);
            foreach (var v in mesh.Faces[f].Corners) normals[v] += weighted;
        }

        // A vertex on no face, or whose faces' normals cancel (a two-sided card), has no normal: without an axis it has nowhere to go, so it is left out and named.
        var stranded = direction is null ? selected.Where(v => normals[v] == Vector3.Zero).ToList() : [];
        selected.ExceptWith(stranded);
        if (selected.Count == 0)
            return EditResult.Refused([new MeshError("selection", "matches no vertex with a normal (each is on no face, or its faces face opposite ways): give axis to move them")]);

        var heights = Heights(mesh, selected, source, errors);
        if (errors.Count > 0) return EditResult.Refused(errors);

        var largest = 0.0;
        var moved = new List<KeyValuePair<int, Vector3>>();
        foreach (var v in selected)
        {
            var along = direction ?? Vector3.Normalize(normals[v]);
            var offset = amount * (heights[v] - midlevel);
            largest = Math.Max(largest, Math.Abs(offset));
            moved.Add(KeyValuePair.Create(v, mesh.Vertices[v] + along * (float)offset));
        }

        var result = mesh.Edit(movedVertices: moved);
        var edited = EditResult.From(mesh, result, [.. selected], [], [], []);
        if (edited.Succeeded)
        {
            var warnings = new List<string>();
            if (stranded.Count > 0)
                warnings.Add($"vertices {Listed(stranded)} have no normal (on no face, or their faces face opposite ways) and did not move; give axis to move them");
            if (Coarse(mesh, selected, source) is { } coarse) warnings.Add(coarse);
            report = new DisplaceReport(largest, warnings);
        }
        return edited;
    }

    private static Dictionary<int, double> Heights(PolygonMesh mesh, IEnumerable<int> selected, DisplaceSource source, List<MeshError> errors)
    {
        var heights = new Dictionary<int, double>();
        switch (source)
        {
            case NoiseSource noise:
                foreach (var v in selected) heights[v] = Noise.Height(mesh.Vertices[v], noise);
                return heights;
            case FieldSource field:
                if (mesh.Uvs is not { IsEmpty: false } uvs || uvs.MappedFaces.Count == 0)
                {
                    errors.Add(new MeshError("source.graph", "the mesh has no UVs to read the graph at; give it UVs with project_uvs or unwrap first, or displace by noise"));
                    return heights;
                }
                // A vertex on a seam has several UVs: it takes the mean of its samples, so the seam does not tear.
                var sum = new Dictionary<int, (double Total, int Count)>();
                var wanted = selected.ToHashSet();
                foreach (var f in mesh.LiveFaces)
                {
                    if (uvs.Of(f) is not { } corners) continue;
                    var face = mesh.Faces[f];
                    for (var k = 0; k < face.Corners.Count; k++)
                    {
                        var v = face.Corners[k];
                        if (!wanted.Contains(v)) continue;
                        var (total, count) = sum.GetValueOrDefault(v);
                        sum[v] = (total + field.Field.Sample(corners[k]), count + 1);
                    }
                }
                var unmapped = wanted.Where(v => !sum.ContainsKey(v)).Order().ToList();
                if (unmapped.Count > 0)
                {
                    errors.Add(new MeshError("selection", $"vertices {Listed(unmapped)} are on no face with UVs, so the graph cannot be read for them; map their faces (project_uvs, unwrap) or leave them out"));
                    return heights;
                }
                foreach (var (v, (total, count)) in sum) heights[v] = total / count;
                return heights;
            default:
                throw new ArgumentException("an unknown source", nameof(source));
        }
    }

    /// <summary>Up to 10 ids, then how many more.</summary>
    private static string Listed(IReadOnlyList<int> ids) =>
        string.Join(", ", ids.Take(10)) + (ids.Count > 10 ? Invariant($" and {ids.Count - 10} more") : "");

    /// <summary>
    /// A warning when the selected part of the mesh is too coarse for the source to show: fewer than two vertices to a
    /// feature, where a feature is the noise's first cell or one rise and fall of the field across its UVs.
    /// </summary>
    private static string? Coarse(PolygonMesh mesh, IReadOnlySet<int> selected, DisplaceSource source)
    {
        var edges = mesh.Topology.Edges.Where(e => selected.Contains(e.Lo) && selected.Contains(e.Hi)).ToList();
        if (edges.Count == 0) return null;
        switch (source)
        {
            case NoiseSource noise:
            {
                var mean = edges.Average(e => (double)Vector3.Distance(mesh.Vertices[e.Lo], mesh.Vertices[e.Hi]));
                return mean > noise.Scale / 2
                    ? Invariant($"the mesh is too coarse for the noise to show: its edges average {mean:0.###} m, more than half of scale {noise.Scale:0.###} m; subdivide first, or raise scale")
                    : null;
            }
            case FieldSource field:
            {
                var lengths = new List<double>();
                foreach (var f in mesh.LiveFaces)
                {
                    if (mesh.Uvs?.Of(f) is not { } uv) continue;
                    var face = mesh.Faces[f];
                    for (var hole = -1; hole < face.Holes.Count; hole++)
                    {
                        var loop = face.LoopAt(hole);
                        for (var i = 0; i < loop.Count; i++)
                        {
                            int a = loop[i], b = loop[(i + 1) % loop.Count];
                            if (selected.Contains(a) && selected.Contains(b))
                                lengths.Add(Vector2.Distance(uv[face.CornerAt(hole, i)], uv[face.CornerAt(hole, (i + 1) % loop.Count)]));
                        }
                    }
                }
                if (lengths.Count == 0) return null;
                var across = 1 / lengths.Average();
                var features = field.Field.FeaturesAcross();
                return across < 2 * features
                    ? Invariant($"the mesh is too coarse for {field.Name} to show: about {across:0.#} vertices across its UVs, for about {features:0.#} features across the output; subdivide first")
                    : null;
            }
            default:
                return null;
        }
    }
}

/// <summary>
/// Seeded 3D gradient noise (Perlin's improved noise, gradients from an integer hash of the lattice point and the seed),
/// in double, so a seed and a position give the same value on every machine. No <see cref="System.Random"/>.
/// </summary>
public static class Noise
{
    /// <summary>The fBm of <paramref name="source"/> at <paramref name="p"/> mapped to 0 to 1: 0.5 + sum / (2 * the amplitudes' sum), clamped.</summary>
    public static double Height(Vector3 p, NoiseSource source)
    {
        double sum = 0, amplitude = 1, total = 0, frequency = 1 / source.Scale;
        for (var o = 0; o < source.Octaves; o++)
        {
            var seed = Mix((uint)source.Seed + (uint)o * 0x9E3779B9u);
            // Each octave is shifted off the lattice by its own fraction, so a lattice point (where gradient noise is 0) is not one in every octave.
            var shift = (seed & 0xFFFF) / 65536.0 + 0.5;
            sum += amplitude * Gradient(p.X * frequency + shift, p.Y * frequency + shift * 1.3, p.Z * frequency + shift * 1.7, seed);
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return Math.Clamp(0.5 + sum / (2 * total), 0, 1);
    }

    /// <summary>Gradient noise at (x, y, z), about -1 to 1, 0 at every lattice point.</summary>
    public static double Gradient(double x, double y, double z, uint seed)
    {
        double fx = Math.Floor(x), fy = Math.Floor(y), fz = Math.Floor(z);
        int ix = (int)fx, iy = (int)fy, iz = (int)fz;
        double dx = x - fx, dy = y - fy, dz = z - fz;
        double u = Fade(dx), v = Fade(dy), w = Fade(dz);
        double Corner(int cx, int cy, int cz) => Dot(Hash(ix + cx, iy + cy, iz + cz, seed), dx - cx, dy - cy, dz - cz);
        double Lerp(double a, double b, double t) => a + (b - a) * t;
        return Lerp(
            Lerp(Lerp(Corner(0, 0, 0), Corner(1, 0, 0), u), Lerp(Corner(0, 1, 0), Corner(1, 1, 0), u), v),
            Lerp(Lerp(Corner(0, 0, 1), Corner(1, 0, 1), u), Lerp(Corner(0, 1, 1), Corner(1, 1, 1), u), v), w);
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    /// <summary>One of the twelve cube-edge gradients (Perlin 2002), dotted with (x, y, z).</summary>
    private static double Dot(uint h, double x, double y, double z) => (h % 12) switch
    {
        0 => x + y, 1 => -x + y, 2 => x - y, 3 => -x - y,
        4 => x + z, 5 => -x + z, 6 => x - z, 7 => -x - z,
        8 => y + z, 9 => -y + z, 10 => y - z, _ => -y - z,
    };

    private static uint Hash(int x, int y, int z, uint seed) =>
        Mix(seed ^ ((uint)x * 0x8DA6B343u) ^ ((uint)y * 0xD8163841u) ^ ((uint)z * 0xCB1AB31Fu));

    private static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7FEB352Du;
        h ^= h >> 15; h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h;
    }
}

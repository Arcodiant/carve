using System.Numerics;
using Carve.Scenario.Crate;
using Carve.Scenario.Uvs;

namespace Carve.Scenario.Materials;

/// <summary>One face of the barrel as <c>get_mesh</c> gives it: its loop in 3D and its UV at each corner (null when unmapped).</summary>
internal sealed record FaceGeo(int Face, Vector3[] Loop, Vector2[]? Uv)
{
    public Vector3 Normal
    {
        get
        {
            var n = Vector3.Zero;
            for (var i = 0; i < Loop.Length; i++)
            {
                Vector3 a = Loop[i], b = Loop[(i + 1) % Loop.Length];
                n += new Vector3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
            }

            return Vector3.Normalize(n);
        }
    }

    public Vector3 Centre => Loop.Aggregate(Vector3.Zero, (s, v) => s + v) / Loop.Length;
}

/// <summary>
/// A pixel whose ray meets a face first. <c>Texel</c> is where its centre lands in the 256 x 256 textures (mip 0, in texels), <c>S</c> the mip-0
/// texels one pixel spans along its longer axis.
/// </summary>
internal sealed record Hit(int X, int Y, FaceGeo Face, Vector2 Texel, float S)
{
    /// <summary>Rule P2's half-width, in texels: the larger of 2 and 3 s (the trilinear footprint).</summary>
    public float Half => MathF.Max(2, 3 * S);
}

/// <summary>
/// One camera of <c>render</c> (a direction, a zoom about a target, the bounds of the whole model, the size of its tile) with the pixel rules of
/// T-78's P2 worked from the geometry: which face a pixel's centre sees (a ray test over every face), where that lands in the textures and how
/// many texels a pixel spans there, and whether the pixel is clear of every drawn edge. Nothing here reads a frame.
/// </summary>
internal sealed class Shot
{
    public const int TextureSize = 256;
    private readonly IReadOnlyList<FaceGeo> _faces;
    private readonly Func<float, float, (Vector3 Origin, Vector3 Direction)> _ray;
    private readonly (Vector2 A, Vector2 B)[] _edges;
    private readonly (int Left, int Top) _origin;

    public Shot(IReadOnlyList<FaceGeo> faces, Vector3 min, Vector3 max, Vector3 direction, int size, float zoom = 1, Vector3? target = null, int left = 0, int top = 0)
    {
        _faces = faces;
        Size = size;
        _origin = (left, top);
        _ray = CameraPixels.Unproject(min, max, direction, size, zoom, target);
        var pixel = CameraPixels.Perspective(min, max, direction, size, zoom, target);
        Eye = CameraPixels.Eye(min, max, direction, target);
        _edges = faces.SelectMany(f => f.Loop.Select((v, i) => (pixel(v), pixel(f.Loop[(i + 1) % f.Loop.Length])))).ToArray();
        Project = pixel;
    }

    public int Size { get; }

    public Vector3 Eye { get; }

    public Func<Vector3, Vector2> Project { get; }

    /// <summary>The frame's pixel of this tile's pixel (x, y).</summary>
    public (int X, int Y) InFrame(int x, int y) => (x + _origin.Left, y + _origin.Top);

    /// <summary>Rule P2's edge clearance: 3 px at 512, scaled with the tile.</summary>
    public float Clearance => 3f * Size / 512;

    public bool Clear(Vector2 centre) => _edges.All(e => Geo.DistanceToSegment(centre, e.A, e.B) >= Clearance);

    /// <summary>The first face the ray of the pixel <c>(x, y)</c> meets, with its UV, or null.</summary>
    public Hit? At(int x, int y)
    {
        var (origin, direction) = _ray(x + 0.5f, y + 0.5f);
        FaceGeo? seen = null;
        var best = float.MaxValue;
        foreach (var f in _faces)
        {
            var t = Along(f, origin, direction);
            if (t <= 1e-4f || t >= best) continue;
            if (Triangle(f, origin + direction * t, 1) is null) continue;
            (seen, best) = (f, t);
        }

        if (seen is null || seen.Uv is null) return null;
        var p = origin + direction * best;

        // The product's own triangulation (Triangulator: the first ear clipped is the one at corner 0, so a quad splits along its 1-3 diagonal,
        // the fan from corner 1; a cap's UVs are an affine image of it and so the same under any split).
        const int Fan = 1;
        var (t0, w0) = Triangle(seen, p, Fan)!.Value;
        var uv = Interpolate(seen, Fan, t0, w0);

        // The UV one pixel to the right and one below, extended affinely out of the same triangle.
        Vector2 Beyond(float dx, float dy)
        {
            var (o, d) = _ray(x + 0.5f + dx, y + 0.5f + dy);
            var q = o + d * Along(seen, o, d);
            return Interpolate(seen, Fan, t0, Barycentric(q, Corner(seen, Fan, t0, 0), Corner(seen, Fan, t0, 1), Corner(seen, Fan, t0, 2)));
        }

        var (right, below) = (Beyond(1, 0) - uv, Beyond(0, 1) - uv);
        var s = TextureSize * MathF.Max(MathF.Max(MathF.Abs(right.X), MathF.Abs(right.Y)), MathF.Max(MathF.Abs(below.X), MathF.Abs(below.Y)));
        return new Hit(x, y, seen, uv * TextureSize, s);
    }

    /// <summary>The parameter at which the ray meets the face's plane; infinity when parallel.</summary>
    private static float Along(FaceGeo f, Vector3 origin, Vector3 direction)
    {
        var n = f.Normal;
        var across = Vector3.Dot(n, direction);
        return MathF.Abs(across) < 1e-9f ? float.PositiveInfinity : Vector3.Dot(n, f.Loop[0] - origin) / across;
    }

    private static Vector3 Corner(FaceGeo f, int fan, int t, int k) => f.Loop[Index(f, fan, t, k)];

    /// <summary>Corner <paramref name="k"/> of the triangle <paramref name="t"/> of the fan from corner <paramref name="fan"/> (0 or 1).</summary>
    private static int Index(FaceGeo f, int fan, int t, int k)
    {
        var n = f.Loop.Length;
        return k == 0 ? fan % n : (fan + t + k - 1) % n;
    }

    private static (int T, (float, float, float) W)? Triangle(FaceGeo f, Vector3 p, int fan)
    {
        for (var t = 1; t + 1 < f.Loop.Length; t++)
        {
            var w = Barycentric(p, Corner(f, fan, t, 0), Corner(f, fan, t, 1), Corner(f, fan, t, 2));
            if (w.U >= -1e-5f && w.V >= -1e-5f && w.W >= -1e-5f) return (t, (w.U, w.V, w.W));
        }

        return null;
    }

    private static Vector2 Interpolate(FaceGeo f, int fan, int t, (float U, float V, float W) w) =>
        f.Uv![Index(f, fan, t, 0)] * w.U + f.Uv[Index(f, fan, t, 1)] * w.V + f.Uv[Index(f, fan, t, 2)] * w.W;

    private static (float U, float V, float W) Barycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector3.Dot(v0, v0), d01 = Vector3.Dot(v0, v1), d11 = Vector3.Dot(v1, v1), d20 = Vector3.Dot(v2, v0), d21 = Vector3.Dot(v2, v1);
        var den = d00 * d11 - d01 * d01;
        var v = (d11 * d20 - d01 * d21) / den;
        var w = (d00 * d21 - d01 * d20) / den;
        return (1 - v - w, v, w);
    }

    /// <summary>
    /// Every pixel of the tile that is a P2 sample site: a face sees it, it is 3 px (scaled) clear of every drawn edge and 4 px of the tile's border,
    /// and it is not under the caption.
    /// </summary>
    public IEnumerable<Hit> Sites(int captionWidth, int captionHeight)
    {
        for (var y = 4; y < Size - 4; y++)
        for (var x = 4; x < Size - 4; x++)
        {
            var (fx, fy) = InFrame(x, y);
            if (fx < captionWidth && fy < captionHeight) continue;
            if (At(x, y) is not { } hit || !Clear(new Vector2(x + 0.5f, y + 0.5f))) continue;
            yield return hit;
        }
    }

    /// <summary>
    /// The class of a hit under <paramref name="classify"/> (a texel's class, null for none): the class every texel the footprint touches has,
    /// else null. The footprint is the square of half-width <see cref="Hit.Half"/> about the texel, wrapping as the sampler's Repeat does.
    /// </summary>
    public static T? Class<T>(Vector2 texel, float half, Func<int, int, T?> classify) where T : struct
    {
        T? first = null;
        for (var y = (int)MathF.Floor(texel.Y - half); y <= (int)MathF.Floor(texel.Y + half); y++)
        for (var x = (int)MathF.Floor(texel.X - half); x <= (int)MathF.Floor(texel.X + half); x++)
        {
            var c = classify(((x % TextureSize) + TextureSize) % TextureSize, ((y % TextureSize) + TextureSize) % TextureSize);
            if (c is null) return null;
            if (first is null) first = c;
            else if (!EqualityComparer<T>.Default.Equals(first.Value, c.Value)) return null;
        }

        return first;
    }
}

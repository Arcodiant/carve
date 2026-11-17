using System.Numerics;

namespace Carve.Scenario.Crate;

/// <summary>
/// The Crate scenario's colour rule (T-30), apart from the crate so that other scenarios can link it. Under the PBR
/// render (T-82) a lit surface's pixel, in linear light, is T-82's diffuse, albedo x (ambient 0.25 + 0.9 n.l / pi), plus
/// the standard PBR specular of the material's own settings (metallic 0, roughness 1), which does not scale with the
/// colour. Since M-7 (M-19's third rebase) the fixed sky adds to both: its light (<see cref="SkyLight"/>, per world normal
/// and channel) to the diffuse, and its reflection (<see cref="SkySpecular"/>) to the specular. So the pixel of a surface
/// coloured <c>#RRGGBB</c> is the grey surface's pixel (0.72, 0.72, 0.70) under the same light, less the specular, times
/// colour / grey in each channel, plus the specular again. Each scenario judges the prediction at its own tolerance.
/// </summary>
/// <remarks>
/// The person's ruling (2026-11-14), word for word: "Yes, diffuse + specular": "ColourRule predicts T-82's diffuse plus
/// the standard PBR specular from the material's own settings; tolerance stays 2. Every check (cuts, gatehouse, crate)
/// must land within it, each changed check shown failing on its break; any that doesn't comes back to you."
/// The specular is Cook-Torrance: GGX at roughness 1 (D = 1 / pi), Smith G with k = (roughness + 1)^2 / 8, Schlick
/// Fresnel on v.h from F0 0.04, under the directional light's 0.9, v the camera's direction. One choice was set by
/// matching the frames, not by the settings: the diffuse is not reduced by (1 - F). With it, the door is
/// (71, 45, 24) against a measured (72, 46, 25); without it, (72, 46, 25) (M-19's rebase).
/// </remarks>
internal static class ColourRule
{
    /// <summary>The surface of a face with no colour (the product's default grey).</summary>
    public static readonly Vector3 Grey = new(0.72f, 0.72f, 0.70f);

    /// <summary>The exported and rendered material's roughness (the product's <c>GlbExport.Roughness</c>).</summary>
    public const double Roughness = 1;

    /// <summary>A dielectric's reflectance at normal incidence (metallic 0, the product's <c>GlbExport.Metallic</c>).</summary>
    public const double F0 = 0.04;

    /// <summary>The render's directional light (<c>RenderSystem</c>).</summary>
    public const double Light = 0.9;

    /// <summary>Colour / grey for each channel, the colour's channels in linear light.</summary>
    public static double[] Scale(string hex)
    {
        var (r, g, b) = Channels(hex);
        var albedo = new Vector3((float)Linear(r), (float)Linear(g), (float)Linear(b));
        return [albedo.X / Grey.X, albedo.Y / Grey.Y, albedo.Z / Grey.Z];
    }

    /// <summary>
    /// The coloured surface's pixel, in linear light, from the grey surface's <paramref name="grey"/> (linear, one per
    /// channel) of the same normal under the same light: (grey - specular) x colour / grey + specular, the sky's in both.
    /// </summary>
    public static double[] Predict(double[] grey, string hex, Vector3 normal, Vector3 direction)
    {
        var scale = Scale(hex);
        var s = Reflected(normal, direction);
        return [(grey[0] - s[0]) * scale[0] + s[0], (grey[1] - s[1]) * scale[1] + s[1], (grey[2] - s[2]) * scale[2] + s[2]];
    }

    /// <summary>
    /// The pixel, in linear light, of a face coloured <paramref name="hex"/> straight from T-82's light model, for a face
    /// with no grey face of the same normal in the frame: albedo x (ambient 0.25 + the light x n.l / pi + the sky's light)
    /// plus the specular, the sky's included.
    /// </summary>
    public static double[] Lit(string hex, Vector3 normal, Vector3 direction)
    {
        var (r, g, b) = Channels(hex);
        return Lit(Linear(r), Linear(g), Linear(b), normal, direction);
    }

    /// <summary>
    /// The sky's light on a face of world <paramref name="normal"/>, per channel, to be multiplied by the albedo: M-7's
    /// <c>ProceduralSky.Default</c> reaches every colour face as a per-frame SH projection the engine docs give no numbers
    /// for, so this is measured, not derived (M-19's <c>scenario-sky</c>, 26 normals, grey and white quads, front, back and
    /// straight on). It is world-fixed (the sky does not turn with the camera) and fits a first-order SH, constant plus
    /// linear in the normal, within 0.009 in linear light: mostly the blue of the zenith from above, neutral from below.
    /// </summary>
    public static double[] SkyLight(Vector3 normal)
    {
        var n = Vector3.Normalize(normal);
        return [0.3323 + (0.0066 * n.X) - (0.0499 * n.Y) - (0.0135 * n.Z),
            0.4163 + (0.0058 * n.X) + (0.0434 * n.Y) - (0.0128 * n.Z),
            0.5748 + (0.0047 * n.X) + (0.2200 * n.Y) - (0.0102 * n.Z)];
    }

    /// <summary>
    /// The sky's specular, per channel, on a face of <paramref name="normal"/> seen along <paramref name="direction"/>:
    /// the sky's light from the reflected direction times 0.0128 + 0.0267 (1 - n.v)^2, fitted to black quads (whose
    /// pixel is specular alone) in <c>scenario-sky</c>. It is small (0.003 to 0.02 in linear light) and does not scale
    /// with the colour; a black face is predicted within 3 levels, a grey or white one within 1 (2 where blue clips).
    /// </summary>
    public static double[] SkySpecular(Vector3 normal, Vector3 direction)
    {
        var n = Vector3.Normalize(normal);
        var v = Vector3.Normalize(direction);
        var nv = Math.Max(0, Vector3.Dot(n, v));
        var sky = SkyLight((2 * (float)nv * n) - v);
        var k = 0.0128 + (0.0267 * (1 - nv) * (1 - nv));
        return [sky[0] * k, sky[1] * k, sky[2] * k];
    }

    /// <summary>All the light a face reflects that does not scale with its colour: the light's <see cref="Specular"/> plus <see cref="SkySpecular"/>.</summary>
    public static double[] Reflected(Vector3 normal, Vector3 direction)
    {
        var s = Specular(normal, direction);
        var sky = SkySpecular(normal, direction);
        return [s + sky[0], s + sky[1], s + sky[2]];
    }

    /// <summary>
    /// The specular a face of <paramref name="normal"/> adds, in linear light, seen along <paramref name="direction"/>
    /// (towards the eye, as <c>render</c> takes it): D G F / (4 n.l n.v) x n.l x the light, with the light turned with
    /// the camera as <c>render</c> turns it.
    /// </summary>
    public static double Specular(Vector3 normal, Vector3 direction)
    {
        var n = Vector3.Normalize(normal);
        var v = Vector3.Normalize(direction);
        var l = ToLight(direction);
        var nl = Vector3.Dot(n, l);
        var nv = Vector3.Dot(n, v);
        if (nl <= 0 || nv <= 0)
        {
            return 0;
        }

        var h = Vector3.Normalize(l + v);
        var nh = Vector3.Dot(n, h);
        var a2 = Math.Pow(Roughness, 4);
        var d = a2 / (Math.PI * Math.Pow((nh * nh * (a2 - 1)) + 1, 2));
        var k = Math.Pow(Roughness + 1, 2) / 8;
        var g = nl / ((nl * (1 - k)) + k) * (nv / ((nv * (1 - k)) + k));
        var f = F0 + ((1 - F0) * Math.Pow(1 - Vector3.Dot(v, h), 5));
        return Light * d * g * f / (4 * nv);
    }

    /// <summary>
    /// The unit vector towards the directional light for a frame along <paramref name="direction"/>: the front view's
    /// light turned with the camera, as the product's <c>ViewFraming.LightOrientation</c> does (the light shines along its -Z).
    /// </summary>
    public static Vector3 ToLight(Vector3 direction)
    {
        Matrix4x4.Invert(LookRotation(Vector3.Normalize(direction)), out var cameraToWorld);
        var turn = FrontLight * LookRotation(Vector3.Normalize(FrontDirection)) * cameraToWorld;
        return Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, turn));
    }

    /// <summary>
    /// A grey face's red less its blue, in 8 bit sRGB levels, from the light model (<see cref="Grey"/> lit as <see cref="Lit"/>
    /// lights a colour): what "not highlighted" is judged against. Under M-7's sky a grey face is blue-cast, its R - B about
    /// -47 facing up and -24 facing a side from the front, so the old "|R - B| at most 6" no longer reads "grey" (the person's
    /// ruling of 2026-11-17, "Accept all", M-19 row 5).
    /// </summary>
    public static int GreyRedLessBlue(Vector3 normal, Vector3 direction)
    {
        var lit = Lit(Grey.X, Grey.Y, Grey.Z, normal, direction);
        return Encode(lit[0]) - Encode(lit[2]);
    }

    /// <summary>A linear channel in 8 bit sRGB, rounded and clipped.</summary>
    public static int Encode(double linear)
    {
        var c = Math.Clamp(linear, 0, 1);
        return (int)Math.Round(255 * (c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1 / 2.4)) - 0.055));
    }

    /// <summary>An 8 bit sRGB channel in linear light.</summary>
    public static double Linear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    /// <summary>A <c>#RRGGBB</c> colour's three 8 bit sRGB channels.</summary>
    private static (int R, int G, int B) Channels(string hex) =>
        (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));

    /// <summary>
    /// The pixel, in linear light, of a surface of linear albedo (<paramref name="r"/>, <paramref name="g"/>, <paramref name="b"/>)
    /// under T-82's light model with the sky: albedo x (ambient 0.25 + the light x n.l / pi + the sky's light) plus <see cref="Reflected"/>.
    /// </summary>
    private static double[] Lit(double r, double g, double b, Vector3 normal, Vector3 direction)
    {
        var nl = Math.Max(0, Vector3.Dot(Vector3.Normalize(normal), ToLight(direction)));
        var diffuse = 0.25 + (Light * nl / Math.PI);
        var sky = SkyLight(normal);
        var s = Reflected(normal, direction);
        return [(r * (diffuse + sky[0])) + s[0], (g * (diffuse + sky[1])) + s[1], (b * (diffuse + sky[2])) + s[2]];
    }

    private static readonly Vector3 FrontDirection = new(1.6f, 1.2f, 2.2f);
    private static readonly Matrix4x4 FrontLight = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);

    private static Matrix4x4 LookRotation(Vector3 direction) =>
        Matrix4x4.CreateLookAt(direction, Vector3.Zero, MathF.Abs(direction.Y) > MathF.Cos(MathF.PI / 180f) ? -Vector3.UnitZ : Vector3.UnitY)
            with { M41 = 0, M42 = 0, M43 = 0 };
}

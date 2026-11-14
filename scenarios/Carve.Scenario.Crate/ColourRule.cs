using System.Numerics;

namespace Carve.Scenario.Crate;

/// <summary>
/// The Crate scenario's colour rule (T-30), apart from the crate so that other scenarios can link it. Under the PBR
/// render (T-82) a lit surface's pixel, in linear light, is T-82's diffuse, albedo x (ambient 0.25 + 0.9 n.l / pi), plus
/// the standard PBR specular of the material's own settings (metallic 0, roughness 1), which does not scale with the
/// colour. So the pixel of a surface coloured <c>#RRGGBB</c> is the grey surface's pixel (0.72, 0.72, 0.70) under the
/// same light, less the specular, times colour / grey in each channel, plus the specular again. Each scenario judges
/// the prediction at its own tolerance.
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
        var (r, g, b) = (Convert.ToInt32(hex[1..3], 16), Convert.ToInt32(hex[3..5], 16), Convert.ToInt32(hex[5..7], 16));
        var albedo = new Vector3((float)Linear(r), (float)Linear(g), (float)Linear(b));
        return [albedo.X / Grey.X, albedo.Y / Grey.Y, albedo.Z / Grey.Z];
    }

    /// <summary>
    /// The coloured surface's pixel, in linear light, from the grey surface's <paramref name="grey"/> (linear, one per
    /// channel) of the same normal under the same light: (grey - specular) x colour / grey + specular.
    /// </summary>
    public static double[] Predict(double[] grey, string hex, Vector3 normal, Vector3 direction)
    {
        var scale = Scale(hex);
        var s = Specular(normal, direction);
        return [(grey[0] - s) * scale[0] + s, (grey[1] - s) * scale[1] + s, (grey[2] - s) * scale[2] + s];
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

    /// <summary>An 8 bit sRGB channel in linear light.</summary>
    public static double Linear(int channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static readonly Vector3 FrontDirection = new(1.6f, 1.2f, 2.2f);
    private static readonly Matrix4x4 FrontLight = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);

    private static Matrix4x4 LookRotation(Vector3 direction) =>
        Matrix4x4.CreateLookAt(direction, Vector3.Zero, MathF.Abs(direction.Y) > MathF.Cos(MathF.PI / 180f) ? -Vector3.UnitZ : Vector3.UnitY)
            with { M41 = 0, M42 = 0, M43 = 0 };
}

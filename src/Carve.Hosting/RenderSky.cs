using Empyrean;

namespace Carve.Hosting;

/// <summary>
/// The fixed sky that lights a face with a material (T-78): the engine's <see cref="ProceduralSky.Default"/> unchanged, sun
/// included, so a PBR metal has something to reflect. The viewer links no Carve code (T-79's ruling 4), so it repeats this
/// sky from <see cref="ProceduralSky.Default"/> in its own <c>ViewerSystem</c>: a change here does not reach the viewer.
/// </summary>
/// <remarks>
/// Nothing here follows the camera or Carve's directional light (<see cref="ProceduralSky.SunFromDirectionalLight"/> stays off),
/// so the engine bakes the sky's specular probe once, in the first frame, and every later view reads the same probe: no rebake,
/// no settling. Carve's pipeline composes no <see cref="SkyboxStage"/>, so no background is drawn and the dark slate stays.
/// The sky lights only the PBR path: its diffuse ambient and specular probe reach <see cref="MaterialFlags.Pbr"/> faces, and
/// a <c>LitMesh</c>, <c>SolidColor</c> or checker face draws exactly as it did without it.
/// </remarks>
public static class RenderSky
{
    /// <summary>The sky's component, put on one entity of the scene when it is built.</summary>
    public static ProceduralSky Sky => ProceduralSky.Default;
}

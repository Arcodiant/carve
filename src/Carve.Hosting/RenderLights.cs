using System.Numerics;
using Empyrean;

namespace Carve.Hosting;

/// <summary>
/// Carve's render lights, shared so a second server (patina's <c>swatch</c> sphere, T-128) lights a material exactly as
/// carve's <c>render</c> does: an ambient light, the fixed sky (<see cref="RenderSky"/>), and a directional light that keeps
/// its place relative to the camera, so it turns with each view.
/// </summary>
public static class RenderLights
{
    /// <summary>The ambient light's intensity (white).</summary>
    public const float AmbientIntensity = 0.25f;

    /// <summary>The directional light's intensity (white).</summary>
    public const float DirectionalIntensity = 0.9f;

    /// <summary>Within this cone of vertical, up is -Z: CreateLookAt returns NaN looking along up.</summary>
    public static readonly float VerticalCosine = MathF.Cos(MathF.PI / 180f);

    /// <summary>
    /// Carve's front camera direction (target to eye). The light's place relative to the camera is defined in this view:
    /// <see cref="FrontLight"/> there gives the agreed look, top bright, +Z middling, +X dark.
    /// </summary>
    public static readonly Vector3 FrontDirection = new(1.6f, 1.2f, 2.2f);

    /// <summary>The light's orientation in the front view.</summary>
    private static readonly Matrix4x4 FrontLight = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.6f);

    /// <summary>
    /// Adds the ambient light, the sky and the directional light (oriented <paramref name="orientation"/>, in that order) to
    /// <paramref name="scene"/>; returns the directional light's entity, whose <see cref="Transform3D"/> a view turns.
    /// </summary>
    public static Entity Add(ISceneManager scenes, IEntityManager entities, string scene, Quaternion orientation)
    {
        var ambient = scenes.CreateEntity(scene);
        entities.AddComponent(ambient, new AmbientLight { Color = Vector3.One, Intensity = AmbientIntensity });

        // The fixed sky (T-78): it lights PBR material faces only, baked once, and is never drawn.
        var sky = scenes.CreateEntity(scene);
        entities.AddComponent(sky, RenderSky.Sky);

        var light = scenes.CreateEntity(scene);
        entities.AddComponent(light, new DirectionalLight { Color = Vector3.One, Intensity = DirectionalIntensity });
        entities.AddComponent(light, new Transform3D
        {
            Position = Vector3.Zero,
            Scale = Vector3.One,
            Orientation = orientation,
        });
        return light;
    }

    /// <summary>
    /// The directional light's orientation for a camera looking at its target from <paramref name="cameraDirection"/>
    /// (target to eye; up is <see cref="UpFor"/>). The light keeps its place relative to the camera: the front light,
    /// turned by whatever turns the front camera into this one. A world-fixed light would leave the far faces ambient-only.
    /// </summary>
    public static Quaternion Orientation(Vector3 cameraDirection)
    {
        Matrix4x4.Invert(LookRotation(Vector3.Normalize(cameraDirection)), out var cameraToWorld);
        return Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(
            FrontLight * LookRotation(Vector3.Normalize(FrontDirection)) * cameraToWorld));
    }

    /// <summary>+Y, or -Z when the unit direction is within 1 degree of vertical.</summary>
    public static Vector3 UpFor(Vector3 direction) => MathF.Abs(direction.Y) > VerticalCosine ? -Vector3.UnitZ : Vector3.UnitY;

    /// <summary>World to camera, rotation only.</summary>
    private static Matrix4x4 LookRotation(Vector3 direction) =>
        Matrix4x4.CreateLookAt(direction, Vector3.Zero, UpFor(direction)) with { M41 = 0, M42 = 0, M43 = 0 };
}

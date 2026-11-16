using System.Numerics;
using Empyrean;

namespace Carve.Server;

/// <summary>
/// The lights of Carve's render scene, in one place so the equality test's host (T-78) lights a model exactly as
/// <c>render</c> does: the ambient light, the fixed sky (<see cref="RenderSky"/>), and the directional light that each view turns.
/// </summary>
internal static class RenderLights
{
    /// <summary>Adds the ambient light, the sky and the directional light to <paramref name="scene"/>; returns the directional light's entity.</summary>
    public static Entity Add(ISceneManager scenes, IEntityManager entities, string scene)
    {
        var ambient = scenes.CreateEntity(scene);
        entities.AddComponent(ambient, new AmbientLight { Color = Vector3.One, Intensity = 0.25f });

        // The fixed sky (T-78): it lights PBR material faces only, baked once, and is never drawn.
        var sky = scenes.CreateEntity(scene);
        entities.AddComponent(sky, RenderSky.Sky);

        var light = scenes.CreateEntity(scene);
        entities.AddComponent(light, new DirectionalLight { Color = Vector3.One, Intensity = 0.9f });
        entities.AddComponent(light, new Transform3D
        {
            Position = Vector3.Zero,
            Scale = Vector3.One,
            Orientation = ViewFraming.LightOrientation(RenderView.Front),
        });
        return light;
    }
}

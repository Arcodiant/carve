using Carve.Core;

namespace Carve.Rover;

/// <summary>
/// The rover built from <see cref="Model"/> calls, from <see cref="RoverSpec"/>'s data alone: the stages the T-96 scenario
/// takes through the tools. Core-side only (the scenario links <c>RoverSpec.cs</c>, not this file).
/// </summary>
internal static partial class RoverSpec
{
    public static Part ToPart(PartSpec p) => new(p.Name)
    {
        Parent = p.Parent,
        Translation = p.Translation,
        Rotation = p.Rotation,
        Envelope = p.Envelope is { } e ? new PartEnvelope(e.Min, e.Max) : null,
        InstanceOf = p.InstanceOf,
        Moves = p.Moves,
    };

    public static PolygonMesh Mesh(Shape shape)
    {
        var made = PolygonMesh.Create(shape.Positions, shape.Faces);
        return made.Mesh ?? throw new InvalidOperationException(string.Join("; ", made.Errors));
    }

    /// <summary>Level 0 kept on every part with geometry, live equal to it.</summary>
    public static Model BuildBlockout()
    {
        var model = Model.Empty;
        foreach (var p in Tree)
        {
            if (Blockout.TryGetValue(p.Name, out var shape)) model = model.With(p.Name, Mesh(shape));
            model = model.WithPart(ToPart(p));
        }
        foreach (var paint in BlockoutPaint) model = Apply(model, paint);
        foreach (var name in GeometryParts) model = model.WithLevel(name, 0);
        return model;
    }

    /// <summary>The whole rover: level 0 as above, then the refinement kept as level 1 on <see cref="RefinedParts"/>.</summary>
    public static Model Build()
    {
        var model = BuildBlockout().With("Wheel_FL", Mesh(RefinedWheel));
        foreach (var cut in Cuts)
        {
            var done = MeshEdits.Extrude(model.Find(cut.Part)!, [cut.Face], cut.Distance, cut.Inset);
            model = model.With(cut.Part, done.Mesh ?? throw new InvalidOperationException(string.Join("; ", done.Errors)));
        }
        foreach (var paint in RefinedPaint) model = Apply(model, paint);
        foreach (var name in RefinedParts) model = model.WithLevel(name, 1);
        return model;
    }

    /// <summary>The Mast turned 90° about +Y: the lamp swings from +X to -Z.</summary>
    public static Model TurnMast(Model model) => model.WithPart(model.FindPart("Mast")! with { Rotation = MastTurned });

    /// <summary>The planted fault: <c>Axle_F</c> lowered 0.3 m.</summary>
    public static Model LowerAxle(Model model) => model.WithPart(model.FindPart("Axle_F")! with { Translation = AxleLoweredTranslation });

    /// <summary>The instance edit: the boss cap of <c>Wheel_FL</c> moved; every wheel follows.</summary>
    public static Model EditHub(Model model)
    {
        var done = MeshEdits.Move(model.Find(HubMove.Part)!, null, null, [HubMove.Face], HubMove.By);
        return model.With(HubMove.Part, done.Mesh ?? throw new InvalidOperationException(string.Join("; ", done.Errors)));
    }

    private static Model Apply(Model model, Paint paint)
    {
        var mesh = model.Find(paint.Part)!;
        return model.With(paint.Part, mesh.WithStyle((mesh.Style ?? FaceStyle.None).WithColour(paint.Faces, paint.Colour)));
    }
}

extern alias carve;
using System.Text.Json;
using Carve.Rover;

namespace Carve.Server.Tests;

/// <summary>
/// T-94: the rover built from <see cref="RoverSpec"/> through the tools alone, on any client (the in-process harness or the
/// real process): the blockout kept as level 0, then the refined wheel, the cuts and their paint kept as level 1, as
/// <c>RoverSpec.Build()</c> makes it in Core.
/// </summary>
internal static class RoverTools
{
    public static async Task BuildAsync(Func<string, string, Task<JsonElement>> call, bool refine = true)
    {
        async Task Ok(string tool, string arguments)
        {
            var result = await call(tool, arguments);
            if (CarveProcess.IsError(result)) throw new InvalidOperationException($"{tool} {arguments}: {CarveProcess.TextOf(result)}");
        }

        foreach (var p in RoverSpec.Tree.Where(p => p.InstanceOf is null)) await Ok("define_part", PartToolsTests.PartArguments(p));
        foreach (var (name, shape) in RoverSpec.Blockout) await Ok("define_mesh", PartToolsTests.MeshArguments(name, shape));
        foreach (var p in RoverSpec.Tree.Where(p => p.InstanceOf is not null)) await Ok("define_part", PartToolsTests.PartArguments(p));
        foreach (var paint in RoverSpec.BlockoutPaint) await Ok("set_colour", Paint(paint));
        await Ok("keep_level", """{"level":0}""");
        if (!refine) return;

        await Ok("define_mesh", PartToolsTests.MeshArguments("Wheel_FL", RoverSpec.RefinedWheel));
        foreach (var cut in RoverSpec.Cuts)
            await Ok("extrude", JsonSerializer.Serialize(new { mesh = cut.Part, faces = new[] { cut.Face }, distance = cut.Distance, inset = cut.Inset }));
        foreach (var paint in RoverSpec.RefinedPaint) await Ok("set_colour", Paint(paint));
        await Ok("keep_level", JsonSerializer.Serialize(new { level = 1, parts = RoverSpec.RefinedParts }));
    }

    private static string Paint(RoverSpec.Paint paint) =>
        JsonSerializer.Serialize(new { mesh = paint.Part, faces = paint.Faces, colour = paint.Colour });
}

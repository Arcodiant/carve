using Carve.Graphs;
using Patina.Graphs;

namespace Carve.Server;

/// <summary>
/// The one error a graph slot that cannot be baked gives (T-163; T-166 owns its wording): the material, the slot and the
/// cause's own message. The causes: the file gone or unreadable and an output the slot cannot take
/// (<see cref="GraphSlotException"/>), a fault in the graph (<see cref="GraphFaultException"/>), no UVs, overlapping UVs,
/// a bad size or a missing group or material (<see cref="MeshMapsException"/>), and a mesh that is not there (<see cref="ArgumentException"/>).
/// </summary>
internal static class GraphSlotProblem
{
    public static string Of(string material, string slot, Exception cause) => $"material '{material}', {slot}: {cause.Message}";

    /// <summary>True for a cause <see cref="Of"/> reports; anything else is a fault of carve's own and is let through.</summary>
    public static bool IsCause(Exception e) => e is GraphSlotException or GraphFaultException or MeshMapsException or ArgumentException or IOException;
}

/// <summary>A graph slot that cannot be baked, its message <see cref="GraphSlotProblem.Of"/>'s.</summary>
internal sealed class GraphSlotProblemException(string message, Exception cause) : InvalidOperationException(message, cause);

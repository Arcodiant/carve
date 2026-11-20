namespace Patina.Graphs.Imaging;

/// <summary>What a port carries: one channel (greyscale) or three (colour). <see cref="Any"/> is for an input port that takes either.</summary>
public enum PortKind { Grey, Colour, Any }

public static class PortKinds
{
    public static string Name(PortKind kind) => kind switch
    {
        PortKind.Grey => "greyscale",
        PortKind.Colour => "colour",
        _ => "greyscale or colour",
    };

    public static int Channels(PortKind kind) => kind == PortKind.Colour ? 3 : 1;
}

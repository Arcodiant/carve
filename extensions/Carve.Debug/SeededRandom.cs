namespace Carve.Debug;

/// <summary>
/// The run's one source of random choices, from <c>Carve:Debug:Seed</c> (default 1). A scenario
/// takes this in its constructor. Not thread-safe, as <see cref="System.Random"/> is not.
/// </summary>
public sealed class SeededRandom(int seed)
{
    public int Seed { get; } = seed;

    public Random Random { get; } = new(seed);
}

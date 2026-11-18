
namespace Carve.Server;

/// <summary>
/// The <c>targets</c> argument of <c>render</c> and <c>check</c> (T-103): morph target name to weight. Compared by value, so
/// the same weights are not a change of view. Carve stores no weight range; these two tools refuse a weight outside 0 to 1
/// (the person's ruling of 2026-11-18), and a game drives the exported morphs however it likes.
/// </summary>
internal sealed class TargetWeights(IReadOnlyDictionary<string, float> byName) : IEquatable<TargetWeights>
{
    public IReadOnlyDictionary<string, float> ByName { get; } = byName;

    public bool Equals(TargetWeights? other) =>
        other is not null && ByName.Count == other.ByName.Count
        && ByName.All(p => other.ByName.TryGetValue(p.Key, out var w) && BitConverter.SingleToInt32Bits(w) == BitConverter.SingleToInt32Bits(p.Value));

    public override bool Equals(object? obj) => Equals(obj as TargetWeights);

    public override int GetHashCode() => ByName.Count;
}

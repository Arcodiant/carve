using System.Globalization;
using System.Text;

namespace Carve.ModelViewer;

/// <summary>
/// The window's weight dials (M-21's agreed sketch): one row per target with <c>&gt;</c> on the selected one, a bar and its
/// weight to 2 places. Plain state and text, so it needs no engine; <see cref="ViewerSystem"/> feeds it keys and draws it.
/// </summary>
internal sealed class WeightPanel(IReadOnlyList<string> names, float[] weights, string? clip)
{
    public const float Step = 0.05f;

    private const int BarWidth = 20;

    public int Selected { get; private set; }

    public bool Playing { get; private set; }

    public float[] Weights { get; } = weights;

    public void Next()
    {
        if (names.Count > 0)
        {
            Selected = (Selected + 1) % names.Count;
        }
    }

    /// <summary>Moves the selected weight by <paramref name="steps"/> of 0.05, held to 0 to 1 and kept to whole hundredths.</summary>
    public void Nudge(int steps)
    {
        if (names.Count > 0)
        {
            Weights[Selected] = MathF.Round(Math.Clamp(Weights[Selected] + (steps * Step), 0f, 1f), 2);
        }
    }

    public void Reset() => Array.Clear(Weights);

    /// <summary>Flips play and stop of the file's first clip; false when it has none.</summary>
    public bool TogglePlay()
    {
        if (clip is null)
        {
            return false;
        }

        Playing = !Playing;
        return true;
    }

    public string Text()
    {
        var text = new StringBuilder();
        var width = names.Count == 0 ? 0 : names.Max(n => n.Length);
        for (var i = 0; i < names.Count; i++)
        {
            var marked = (int)MathF.Round(Weights[i] * BarWidth);
            text.Append(i == Selected ? "> " : "  ")
                .Append(names[i].PadRight(width))
                .Append(" [").Append('=', marked).Append('-', BarWidth - marked).Append("]  ")
                .AppendLine(Weights[i].ToString("0.00", CultureInfo.InvariantCulture));
        }

        if (names.Count > 0)
        {
            text.AppendLine().AppendLine("Tab: next target    Left / Right: -/+ 0.05");
        }

        if (clip is not null)
        {
            text.Append($"Space: {(Playing ? "stop" : "play")} {clip}     ");
        }

        if (names.Count > 0)
        {
            text.Append("0: all weights to 0");
        }

        return text.ToString().TrimEnd();
    }
}

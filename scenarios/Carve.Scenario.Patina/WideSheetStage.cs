using System.Text.Json;
using System.Text.Json.Nodes;
using Carve.Debug;
using ModelContextProtocol.Protocol;
using StbImageSharp;

namespace Carve.Scenario.Patina;

/// <summary>
/// RV-76's stage (T-176): a sheet of more than 15 outputs, which threw past the label atlas's 32 rows, now draws, the atlas
/// grown a page. A graph of one dark colour with base_colour and 17 named outputs (18 columns, 38 labels) is drawn as a
/// sheet, and the labels past the first page (the last columns' and the sphere's) are looked for as white text on their
/// backing. No frame is captured: every sheet of 15 outputs or fewer is drawn as before, and the stages' frames hold that.
/// </summary>
internal sealed class WideSheetStage : IPatinaStage
{
    private const int Named = 17;

    // The sheet's layout, as patina's SheetLayout and LabelAtlas give it (the scenario does not reference the server).
    private const int Cell = 256, Gutter = 2, Title = 24, Padding = 2, SphereCell = 512;

    public async Task RunAsync(ScenarioDriver driver, PatinaChecks checks)
    {
        var nodes = new JsonObject
        {
            ["c"] = new JsonObject { ["type"] = "colour", ["params"] = new JsonObject { ["colour"] = "#202020" } },
            ["out"] = new JsonObject { ["type"] = "output", ["inputs"] = new JsonObject { ["in"] = "c" } },
        };
        // Each name a letter longer than the one before (RV-77), so no label's text is as wide as another's: a page drawn with
        // another page's labels, or with its labels a row out, puts text of another width in a label's backing.
        var names = Enumerable.Range(1, Named).Select(i => "m" + new string('x', i)).ToList();
        foreach (var name in names)
            nodes["o_" + name] = new JsonObject { ["type"] = "output", ["params"] = new JsonObject { ["name"] = name }, ["inputs"] = new JsonObject { ["in"] = "c" } };
        var graph = new JsonObject { ["version"] = 1, ["size"] = 64, ["seed"] = 0, ["nodes"] = nodes };
        await checks.CallAsync("define_graph", JsonSerializer.Serialize(new { graph }));

        var result = await driver.CallAsync("swatch", new { view = "sheet" });
        var text = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        var image = result.Content.OfType<ImageContentBlock>().FirstOrDefault();
        checks.Check(result.IsError is not true && image is not null, "wide sheet: a sheet of 18 outputs was not drawn: " + text);
        if (image is null) return;
        var columns = 1 + Named;
        var decoded = ImageResult.FromMemory(image.DecodedData.ToArray(), ColorComponents.RedGreenBlueAlpha);
        var width = columns * (Cell + Gutter) + SphereCell;
        checks.Check(decoded.Width == width && text.Contains($"; {width} x {Title + 2 * Cell + Gutter} px", StringComparison.Ordinal),
            $"wide sheet: the sheet is {decoded.Width} px wide, not {width}: " + text);
        checks.Check(text.Contains("columns base_colour, " + string.Join(", ", names) + ",", StringComparison.Ordinal), "wide sheet: the caption does not name every column: " + text);

        // Whether a label's band, from (left, top), holds white text: the cells are #202020, so only text is bright.
        bool Lettered(int left, int top)
        {
            for (var y = top; y < top + 18; y++)
                for (var x = left; x < left + 40; x++)
                    if (decoded.Data[(y * decoded.Width + x) * 4] > 200) return true;
            return false;
        }

        // Labels 0 to 31 are the first page; the last columns' (rows 31 to 36) and the sphere's (row 37) are on the second.
        for (var column = 15; column < columns; column++)
        {
            var left = column * (Cell + Gutter) + Padding;
            checks.Check(Lettered(left, Title + Padding), $"wide sheet: column {column}'s flat label is not drawn");
            checks.Check(Lettered(left, Title + Cell + Gutter + Padding), $"wide sheet: column {column}'s tiled label is not drawn");
        }
        checks.Check(Lettered(columns * (Cell + Gutter) + Padding, Title + 1 + Padding), "wide sheet: the sphere's label is not drawn");

        // Each label's text against its backing, which is sized from the label's own measured text (RV-77): the text ends in
        // the backing's last few pixels before its padding, never in the padding, so text of another width (another label, or
        // one a row out) fails; and a column's tiled label ("mxx 3 x 3") begins with the flat label's text, pixel for pixel.
        for (var column = 1; column < columns; column++)
        {
            var left = column * (Cell + Gutter) + Padding;
            var flat = Fit(left, Title + Padding);
            var tiled = Fit(left, Title + Cell + Gutter + Padding);
            checks.Check(flat.Fits && tiled.Fits, $"wide sheet: column {column}'s labels do not end where their backing does (flat: {flat.Why}; tiled: {tiled.Why})");
            var same = flat.Width > 0;
            for (var y = 0; y < 16 && same; y++)
                for (var x = 0; x < flat.Width - Padding && same; x++)
                    same = Pixel(left + x, Title + Padding + y) == Pixel(left + x, Title + Cell + Gutter + Padding + y);
            checks.Check(same, $"wide sheet: column {column}'s tiled label does not begin with its flat label's text");
        }
        File.AppendAllText(Path.Combine(driver.RunFolder, "wide-sheet-labels.tsv"), string.Join("", Enumerable.Range(1, columns - 1).Select(column =>
        {
            var left = column * (Cell + Gutter) + Padding;
            var (fits, width, last, why) = Fit(left, Title + Padding);
            return $"{column}\t{width}\t{last}\t{fits}\t{why}\n";
        })));

        (byte, byte, byte) Pixel(int x, int y)
        {
            var at = (y * decoded.Width + x) * 4;
            return (decoded.Data[at], decoded.Data[at + 1], decoded.Data[at + 2]);
        }

        // A label's backing, from (left, top): the run of pixels darker than the #202020 cell along its top row, and the
        // rightmost text pixel (red over 200) in its first 16 rows.
        (bool Fits, int Width, int Last, string Why) Fit(int left, int top)
        {
            var width = 0;
            while (width < Cell && decoded.Data[(top * decoded.Width + left + width) * 4] < 0x18) width++;
            var last = -1;
            for (var y = top; y < top + 16; y++)
                for (var x = 0; x < width; x++)
                    if (decoded.Data[(y * decoded.Width + left + x) * 4] > 200) last = Math.Max(last, x);
            if (width == 0) return (false, 0, last, "no backing");
            if (last < 0) return (false, width, last, "no text");
            if (last >= width - Padding) return (false, width, last, $"text in the padding, at {last} of {width}");
            if (last < width - Padding - TextSlack) return (false, width, last, $"text ends at {last}, short of its backing's {width}");
            return (true, width, last, "");
        }
    }

    /// <summary>How far short of its backing's padding a label's last bright pixel may end: the last glyph's side bearing and edge.</summary>
    private const int TextSlack = 4;
}

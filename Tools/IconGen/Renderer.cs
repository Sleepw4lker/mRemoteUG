using System.Text.RegularExpressions;
using SkiaSharp;

namespace IconGen;

/// <summary>
/// Turns one Fluent icon's SVG markup into a PNG at an exact pixel size.
/// </summary>
public static partial class Renderer
{
    [GeneratedRegex("""<path\b[^>]*?\bd="([^"]+)"[^>]*>""", RegexOptions.Singleline)]
    private static partial Regex PathElement();

    [GeneratedRegex("""<(?<tag>[a-zA-Z:-]+)\b""")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\b(?<attr>stroke|opacity|clip-path|fill)\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex RiskyAttribute();

    /// <summary>
    /// Why this markup cannot be rendered as a flat one-colour mask, or null when it can.
    /// </summary>
    /// <remarks>
    /// Measured across all 20392 Regular and Filled icons: 99.2% are a single &lt;path d=…&gt; and
    /// the whole attribute vocabulary is `d` (20950), `fill` (238), `clip-path` (5), `opacity` (3)
    /// and `stroke` (1). So anything carrying paint of its own is both rare and a genuine signal
    /// that the icon is multi-tone, which is a thing to be told about rather than to flatten.
    /// </remarks>
    public static string? WhyNotAMask(string markup)
    {
        foreach (var tag in AnyTag().Matches(markup).Select(m => m.Groups["tag"].Value).Distinct())
            if (tag is not "path")
                return $"carries a <{tag}> element, so it is not a single flat shape";

        foreach (Match match in RiskyAttribute().Matches(markup))
        {
            var attr = match.Groups["attr"].Value;
            var value = match.Groups["value"].Value.Trim();

            // currentColor and none are the two that mean "no opinion", which is what a mask wants.
            if (attr is "fill" && value is "currentColor" or "none" or "")
                continue;

            return $"""carries {attr}="{value}", so its own paint would be discarded""";
        }

        return null;
    }

    /// <summary>How much of the frame a badge covers, measured from the bottom-right corner.</summary>
    private const float BadgeScale = 0.6f;

    /// <summary>Renders the markup, authored for <paramref name="viewBox"/>, at N x N pixels.</summary>
    public static byte[] ToPng(string markup, int viewBox, int pixels, SKColor color, bool badge = false)
    {
        using var bitmap = new SKBitmap(pixels, pixels, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);

            // A badge occupies the bottom-right BadgeScale of the frame; everything else fills it.
            var scale = pixels / (float)viewBox;
            if (badge)
            {
                canvas.Translate(pixels * (1 - BadgeScale), pixels * (1 - BadgeScale));
                scale *= BadgeScale;
            }

            canvas.Scale(scale);

            using var paint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var drew = false;
            foreach (Match match in PathElement().Matches(markup))
            {
                using var path = SKPath.ParseSvgPathData(match.Groups[1].Value);
                if (path is null)
                    throw new InvalidDataException("SkiaSharp could not parse a path.");

                // Fluent's geometry relies on even-odd to punch holes - a folder's fold, the
                // inside of an O. Skia defaults to winding, which fills them in solid.
                path.FillType = SKPathFillType.EvenOdd;
                canvas.DrawPath(path, paint);
                drew = true;
            }

            if (!drew)
                throw new InvalidDataException("No <path> elements were found.");
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

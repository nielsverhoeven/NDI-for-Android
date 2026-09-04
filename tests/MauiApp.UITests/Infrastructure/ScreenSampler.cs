using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using SkiaSharp;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>A colour read off the screen, plus the WCAG relative luminance maths.</summary>
public readonly record struct SampledColor(byte R, byte G, byte B)
{
    /// <summary>WCAG 2.1 relative luminance.</summary>
    public double Luminance
    {
        get
        {
            static double Channel(byte v)
            {
                var s = v / 255d;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B);
        }
    }

    /// <summary>WCAG 2.1 contrast ratio between two colours, from 1:1 to 21:1.</summary>
    public static double Contrast(SampledColor a, SampledColor b)
    {
        var (hi, lo) = a.Luminance >= b.Luminance ? (a.Luminance, b.Luminance) : (b.Luminance, a.Luminance);
        return (hi + 0.05) / (lo + 0.05);
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>
/// Reads actual pixels off the device screen.
/// </summary>
/// <remarks>
/// <para>
/// The view tree cannot answer the question #294 asked. A MAUI <c>Path</c>'s <c>Fill</c> is not
/// exposed to the accessibility tree at all, so "are the rail icons still white on a light
/// background" is unanswerable through Appium's element API — which is precisely why the suite was
/// blind to a defect that was glaringly obvious to a user. Pixels are the only ground truth
/// available, so this samples them.
/// </para>
/// <para>
/// Screenshots come back in device pixels while element bounds are already device pixels too, so
/// no scaling is applied between them. Where the two disagree — some devices return a screenshot
/// scaled to the display's real resolution — <see cref="Capture"/> records both sizes so a
/// mismatch is visible in the failure message rather than silently sampling the wrong region.
/// </para>
/// </remarks>
public sealed class ScreenSampler : IDisposable
{
    private readonly SKBitmap _bitmap;

    /// <summary>Screenshot dimensions in pixels.</summary>
    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;

    private ScreenSampler(SKBitmap bitmap) => _bitmap = bitmap;

    /// <summary>Takes a screenshot and decodes it for sampling.</summary>
    public static ScreenSampler Capture(AndroidDriver driver)
    {
        var bytes = driver.GetScreenshot().AsByteArray;
        var bitmap = SKBitmap.Decode(bytes)
            ?? throw new InvalidOperationException(
                $"Could not decode the device screenshot ({bytes.Length} bytes). " +
                "Colour assertions cannot run without it.");

        return new ScreenSampler(bitmap);
    }

    /// <summary>The colour at a single point.</summary>
    public SampledColor At(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            throw new ArgumentOutOfRangeException(
                nameof(x), $"({x},{y}) is outside the {Width}x{Height} screenshot.");

        var c = _bitmap.GetPixel(x, y);
        return new SampledColor(c.Red, c.Green, c.Blue);
    }

    /// <summary>
    /// The most common colour inside an element's bounds — its background in practice.
    /// </summary>
    public SampledColor DominantColorOf(IWebElement element, double inset = 0.0) =>
        Histogram(element, inset).MaxBy(kv => kv.Value).Key;

    /// <summary>
    /// The colour that contrasts most with <paramref name="against"/> inside an element's bounds.
    /// </summary>
    /// <remarks>
    /// This is how a glyph is found without knowing its shape: the icon occupies a minority of its
    /// container's pixels, so averaging would drown it in background. Taking the extreme instead
    /// finds the drawn foreground whatever form it takes — and if the icon is the *same* colour as
    /// the background, which is exactly the #294 defect, the most-contrasting colour it can find
    /// is the background itself and the ratio collapses to 1:1.
    /// </remarks>
    public SampledColor MostContrastingColorIn(IWebElement element, SampledColor against, double inset = 0.0)
    {
        var histogram = Histogram(element, inset);

        // Ignore colours that occupy a handful of pixels: anti-aliasing and JPEG-ish artefacts
        // around an edge produce near-arbitrary extremes that are not what the user perceives.
        var floor = Math.Max(2, histogram.Values.Sum() / 500);

        return histogram
            .Where(kv => kv.Value >= floor)
            .OrderByDescending(kv => SampledColor.Contrast(kv.Key, against))
            .Select(kv => kv.Key)
            .DefaultIfEmpty(against)
            .First();
    }

    /// <summary>Counts every colour inside an element's bounds.</summary>
    /// <param name="inset">
    /// Fraction of width/height to trim from each edge before sampling. A container's own border
    /// and the gap to its neighbour both fall inside its reported bounds, so trimming keeps the
    /// sample on the element rather than on what surrounds it.
    /// </param>
    private Dictionary<SampledColor, int> Histogram(IWebElement element, double inset)
    {
        var location = element.Location;
        var size = element.Size;

        var trimX = (int)(size.Width * inset);
        var trimY = (int)(size.Height * inset);

        var left   = location.X + trimX;
        var top    = location.Y + trimY;
        var right  = location.X + size.Width - trimX;
        var bottom = location.Y + size.Height - trimY;

        // Clamp rather than throw: an element partly off-screen still has a sampleable portion,
        // and refusing to sample it would fail the test for the wrong reason.
        left   = Math.Clamp(left, 0, Width - 1);
        top    = Math.Clamp(top, 0, Height - 1);
        right  = Math.Clamp(right, left + 1, Width);
        bottom = Math.Clamp(bottom, top + 1, Height);

        var histogram = new Dictionary<SampledColor, int>();

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var c = _bitmap.GetPixel(x, y);
                var key = new SampledColor(c.Red, c.Green, c.Blue);
                histogram[key] = histogram.TryGetValue(key, out var n) ? n + 1 : 1;
            }
        }

        if (histogram.Count == 0)
            throw new InvalidOperationException(
                $"Sampled no pixels for an element at {location} sized {size} " +
                $"in a {Width}x{Height} screenshot. Element bounds and screenshot may be in " +
                "different coordinate spaces.");

        return histogram;
    }

    /// <summary>Writes the screenshot alongside the other failure evidence.</summary>
    public void SaveTo(string path)
    {
        using var image = SKImage.FromBitmap(_bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    public void Dispose() => _bitmap.Dispose();
}

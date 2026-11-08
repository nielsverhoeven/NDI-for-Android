using OpenQA.Selenium.Appium.Android;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>
/// Device geometry the layout assertions need: system-bar insets and display density.
/// </summary>
/// <remarks>
/// <para>
/// Every accessor here throws when the underlying value cannot be read, and none of them has a
/// default. That is deliberate. A status-bar height that quietly falls back to 0 turns "assert the
/// rail sits below the status bar" into "assert y >= 0", which passes against the exact bug it was
/// written to catch (#296). An unavailable measurement is an infrastructure failure and must look
/// like one.
/// </para>
/// </remarks>
public sealed class DeviceMetrics
{
    private readonly AndroidDriver _driver;

    public DeviceMetrics(AndroidDriver driver) => _driver = driver;

    /// <summary>
    /// Height of the status bar in pixels, as the system reports it right now.
    /// </summary>
    /// <remarks>
    /// Read per call rather than cached: the inset changes on rotation, and on devices with a
    /// display cutout it differs between orientations.
    /// </remarks>
    public int StatusBarHeight
    {
        get
        {
            var bars = Invoke("mobile: getSystemBars");

            if (bars is not Dictionary<string, object> map || !map.TryGetValue("statusBar", out var bar))
                throw new InvalidOperationException(
                    $"'mobile: getSystemBars' returned no statusBar entry (got: {Describe(bars)}). " +
                    "The inset assertions cannot run without it.");

            if (bar is not Dictionary<string, object> status || !status.TryGetValue("height", out var height))
                throw new InvalidOperationException(
                    $"statusBar entry carried no height (got: {Describe(bar)}).");

            return Convert.ToInt32(height);
        }
    }

    /// <summary>
    /// The navigation bar's rectangle in screen pixels, as the system reports it right now.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On a phone with 3-button navigation the bar leaves the bottom edge in landscape and takes a
    /// side — the left side under <c>ROTATION_270</c>. The left rail is on that same side, and the
    /// window draws edge-to-edge, so this rectangle is what decides whether a rail item's tap
    /// point is inside system UI (#321). Read per call: it moves on every rotation.
    /// </para>
    /// <para>
    /// Like every other accessor here, a missing or malformed entry throws rather than defaulting:
    /// a bar that silently read as 0x0 would make "no rail item is covered" pass for free.
    /// </para>
    /// </remarks>
    public SystemBarBounds NavigationBar
    {
        get
        {
            var bars = Invoke("mobile: getSystemBars");

            if (bars is not Dictionary<string, object> map || !map.TryGetValue("navigationBar", out var bar))
                throw new InvalidOperationException(
                    $"'mobile: getSystemBars' returned no navigationBar entry (got: {Describe(bars)}). " +
                    "The navigation-bar overlap assertions cannot run without it.");

            if (bar is not Dictionary<string, object> nav)
                throw new InvalidOperationException(
                    $"navigationBar entry was not a map (got: {Describe(bar)}).");

            return new SystemBarBounds(
                Read(nav, "x"),
                Read(nav, "y"),
                Read(nav, "width"),
                Read(nav, "height"),
                nav.TryGetValue("visible", out var visible) && Convert.ToBoolean(visible));

            static int Read(Dictionary<string, object> map, string key) =>
                map.TryGetValue(key, out var value)
                    ? Convert.ToInt32(value)
                    : throw new InvalidOperationException(
                        $"navigationBar entry carried no '{key}' (got: {Describe(map)}).");
        }
    }

    /// <summary>Display density, i.e. physical pixels per density-independent unit.</summary>
    public double Density
    {
        get
        {
            var info = Invoke("mobile: deviceInfo");

            if (info is Dictionary<string, object> map &&
                map.TryGetValue("displayDensity", out var density))
            {
                // displayDensity is reported in DPI (e.g. 560), not as a scale factor.
                var dpi = Convert.ToDouble(density);
                if (dpi > 0)
                    return dpi / 160d;
            }

            throw new InvalidOperationException(
                $"'mobile: deviceInfo' returned no usable displayDensity (got: {Describe(info)}). " +
                "Touch-target assertions are expressed in dp and cannot run without it.");
        }
    }

    /// <summary>
    /// The device's short edge in dp — Android's own phone/tablet discriminator
    /// (<c>Configuration.SmallestScreenWidthDp</c>, sw600dp), mirrored for the test side so a
    /// compact-device-only assertion can skip on a tablet. Read from the *real* display size, not
    /// from the app window: the window can exclude system decorations, and the Tab A9+ sits on the
    /// 600 dp boundary where that difference decides the answer.
    /// </summary>
    public double SmallestWidthDp
    {
        get
        {
            var info = Invoke("mobile: deviceInfo");

            if (info is Dictionary<string, object> map &&
                map.TryGetValue("realDisplaySize", out var size) &&
                size?.ToString() is { } text &&
                text.Split('x') is [var first, var second] &&
                int.TryParse(first, out var width) && int.TryParse(second, out var height))
            {
                return Math.Min(width, height) / Density;
            }

            throw new InvalidOperationException(
                $"'mobile: deviceInfo' returned no usable realDisplaySize (got: {Describe(info)}). " +
                "The compact-device skip guard cannot run without it.");
        }
    }

    /// <summary>Converts density-independent units to physical pixels.</summary>
    public int ToPixels(double dp) => (int)Math.Round(dp * Density);

    private object? Invoke(string command)
    {
        try
        {
            return _driver.ExecuteScript(command);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"'{command}' failed ({ex.GetType().Name}: {ex.Message}). This is a UiAutomator2 " +
                "command; a driver that does not support it cannot run the layout assertions.", ex);
        }
    }

    private static string Describe(object? value) => value switch
    {
        null => "null",
        Dictionary<string, object> map => "{" + string.Join(", ", map.Keys) + "}",
        _ => value.ToString() ?? value.GetType().Name,
    };
}

/// <summary>A system bar's rectangle in screen pixels, plus whether the system is showing it.</summary>
public readonly record struct SystemBarBounds(int X, int Y, int Width, int Height, bool Visible)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    /// <summary>True when the bar is docked to a side of the display rather than its bottom.</summary>
    public bool IsSideDocked => Visible && Height > Width;

    /// <summary>True when a tap at (<paramref name="x"/>, <paramref name="y"/>) lands on the bar.</summary>
    public bool Contains(int x, int y) =>
        Visible && x >= Left && x < Right && y >= Top && y < Bottom;

    public override string ToString() =>
        $"{Width}x{Height} at ({X},{Y}) — x {Left}..{Right}, y {Top}..{Bottom}, visible={Visible}";
}

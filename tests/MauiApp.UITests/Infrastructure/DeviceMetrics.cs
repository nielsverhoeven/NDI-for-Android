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

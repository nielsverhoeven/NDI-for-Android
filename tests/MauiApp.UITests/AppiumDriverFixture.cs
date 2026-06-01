using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using Xunit;

namespace MauiApp.UITests;

/// <summary>
/// xUnit async lifetime fixture that creates and manages the Appium AndroidDriver session.
/// The driver is shared across all tests in the "AppiumSession" collection.
///
/// Environment variables:
///   APPIUM_SERVER_URL  — Appium server URL (default: http://127.0.0.1:4723/)
///   ANDROID_APK_PATH   — Full path to the APK under test (required)
///
/// If ANDROID_APK_PATH is not set or the Appium server cannot be reached, every test
/// that receives this fixture will be skipped (not failed).
/// </summary>
public sealed class AppiumDriverFixture : IAsyncLifetime
{
    public AndroidDriver? Driver { get; private set; }

    /// <summary>
    /// Reason the fixture was skipped, or null when the driver is available.
    /// Tests must check this and call Skip.If / throw SkipException themselves.
    /// </summary>
    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        var apkPath = Environment.GetEnvironmentVariable("ANDROID_APK_PATH");
        if (string.IsNullOrWhiteSpace(apkPath))
        {
            SkipReason = "ANDROID_APK_PATH environment variable is not set — no emulator available.";
            return;
        }

        var serverUrlRaw = Environment.GetEnvironmentVariable("APPIUM_SERVER_URL")
                           ?? "http://127.0.0.1:4723/";

        if (!Uri.TryCreate(serverUrlRaw, UriKind.Absolute, out var serverUri))
        {
            SkipReason = $"APPIUM_SERVER_URL '{serverUrlRaw}' is not a valid URI.";
            return;
        }

        // Quick reachability check — skip instead of hanging if Appium is not running.
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var statusUri = new Uri(serverUri, "status");
            var response = await http.GetAsync(statusUri).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                SkipReason = $"Appium server at {serverUri} returned HTTP {(int)response.StatusCode}. Tests skipped.";
                return;
            }
        }
        catch (Exception ex)
        {
            SkipReason = $"Appium server at {serverUri} is not reachable ({ex.GetType().Name}: {ex.Message}). Tests skipped.";
            return;
        }

        var options = new AppiumOptions();
        options.PlatformName = "Android";
        options.AddAdditionalAppiumOption("appium:automationName", "UIAutomator2");
        options.AddAdditionalAppiumOption("appium:app", apkPath);
        options.AddAdditionalAppiumOption("appium:appPackage", "com.ndi.android");
        options.AddAdditionalAppiumOption("appium:appActivity", "com.ndi.android.MainActivity");
        options.AddAdditionalAppiumOption("appium:noReset", false);
        options.AddAdditionalAppiumOption("appium:newCommandTimeout", 60);

        try
        {
            Driver = new AndroidDriver(serverUri, options, TimeSpan.FromSeconds(120));
        }
        catch (Exception ex)
        {
            SkipReason = $"Failed to create AndroidDriver: {ex.GetType().Name}: {ex.Message}. Tests skipped.";
        }
    }

    public Task DisposeAsync()
    {
        try
        {
            Driver?.Quit();
        }
        catch
        {
            // Best-effort cleanup — ignore disposal errors.
        }

        Driver = null;
        return Task.CompletedTask;
    }
}

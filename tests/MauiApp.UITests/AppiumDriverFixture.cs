using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// xUnit async lifetime fixture that creates and manages the Appium AndroidDriver session.
/// The driver is shared across all tests in the "AppiumSession" collection.
/// </summary>
/// <remarks>
/// <para>Environment variables:</para>
/// <list type="bullet">
///   <item><c>APPIUM_SERVER_URL</c> — Appium server URL (default: http://127.0.0.1:4723/)</item>
///   <item><c>ANDROID_APK_PATH</c> — full path to the APK under test (required)</item>
///   <item><c>E2E_REQUIRE_DEVICE</c> — set to <c>true</c> in CI; see below</item>
/// </list>
/// <para>
/// <b>Two modes, deliberately.</b> On a developer machine with no emulator attached, an
/// unavailable device is not a defect — the fixture records <see cref="SkipReason"/> and the
/// tests skip. In CI a device is guaranteed, so the same condition is a real failure: with
/// <c>E2E_REQUIRE_DEVICE=true</c> the fixture throws instead, and the suite goes red.
/// </para>
/// <para>
/// That distinction is the whole point of this class. Previously every environmental failure
/// set <see cref="SkipReason"/> unconditionally, so a broken emulator turned all ten tests into
/// skips and <c>dotnet test</c> still exited 0 — the e2e gate reported success while executing
/// nothing. Keep infrastructure failures fatal under <c>E2E_REQUIRE_DEVICE</c>.
/// </para>
/// </remarks>
public sealed class AppiumDriverFixture : IAsyncLifetime
{
    public AndroidDriver? Driver { get; private set; }

    /// <summary>
    /// Reason the fixture could not produce a driver, or <c>null</c> when one is available.
    /// Always <c>null</c> when <see cref="RequireDevice"/> is set — in that mode the fixture
    /// throws rather than allowing a skip. Tests check this and call <c>Skip.If</c>.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>
    /// True when a device is mandatory and an unavailable one must fail the run (CI).
    /// </summary>
    public static bool RequireDevice =>
        string.Equals(
            Environment.GetEnvironmentVariable("E2E_REQUIRE_DEVICE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        var apkPath = Environment.GetEnvironmentVariable("ANDROID_APK_PATH");
        if (string.IsNullOrWhiteSpace(apkPath))
        {
            Unavailable("ANDROID_APK_PATH environment variable is not set — no emulator available.");
            return;
        }

        // Resolved against the test host's working directory, which is the project's output
        // directory rather than the repo root — a workspace-relative path from CI does not
        // resolve here. Appium also requires an absolute path for the `app` capability, so
        // normalise once and use the result for both.
        apkPath = Path.GetFullPath(apkPath);

        if (!File.Exists(apkPath))
        {
            Unavailable(
                $"No APK at '{apkPath}' (ANDROID_APK_PATH resolved against '{Directory.GetCurrentDirectory()}').");
            return;
        }

        var serverUrlRaw = Environment.GetEnvironmentVariable("APPIUM_SERVER_URL")
                           ?? "http://127.0.0.1:4723/";

        if (!Uri.TryCreate(serverUrlRaw, UriKind.Absolute, out var serverUri))
        {
            Unavailable($"APPIUM_SERVER_URL '{serverUrlRaw}' is not a valid URI.");
            return;
        }

        // Quick reachability check — fail fast rather than hanging on driver creation.
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var statusUri = new Uri(serverUri, "status");
            var response = await http.GetAsync(statusUri).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Unavailable($"Appium server at {serverUri} returned HTTP {(int)response.StatusCode}.");
                return;
            }
        }
        catch (Exception ex)
        {
            Unavailable($"Appium server at {serverUri} is not reachable ({ex.GetType().Name}: {ex.Message}).");
            return;
        }

        var options = new AppiumOptions();
        options.PlatformName = "Android";
        options.AutomationName = "UIAutomator2";
        options.App = apkPath;
        options.AddAdditionalAppiumOption("appium:appPackage", "com.ndi.android");
        // appActivity intentionally omitted — Appium auto-detects the launcher activity from the APK manifest.
        // The MAUI-generated activity class name (crc64... hash) changes between builds.

        // appWaitActivity "*" accepts whatever activity reaches the foreground instead of
        // waiting for the specific generated one. Without it the session fails with:
        //   'crc64<hash>.MainActivity' ... never started
        // even when the app is running fine. Two things make that mismatch likely here:
        // MainActivity requests POST_NOTIFICATIONS on API 33+, so the permission dialog is
        // briefly the foreground activity, and the crc64 name is regenerated per build.
        options.AddAdditionalAppiumOption("appium:appWaitActivity", "*");

        // MAUI cold start on a software-rendered emulator runs well past Appium's 20s default;
        // this repo's own guidance puts the cold-emulator floor at 30s.
        options.AddAdditionalAppiumOption("appium:appWaitDuration", 60000);

        // Answer the runtime permission dialog up front so it cannot sit in front of the app.
        options.AddAdditionalAppiumOption("appium:autoGrantPermissions", true);

        options.AddAdditionalAppiumOption("appium:noReset", false);
        options.AddAdditionalAppiumOption("appium:newCommandTimeout", 60);

        try
        {
            Driver = new AndroidDriver(serverUri, options, TimeSpan.FromSeconds(120));
        }
        catch (Exception ex)
        {
            Unavailable($"Failed to create AndroidDriver: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Records that no driver could be obtained: fatal when a device is required, otherwise a skip.
    /// </summary>
    private void Unavailable(string reason)
    {
        if (RequireDevice)
            throw new InvalidOperationException(
                $"E2E_REQUIRE_DEVICE is set, so the Appium session must be available. {reason}");

        SkipReason = $"{reason} Tests skipped.";
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

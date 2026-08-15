using System.Runtime.CompilerServices;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// Shared base for the UI tests: skip handling, the page-object entry point, and failure evidence.
/// </summary>
/// <remarks>
/// <para>
/// Every test body runs inside <see cref="Run"/>, which captures a screenshot, the view hierarchy
/// and the device state when the body throws (#312). Doing it here rather than per test means a
/// new test cannot forget to be diagnosable.
/// </para>
/// <para>
/// Evidence is keyed on the calling method name, supplied by the compiler — so the artifact for a
/// failure is named after the test that produced it without any test having to repeat its own
/// name as a string.
/// </para>
/// </remarks>
public abstract class UiTestBase
{
    private readonly AppiumDriverFixture _fixture;

    protected UiTestBase(AppiumDriverFixture fixture) => _fixture = fixture;

    /// <summary>The app under test. Only valid inside <see cref="Run"/>.</summary>
    protected NdiApp App { get; private set; } = null!;

    /// <summary>
    /// Skips when no device is available, then runs the test body with evidence capture.
    /// </summary>
    /// <remarks>
    /// The skip is checked here so that a device-less developer machine skips cleanly while CI —
    /// where <c>E2E_REQUIRE_DEVICE=true</c> makes the fixture throw during setup instead — cannot
    /// reach a skip at all. That distinction is what stops the suite reporting success while
    /// executing nothing.
    /// </remarks>
    protected void Run(Action<NdiApp> body, [CallerMemberName] string testName = "")
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        App = new NdiApp(driver);

        FailureEvidence.Capture(driver, testName, () => body(App));
    }
}

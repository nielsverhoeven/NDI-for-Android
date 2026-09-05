using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests;

/// <summary>
/// Regression cover for the theme defects that shipped and that the suite could not see (#313).
/// </summary>
/// <remarks>
/// <para>
/// Three defects in two days — #294 (rail icons stayed white on a light background), #300 (the
/// selected theme reverted when Settings was torn down), #296 (the rail drew under the status
/// bar) — all user-visible, all green in CI. The suite had no theme coverage whatsoever.
/// </para>
/// <para>
/// <b>Themes are set through the app's own Settings UI</b>, never by forcing
/// <c>Application.UserAppTheme</c>. Forcing it would skip the persistence path, which is exactly
/// where #300 lived.
/// </para>
/// <para>
/// <b>Colour is asserted as contrast, not as hex.</b> Two reasons. Pinning literal colours would
/// make the suite fail on every intentional palette change, which is how colour assertions get
/// deleted. More importantly, contrast is the property the user actually cares about: #294 was
/// not "the icon is #FFFFFF", it was "the icon is invisible against what is behind it". A ratio
/// catches that whatever the palette becomes.
/// </para>
/// </remarks>
[Collection("AppiumSession")]
public sealed class ThemeRegressionTests : UiTestBase
{
    /// <summary>
    /// WCAG AA for large text and graphical objects. Nav icons and their captions are the
    /// graphical-object case, so 3:1 is the correct bar rather than the 4.5:1 body-text one.
    /// </summary>
    private const double MinIconContrast = 3.0;

    private readonly ITestOutputHelper _output;

    public ThemeRegressionTests(AppiumDriverFixture fixture, ITestOutputHelper output)
        : base(fixture) => _output = output;

    [SkippableTheory]
    [InlineData(ThemeOption.Light)]
    [InlineData(ThemeOption.Dark)]
    [InlineData(ThemeOption.System)]
    public void Theme_NavigationIcons_ContrastAgainstTheirBackground(ThemeOption theme) => Run(app =>
    {
        ApplyTheme(app, theme);

        // Both placements, because they are separately implemented and #294 hit only one of them:
        // the bottom tab bar is Shell's own, natively tinted from ShellTabForegroundColor, while
        // the rail is drawn by AppShell.xaml.cs and had to be re-tinted by hand.
        foreach (var orientation in new[] { ScreenOrientation.Portrait, ScreenOrientation.Landscape })
        {
            app.Rotate(orientation);
            app.Navigation.GoTo(NavDestination.Home);
            app.Home.WaitUntilVisible();

            var placement = orientation == ScreenOrientation.Portrait ? "bottom tab bar" : "left rail";

            foreach (var destination in Enum.GetValues<NavDestination>())
            {
                var item = app.Navigation.Item(destination);

                using var screen = app.CaptureScreen();

                // Trim 15% off each edge so the sample stays inside the item and off the divider
                // and the neighbouring item's background.
                var background = screen.DominantColorOf(item, inset: 0.15);
                var foreground = screen.MostContrastingColorIn(item, background, inset: 0.15);
                var ratio = SampledColor.Contrast(foreground, background);

                _output.WriteLine(
                    $"{theme}/{placement}/{destination}: fg={foreground} bg={background} ratio={ratio:0.00}:1");

                Assert.True(ratio >= MinIconContrast,
                    $"The {destination} item in the {placement} is illegible under the {theme} theme: " +
                    $"foreground {foreground} against background {background} is only {ratio:0.00}:1, " +
                    $"below the {MinIconContrast}:1 WCAG AA bar for graphical objects. " +
                    "This is the #294 failure mode — an icon that keeps its old tint after a theme change.");
            }
        }
    });

    [SkippableFact]
    public void Theme_SelectedInSettings_SurvivesNavigatingAwayAndBack() => Run(app =>
    {
        // #300: the theme reverted to default when SettingsPage was torn down, so the defect only
        // appeared once the user left the page. Selecting and asserting without leaving — which is
        // all a naive test would do — passes against the bug.
        ApplyTheme(app, ThemeOption.Dark);

        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();
        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Appearance);

        Assert.True(app.Settings.IsThemeSelected(ThemeOption.Dark),
            "The Dark theme was applied, but after navigating away and back Settings no longer " +
            "reports it as selected. This is the #300 failure mode — the selection is lost when " +
            "the page is torn down.");
    });

    [SkippableFact]
    public void Theme_SelectedInSettings_SurvivesAnAppRestart() => Run(app =>
    {
        ApplyTheme(app, ThemeOption.Dark);

        // PersistAsync is fire-and-forget, so without a save barrier the restart below can race
        // it. Leaving Settings and coming back forces a fresh SettingsViewModel to load from the
        // repository; only once that reads back as Dark has the save actually landed.
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Appearance);

        Assert.True(app.Settings.WaitUntilThemeReads(ThemeOption.Dark),
            "The Dark theme was not read back as selected after navigating away and back, so " +
            "the restart below would not be testing persistence at all.");

        Skip.IfNot(app.TryRestart(), "App lifecycle commands are unavailable in this environment.");

        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Appearance);

        Assert.True(app.Settings.IsThemeSelected(ThemeOption.Dark),
            "The Dark theme did not survive an app restart, so it was never persisted.");
    });

    [SkippableFact]
    public void Theme_SwitchingLightToDark_ActuallyChangesWhatIsOnScreen() => Run(app =>
    {
        // Guards the whole file against becoming vacuous. Every other assertion here is a contrast
        // ratio, and a ratio stays healthy if the theme silently never changes at all — the app
        // would simply keep whatever palette it started with and pass. Comparing the two themes'
        // actual background pixels proves the switch did something.
        ApplyTheme(app, ThemeOption.Light);
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();
        var light = SampleNavBackground(app);

        ApplyTheme(app, ThemeOption.Dark);
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();
        var dark = SampleNavBackground(app);

        _output.WriteLine($"nav background: light={light} dark={dark}");

        Assert.True(light != dark,
            $"The navigation background is {light} under both the Light and Dark themes, so " +
            "switching theme changed nothing on screen. Every contrast assertion in this file is " +
            "meaningless while this is true.");

        Assert.True(light.Luminance > dark.Luminance,
            $"The Light theme's navigation background ({light}, luminance {light.Luminance:0.000}) " +
            $"is not brighter than the Dark theme's ({dark}, luminance {dark.Luminance:0.000}).");
    });

    private static SampledColor SampleNavBackground(NdiApp app)
    {
        var item = app.Navigation.Item(NavDestination.Home);
        using var screen = app.CaptureScreen();
        return screen.DominantColorOf(item, inset: 0.15);
    }

    /// <summary>
    /// Selects a theme through Settings, leaving the app on a known page. Settings auto-save, so
    /// <see cref="SettingsPage.SelectTheme"/> confirming the radio button actually toggled is the
    /// whole action — there is no separate Apply step to wait on.
    /// </summary>
    private static void ApplyTheme(NdiApp app, ThemeOption theme)
    {
        app.Rotate(ScreenOrientation.Portrait);
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();

        app.Settings.OpenSection(SettingsSection.Appearance);
        app.Settings.SelectTheme(theme);
    }
}

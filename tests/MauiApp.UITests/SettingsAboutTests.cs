using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using SettingsSection = NdiForAndroid.UITests.Pages.SettingsSection;

namespace NdiForAndroid.UITests;

/// <summary>
/// Settings → About (#457): the NDI trademark attribution and the privacy-policy link Google Play
/// requires "within the app" for a camera/microphone app.
/// </summary>
/// <remarks>
/// The trademark text is compared against the very constant the page binds
/// (<see cref="SettingsViewModel.NdiTrademarkNotice"/>), so a wording change is a deliberate
/// two-place edit, not a silently drifting assertion. The link is asserted by presence only:
/// tapping it opens a Custom Tab / browser and leaves the app under test, which the Appium session
/// cannot recover from reliably. Lives in its own file because #455 edits AppLaunchTests.cs.
/// </remarks>
[Collection("AppiumSession")]
public sealed class SettingsAboutTests : UiTestBase
{
    public SettingsAboutTests(AppiumDriverFixture fixture) : base(fixture) { }

    [RetryableSkippableFact]
    public void Settings_About_ShowsTrademarkNoticeAndPrivacyPolicyLink() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.About);

        Assert.Equal(SettingsViewModel.NdiTrademarkNotice, app.Settings.NdiTrademarkText);
        Assert.True(app.Settings.HasPrivacyPolicyLink, "The About panel is missing the 'Privacy policy' link (#457)");
    });
}

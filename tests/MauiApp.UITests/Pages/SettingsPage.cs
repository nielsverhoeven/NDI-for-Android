using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;

namespace NdiForAndroid.UITests.Pages;

/// <summary>Settings sections, addressable by name rather than by button caption.</summary>
public enum SettingsSection
{
    General,
    Appearance,
    Discovery,
    DeveloperTools,
    About,
}

/// <summary>Theme options in the Appearance section.</summary>
public enum ThemeOption
{
    Light,
    Dark,
    System,
}

/// <summary>
/// The Settings page: a section rail on the left, one visible panel on the right, Apply below.
/// </summary>
/// <remarks>
/// Section panels are all present in the tree and toggled with <c>IsVisible</c>, so "is the
/// Discovery panel showing" is a displayed check on the panel id — not a search for a caption.
/// The old tests matched section buttons on text and had to spell every caption twice to survive
/// Android's all-caps button rendering (<c>'Discovery' or 'DISCOVERY'</c>); ids make the casing
/// question disappear entirely.
/// </remarks>
public sealed class SettingsPage : PageObject
{
    public SettingsPage(AndroidDriver driver) : base(driver) { }

    protected override string PageId => TestIds.SettingsPage;
    public override string Name => "Settings";

    /// <summary>Opens a section and waits for its panel to render.</summary>
    public void OpenSection(SettingsSection section)
    {
        Tap(SectionButtonId(section), Timeouts.Navigation);
        WaitFor(PanelId(section), Timeouts.Navigation, $"The {section} panel did not open");
    }

    public bool HasSectionButton(SettingsSection section) => IsPresent(SectionButtonId(section));
    public bool IsSectionOpen(SettingsSection section)    => IsPresent(PanelId(section));

    // ── Discovery section ────────────────────────────────────────────────────

    public string DiscoveryHost
    {
        get => TextOf(TestIds.SettingsDiscoveryHost);
        set => SetText(TestIds.SettingsDiscoveryHost, value);
    }

    public string DiscoveryPort
    {
        get => TextOf(TestIds.SettingsDiscoveryPort);
        set => SetText(TestIds.SettingsDiscoveryPort, value);
    }

    public string ValidationError => TextOf(TestIds.SettingsDiscoveryServersError);

    // ── Appearance section ───────────────────────────────────────────────────

    public void SelectTheme(ThemeOption theme) => Tap(ThemeId(theme));

    public bool IsThemeSelected(ThemeOption theme) =>
        string.Equals(WaitFor(ThemeId(theme)).GetAttribute("checked"), "true", StringComparison.OrdinalIgnoreCase);

    // ── Apply ────────────────────────────────────────────────────────────────

    public void Apply() => Tap(TestIds.SettingsApply);

    /// <summary>Waits for the "Settings applied." confirmation to appear.</summary>
    public void WaitForApplied() =>
        WaitFor(TestIds.SettingsAppliedNotice, Timeouts.Element, "Settings were not confirmed as applied");

    public bool IsApplied => IsPresent(TestIds.SettingsAppliedNotice);

    private static string SectionButtonId(SettingsSection section) => section switch
    {
        SettingsSection.General        => TestIds.SettingsSectionGeneral,
        SettingsSection.Appearance     => TestIds.SettingsSectionAppearance,
        SettingsSection.Discovery      => TestIds.SettingsSectionDiscovery,
        SettingsSection.DeveloperTools => TestIds.SettingsSectionDeveloperTools,
        SettingsSection.About          => TestIds.SettingsSectionAbout,
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };

    private static string PanelId(SettingsSection section) => section switch
    {
        SettingsSection.General        => TestIds.SettingsPanelGeneral,
        SettingsSection.Appearance     => TestIds.SettingsPanelAppearance,
        SettingsSection.Discovery      => TestIds.SettingsPanelDiscovery,
        SettingsSection.DeveloperTools => TestIds.SettingsPanelDeveloperTools,
        SettingsSection.About          => TestIds.SettingsPanelAbout,
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };

    private static string ThemeId(ThemeOption theme) => theme switch
    {
        ThemeOption.Light  => TestIds.SettingsThemeLight,
        ThemeOption.Dark   => TestIds.SettingsThemeDark,
        ThemeOption.System => TestIds.SettingsThemeSystem,
        _ => throw new ArgumentOutOfRangeException(nameof(theme)),
    };
}

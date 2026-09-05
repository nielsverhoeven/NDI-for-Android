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
/// The Settings page: a section rail on the left, one visible panel on the right. Every change
/// auto-saves; there is no Apply step.
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

    /// <summary>Fills the add-server form and taps Add Server.</summary>
    public void AddServer(string host, string port = "")
    {
        DiscoveryHost = host;
        DiscoveryPort = port;
        Tap(TestIds.SettingsDiscoveryServerAction, Timeouts.Navigation);
    }

    /// <summary>Endpoint text ("host:port") of every rendered server row, in list order.</summary>
    public IReadOnlyList<string> ServerRowEndpoints =>
        FindDisplayed(TestIds.SettingsServerRowEndpoint).Select(row => row.Text).ToList();

    /// <summary>Deletes the row whose endpoint matches <paramref name="endpoint"/>, if one is rendered.</summary>
    public void RemoveServer(string endpoint)
    {
        var index = FindDisplayed(TestIds.SettingsServerRowEndpoint)
            .Select(row => row.Text)
            .ToList()
            .IndexOf(endpoint);

        if (index < 0)
            return;

        FindDisplayed(TestIds.SettingsServerRowDelete)[index].Click();
    }

    // ── Appearance section ───────────────────────────────────────────────────

    /// <summary>
    /// Selects a theme and confirms the selection actually took.
    /// </summary>
    /// <remarks>
    /// The confirmation is not ceremony. These radio buttons use a MAUI <c>ControlTemplate</c>
    /// rather than the native Android control, so the automation id sits on a container and a tap
    /// on it does not necessarily toggle anything. Without this check, a tap that silently does
    /// nothing surfaces later as a theme assertion failure elsewhere in the test, which reads as
    /// an unrelated defect rather than a selection that never happened.
    /// </remarks>
    public void SelectTheme(ThemeOption theme) =>
        LastThemeTapStrategy = TapUntilSet(ThemeId(theme), () => IsThemeSelected(theme));

    /// <summary>
    /// How the last theme selection actually landed.
    /// </summary>
    /// <remarks>
    /// Surfaced so a run records which input path these templated radio buttons respond to. If it
    /// ever reads "direct tap" the template has changed and the fallbacks can go; if it changes
    /// between runs, the control is timing-sensitive and that is worth knowing before it becomes
    /// an intermittent failure.
    /// </remarks>
    public string LastThemeTapStrategy { get; private set; } = "(none)";

    public bool IsThemeSelected(ThemeOption theme) =>
        string.Equals(CheckedState(theme), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Polls until the selection reads back as <paramref name="theme"/>, or the timeout elapses.
    /// </summary>
    /// <remarks>
    /// SettingsViewModel is transient and reloads from the repository asynchronously in
    /// OnAppearing, so the panel can render before that load has populated the selection — a
    /// single read right after <see cref="OpenSection"/> can catch that default value instead of
    /// what was actually persisted.
    /// </remarks>
    public bool WaitUntilThemeReads(ThemeOption theme, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Timeouts.Navigation);
        while (DateTime.UtcNow < deadline)
        {
            if (IsThemeSelected(theme))
                return true;

            Thread.Sleep(250);
        }

        return IsThemeSelected(theme);
    }

    private string CheckedState(ThemeOption theme) =>
        WaitFor(ThemeId(theme)).GetAttribute("checked") ?? "(no checked attribute)";

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

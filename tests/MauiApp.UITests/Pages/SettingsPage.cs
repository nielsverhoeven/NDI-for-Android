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

    /// <summary>
    /// Selects a theme.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately unverified at the point of the tap.</b> An earlier version confirmed the
    /// selection by reading the node's <c>checked</c> attribute and retried through a ladder of tap
    /// strategies until it turned true. A diagnostic run settled that this could never work: with
    /// no tap at all, and the app necessarily on some theme, all three options report
    /// <c>checked='false'</c>. The attribute is simply not carried on the
    /// <c>android.view.ViewGroup</c> that holds the automation id — these radio buttons use a MAUI
    /// <c>ControlTemplate</c> rather than the native control, because the repo's theming rules
    /// require it. So the ladder was reading a constant, exhausting every strategy and throwing
    /// even on runs where the tap had worked perfectly.
    /// </para>
    /// <para>
    /// Both centre taps are issued because the container reports <c>clickable=false</c> and has no
    /// clickable descendant, so which of the two paths reaches the handler is not knowable from the
    /// tree. Sending both is safe: re-selecting an already-selected radio button is a no-op.
    /// </para>
    /// <para>
    /// The selection is instead verified by its effect — see the pixel assertions in
    /// <c>ThemeRegressionTests</c>. That is a stronger check than <c>checked</c> ever was: it
    /// proves the theme reached the screen, not merely that a control changed state.
    /// </para>
    /// </remarks>
    public void SelectTheme(ThemeOption theme)
    {
        var element = WaitFor(ThemeId(theme));
        element.Click();
        TapAtCentre(element);
    }

    /// <summary>
    /// Describes each theme option node as the accessibility tree sees it.
    /// </summary>
    /// <remarks>
    /// Diagnostic, not an assertion. Kept because it is what established that <c>checked</c> is
    /// never true here, and it is how a future run would notice the template gaining real
    /// checkable semantics — at which point selection could be asserted directly again.
    /// </remarks>
    public IReadOnlyList<string> DescribeThemeOptionNodes()
    {
        var lines = new List<string>();

        foreach (var theme in Enum.GetValues<ThemeOption>())
        {
            try
            {
                var element = WaitFor(ThemeId(theme));
                lines.Add(
                    $"{ThemeId(theme)}: checked='{element.GetAttribute("checked") ?? "(absent)"}' " +
                    $"class={element.GetAttribute("class")} " +
                    $"clickable={element.GetAttribute("clickable")} " +
                    $"focusable={element.GetAttribute("focusable")} " +
                    $"text='{element.Text}' " +
                    $"desc='{element.GetAttribute("content-desc")}' " +
                    $"size={element.Size.Width}x{element.Size.Height}");
            }
            catch (Exception ex)
            {
                lines.Add($"{ThemeId(theme)}: could not read — {ex.GetType().Name}: {ex.Message}");
            }
        }

        return lines;
    }

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

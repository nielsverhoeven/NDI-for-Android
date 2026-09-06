using OpenQA.Selenium;
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

    /// <summary>Number of discovery server rows currently rendered.</summary>
    public int ServerRowCount => FindDisplayed(TestIds.SettingsServerRowDelete).Count;

    /// <summary>
    /// Deletes rows from the bottom of the list until only <paramref name="baselineCount"/> remain.
    /// </summary>
    /// <remarks>
    /// Cleanup by endpoint text depends on the very row locator a layout overflow can make
    /// disappear, silently no-oping and leaving a bogus server persisted. Counting rows instead
    /// does not depend on the row's content rendering at all.
    /// </remarks>
    public void RemoveServersDownTo(int baselineCount)
    {
        const int maxAttempts = 20;

        for (var attempt = 0; ServerRowCount > baselineCount; attempt++)
        {
            if (attempt >= maxAttempts)
                throw new InvalidOperationException(
                    $"Could not bring the discovery server list down to {baselineCount} row(s) — " +
                    $"still {ServerRowCount} after {maxAttempts} deletes.");

            FindDisplayed(TestIds.SettingsServerRowDelete)[^1].Click();
        }
    }

    /// <summary>
    /// Rendered size, in pixels, of the last row's <paramref name="controlId"/> — one of the
    /// <c>settings.serverRow.*</c> ids — or <see cref="System.Drawing.Size.Empty"/> if it is not
    /// currently displayed.
    /// </summary>
    /// <remarks>
    /// A row template that overflows its container does not shrink its controls to zero and keep
    /// them findable — Android drops a zero-area node from the accessibility tree entirely, so
    /// "not displayed" and "displayed with no area" are the same observable failure here.
    /// </remarks>
    public System.Drawing.Size LastServerRowControlSize(string controlId) =>
        FindDisplayed(controlId).LastOrDefault()?.Size ?? System.Drawing.Size.Empty;

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
        LastThemeTapStrategy = TapUntilSet(ThemeId(theme), () => IsThemeSelected(theme), DescribeThemeState);

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

    public bool IsThemeSelected(ThemeOption theme) => SelectedTheme == theme;

    /// <summary>
    /// Which theme currently reads as selected, read from the RadioButton templates' own pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Android <c>checked</c> accessibility attribute is structurally unavailable here: MAUI's
    /// default (Content-based) RadioButton <c>ControlTemplate</c> exposes no checkable or
    /// clickable node anywhere in its subtree, so every <c>settings.theme.*</c> node reports
    /// <c>checked="false"</c> regardless of actual selection. Pixels are the only ground truth.
    /// </para>
    /// <para>
    /// Each theme row renders exactly two <c>android.view.View</c> children — an outer ring and an
    /// inner check-mark dot — and only the selected row's dot is filled; the other two read as the
    /// panel background. Grouping the three sampled colours and requiring exactly one to differ
    /// from the other two (rather than testing each dot against a fixed threshold) is
    /// threshold-free and palette-independent, and carries its own negative control: a broken
    /// read-back that always reports "selected" fails the "exactly one differs" invariant instead
    /// of silently returning a plausible-looking answer.
    /// </para>
    /// </remarks>
    public ThemeOption SelectedTheme
    {
        get
        {
            using var screen = ScreenSampler.Capture(Driver);

            var samples = Enum.GetValues<ThemeOption>()
                .ToDictionary(t => t, t => screen.DominantColorOf(ThemeMarker(t)));

            var oddOnesOut = samples
                .GroupBy(kv => kv.Value, kv => kv.Key)
                .Where(g => g.Count() == 1)
                .ToList();

            if (samples.Values.Distinct().Count() == 2 && oddOnesOut.Count == 1)
                return oddOnesOut[0].Single();

            throw new InvalidOperationException(
                "Could not read back the selected theme: expected exactly one marker colour to " +
                "differ from the other two (2 alike, 1 different), but sampled " +
                string.Join(", ", samples.Select(kv => $"{kv.Key}={kv.Value}")) + ".");
        }
    }

    /// <summary>
    /// Polls until the selection reads back as <paramref name="theme"/>, or the timeout elapses.
    /// </summary>
    /// <remarks>
    /// SettingsViewModel is transient and reloads from the repository asynchronously in
    /// OnAppearing, so the panel can render before that load has populated the selection — a
    /// single read right after <see cref="OpenSection"/> can catch that default value instead of
    /// what was actually persisted. <see cref="SelectedTheme"/> can also throw mid-transition
    /// (a VSM fade can briefly leave no marker clearly the odd one out); that is tolerated while
    /// polling but propagates from the final read, so an actual infrastructure failure still
    /// surfaces as one.
    /// </remarks>
    public bool WaitUntilThemeReads(ThemeOption theme, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? Timeouts.Navigation);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (IsThemeSelected(theme))
                    return true;
            }
            catch
            {
                // Tolerated — see remarks. The unguarded read below is what surfaces a real failure.
            }

            Thread.Sleep(250);
        }

        return IsThemeSelected(theme);
    }

    /// <summary>The inner check-mark dot for a theme's RadioButton template.</summary>
    private IWebElement ThemeMarker(ThemeOption theme)
    {
        var id = ThemeId(theme);
        var markers = Driver.FindElements(By.XPath(
            $"//*[@resource-id='{NdiApp.PackageName}:id/{id}']//android.view.View"));

        if (markers.Count < 2)
            throw new InvalidOperationException(
                $"'{id}' has only {markers.Count} 'android.view.View' descendant(s); expected " +
                "the outer ring and the inner check-mark dot. The RadioButton ControlTemplate " +
                "has changed.");

        return markers[^1];
    }

    private string DescribeThemeState()
    {
        try
        {
            return $"SelectedTheme={SelectedTheme}";
        }
        catch (Exception ex)
        {
            return $"SelectedTheme threw {ex.GetType().Name}: {ex.Message}";
        }
    }

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

using System.Reflection;

namespace NdiForAndroid.Testing;

/// <summary>
/// Stable automation identifiers for every element the UI tests drive.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this class exists.</b> Before it, the e2e suite had zero <c>AutomationId</c> across 51
/// interactive elements, so every locator was an XPath over visible text. Copy is not an API: a
/// wording change silently broke tests, and a duplicated string silently matched the wrong node.
/// That is not hypothetical — a locator matching <c>@text='Home'</c> hit the Shell top app bar
/// title instead of the navigation item, which made one test fail against a perfectly good app
/// and another pass on evidence it never actually checked.
/// </para>
/// <para>
/// <b>Why it lives in Core rather than in the test project.</b> Both the XAML (via
/// <c>{x:Static}</c>) and the page objects reference these same constants, so renaming one is a
/// compile error in both places rather than a test that quietly stops finding anything. A
/// test-project-local copy would restore exactly the drift this is meant to remove.
/// </para>
/// <para>
/// <b>Why the members are flat rather than grouped into nested classes.</b> Nested types are the
/// more readable shape, but referencing one from XAML needs <c>{x:Static t:TestIds+Home.Page}</c>,
/// and nested-type resolution in <c>x:Static</c> is not reliably supported across XAML compilers.
/// This project's Android head cannot be built without the MAUI workload, so that syntax would
/// only be validated in CI — a bad trade for a change that touches every view. The name prefix
/// carries the grouping instead.
/// </para>
/// <para>
/// <b>On Android</b> MAUI maps <c>AutomationId</c> to the view's <c>resource-id</c>, so these
/// values are matched with <c>By.Id</c> — no XPath, no text. The values are stable API: change one
/// only when the element itself is genuinely replaced, never to match a new caption.
/// </para>
/// </remarks>
public static class TestIds
{
    /// <summary>Every declared id value.</summary>
    /// <remarks>
    /// Read by reflection so it cannot drift from the declarations below — a hand-maintained list
    /// would be one more thing to forget when adding an id. Used by the accessibility audit to
    /// catch an element announcing its automation id as its screen-reader label, which is
    /// non-empty and therefore passes a naive "has a description" check while telling a user
    /// nothing.
    /// </remarks>
    public static IReadOnlySet<string> All { get; } =
        typeof(TestIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    // ── Shell chrome: the four primary navigation destinations ───────────────
    //
    // The bottom tab bar and the landscape left rail are different visual treatments of the same
    // destination, and only one of them is in the tree at a time — so both carry the same id. A
    // test asking for NavHome gets whichever placement is live, which is what lets the adaptive
    // navigation tests assert on position without also encoding which control they hit.

    public const string NavHome     = "nav.home";
    public const string NavStream   = "nav.stream";
    public const string NavView     = "nav.view";
    public const string NavSettings = "nav.settings";

    // ── HomePage: status cards and quick actions ─────────────────────────────

    public const string HomePage                = "home.page";
    public const string HomeDiscoveryStatusCard = "home.discoveryStatusCard";
    public const string HomeDiscoveryStatus     = "home.discoveryStatus";
    public const string HomeSourceCount         = "home.sourceCount";
    public const string HomeLastRefresh         = "home.lastRefresh";
    public const string HomeViewerStatusCard    = "home.viewerStatusCard";
    public const string HomeViewerStatus        = "home.viewerStatus";
    public const string HomeOutputStatusCard    = "home.outputStatusCard";
    public const string HomeOutputStatus        = "home.outputStatus";
    public const string HomeStartViewingLast    = "home.startViewingLast";
    public const string HomeResumeOutput        = "home.resumeOutput";

    // ── SourceListPage: the discovered-source list and its row actions ───────
    //
    // Row ids repeat for every row, deliberately: they identify the *kind* of control. Tests
    // select a specific row by index or by the row's own name label.

    public const string SourcesPage        = "sources.page";
    public const string SourcesList        = "sources.list";
    public const string SourcesRefresh     = "sources.refresh";
    public const string SourcesViewerPane  = "sources.viewerPane";
    public const string SourceRowName      = "sources.row.name";
    public const string SourceRowEndpoint  = "sources.row.endpoint";
    public const string SourceRowWatch     = "sources.row.watch";
    public const string SourceRowOutput    = "sources.row.output";

    // ── ViewerView: video surface, quality, audio, PTZ, reconnect ────────────
    //
    // ViewerView is embedded twice — as the whole of ViewerPage, and as the Expanded-window pane
    // inside SourceListPage — so these ids are not unique across the tree when the pane is
    // visible. Page objects scope to the containing page.

    public const string ViewerPage            = "viewer.page";
    public const string ViewerVideoCanvas     = "viewer.videoCanvas";
    public const string ViewerVideoBorder     = "viewer.videoBorder";
    public const string ViewerStatus          = "viewer.status";
    public const string ViewerQualitySmooth   = "viewer.quality.smooth";
    public const string ViewerQualityBalanced = "viewer.quality.balanced";
    public const string ViewerQualityHigh     = "viewer.quality.high";
    public const string ViewerAudioToggle     = "viewer.audioToggle";
    public const string ViewerPtzUp           = "viewer.ptz.up";
    public const string ViewerPtzDown         = "viewer.ptz.down";
    public const string ViewerPtzLeft         = "viewer.ptz.left";
    public const string ViewerPtzRight        = "viewer.ptz.right";
    public const string ViewerPtzAutoFocus    = "viewer.ptz.autoFocus";
    public const string ViewerPtzZoomIn       = "viewer.ptz.zoomIn";
    public const string ViewerPtzZoomOut      = "viewer.ptz.zoomOut";
    public const string ViewerRetryStatus     = "viewer.retryStatus";
    public const string ViewerCancelRetry     = "viewer.cancelRetry";
    public const string ViewerReconnect       = "viewer.reconnect";
    public const string ViewerStop            = "viewer.stop";

    // ── OutputPage (Stream tab): send configuration and start/stop ───────────

    public const string OutputPage             = "output.page";
    public const string OutputModeToggle       = "output.modeToggle";
    public const string OutputStreamName       = "output.streamName";
    public const string OutputVideoInput       = "output.videoInput";
    public const string OutputMicrophoneToggle = "output.microphoneToggle";
    public const string OutputReStreamSourceId = "output.reStreamSourceId";
    public const string OutputConnectionCount  = "output.connectionCount";
    public const string OutputOnAirTally       = "output.onAirTally";
    public const string OutputStatus           = "output.status";
    public const string OutputStart            = "output.start";
    public const string OutputStop             = "output.stop";

    // ── SettingsPage: section rail, each section's controls, and Apply ───────

    public const string SettingsPage = "settings.page";

    public const string SettingsSectionGeneral        = "settings.section.general";
    public const string SettingsSectionAppearance     = "settings.section.appearance";
    public const string SettingsSectionDiscovery      = "settings.section.discovery";
    public const string SettingsSectionDeveloperTools = "settings.section.developerTools";
    public const string SettingsSectionAbout          = "settings.section.about";

    public const string SettingsPanelGeneral        = "settings.panel.general";
    public const string SettingsPanelAppearance     = "settings.panel.appearance";
    public const string SettingsPanelDiscovery      = "settings.panel.discovery";
    public const string SettingsPanelDeveloperTools = "settings.panel.developerTools";
    public const string SettingsPanelAbout          = "settings.panel.about";

    public const string SettingsThemeLight  = "settings.theme.light";
    public const string SettingsThemeDark   = "settings.theme.dark";
    public const string SettingsThemeSystem = "settings.theme.system";

    public const string SettingsAccentBlue   = "settings.accent.blue";
    public const string SettingsAccentTeal   = "settings.accent.teal";
    public const string SettingsAccentGreen  = "settings.accent.green";
    public const string SettingsAccentOrange = "settings.accent.orange";
    public const string SettingsAccentRed    = "settings.accent.red";
    public const string SettingsAccentPink   = "settings.accent.pink";

    public const string SettingsDiscoveryHost           = "settings.discoveryHost";
    public const string SettingsDiscoveryPort           = "settings.discoveryPort";
    public const string SettingsDiscoveryServerEndpoint = "settings.discoveryServerEndpoint";
    public const string SettingsDiscoveryServerAction   = "settings.discoveryServerAction";
    public const string SettingsDiscoveryServersError   = "settings.discoveryServersError";
    public const string SettingsDiscoveryServerList     = "settings.discoveryServerList";
    public const string SettingsServerRowEndpoint       = "settings.serverRow.endpoint";
    public const string SettingsServerRowEnabled        = "settings.serverRow.enabled";
    public const string SettingsServerRowUp             = "settings.serverRow.up";
    public const string SettingsServerRowDown           = "settings.serverRow.down";
    public const string SettingsServerRowEdit           = "settings.serverRow.edit";
    public const string SettingsServerRowDelete         = "settings.serverRow.delete";

    public const string SettingsDeveloperModeToggle = "settings.developerModeToggle";
    public const string SettingsCachedSourceList    = "settings.cachedSourceList";

    public const string SettingsAppName       = "settings.appName";
    public const string SettingsAppVersion    = "settings.appVersion";
    public const string SettingsNdiSdkVersion = "settings.ndiSdkVersion";

    public const string SettingsValidationError = "settings.validationError";
    public const string SettingsApply           = "settings.apply";
    public const string SettingsAppliedNotice   = "settings.appliedNotice";

    // ── DiagnosticLogPage: the in-memory log buffer view ─────────────────────

    public const string DiagnosticLogPage     = "diagnosticLog.page";
    public const string DiagnosticLogList     = "diagnosticLog.list";
    public const string DiagnosticLogClear    = "diagnosticLog.clear";
    public const string DiagnosticLogRowLevel = "diagnosticLog.row.level";
    public const string DiagnosticLogRowTime  = "diagnosticLog.row.time";
    public const string DiagnosticLogRowText  = "diagnosticLog.row.message";
}

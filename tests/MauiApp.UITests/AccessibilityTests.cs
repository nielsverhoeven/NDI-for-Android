using OpenQA.Selenium;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests;

/// <summary>
/// Accessibility as a build gate rather than something found by accident (#314).
/// </summary>
/// <remarks>
/// <para>
/// The left rail shipped with no accessibility label on any of its four items — TalkBack users
/// could not identify them — and it was found only because an Appium locator happened to need a
/// <c>content-desc</c>. Nothing in the suite looked, so nothing noticed.
/// </para>
/// <para>
/// <b>The gate is a ratchet, not a cliff.</b> It fails when the violation count exceeds
/// <see cref="Budget"/>, which tracks the number of violations the app has today (currently 12,
/// against a measured 10). Failing outright on every pre-existing violation would have meant
/// either shipping a red pipeline or fixing the entire back catalogue inside a test PR; neither
/// is honest. What this does buy immediately is that a *new* violation goes red. Lower the
/// budget as violations are fixed — never raise it to make a red run green.
/// </para>
/// <para>
/// The budget is overridable via <c>A11Y_MAX_VIOLATIONS</c> so it can be tightened in CI without
/// a code change.
/// </para>
/// </remarks>
[Collection("AppiumSession")]
public sealed class AccessibilityTests : UiTestBase
{
    /// <summary>
    /// Maximum tolerated violations across the audited screens.
    /// </summary>
    /// <remarks>
    /// Ratcheted to the measured count, exactly as <c>COVERAGE_MIN</c> was. Dispatched run
    /// 34155799008 on this branch measured 10 violations — 8 structural missing-label findings on
    /// <c>RecyclerView</c>/<c>ScrollView</c> containers and 2 touch-target findings on the Output
    /// tab's Entry/Picker — so the default is set to 12 (a small margin above the measured count,
    /// not the provisional 200 this started at, which let the app grow ~10x its violations before
    /// the gate noticed). Lower it further as violations are fixed; never raise it to make a red
    /// run green.
    /// </remarks>
    private static int Budget =>
        int.TryParse(Environment.GetEnvironmentVariable("A11Y_MAX_VIOLATIONS"), out var configured)
            ? configured
            : 12;

    private readonly ITestOutputHelper _output;

    public AccessibilityTests(AppiumDriverFixture fixture, ITestOutputHelper output)
        : base(fixture) => _output = output;

    [RetryableSkippableFact]
    public void Accessibility_AcrossPrimaryScreens_StaysWithinBudget() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Portrait);

        var violations = new List<A11yViolation>();

        foreach (var (destination, screen) in PrimaryScreens)
        {
            app.Navigation.GoTo(destination);
            WaitForScreen(app, destination);

            violations.AddRange(app.Accessibility.Run(screen, TestIds.All));
        }

        // Settings lands on General, so the four-screen walk above never saw the Discovery
        // panel — which is how its add-server Entries went unmeasured until #467 and had to be
        // carried as a documented exception. The walk ends on Settings, so each extra section is
        // one tap. Appearance rides along because it is just as cheap and carries the #465
        // RadioButtons, whose 48 dp also comes from an implicit style.
        foreach (var (section, screen) in AuditedSettingsSections)
        {
            app.Settings.OpenSection(section);
            violations.AddRange(app.Accessibility.Run(screen, TestIds.All));
        }

        violations.AddRange(AuditEditServerDialog(app));

        var summary = AccessibilityAudit.Summarise(violations, Budget);

        // Three destinations, because each answers a different need. xUnit output attaches it to
        // the test; stdout puts it in the job log; the file lets run-emulator-tests.sh echo it at
        // the very end of the run, which is the only place a reader reliably looks — a summary
        // buried mid-log among hundreds of lines of dotnet output may as well not exist, and the
        // count is what tells us where to set the budget next.
        _output.WriteLine(summary);
        Console.WriteLine(summary);
        WriteSummaryArtifact(summary);

        Assert.True(violations.Count <= Budget,
            $"{violations.Count} accessibility violations, above the budget of {Budget}. " +
            $"Lower the budget as these are fixed; never raise it.{Environment.NewLine}{summary}");
    });

    [RetryableSkippableFact]
    public void Accessibility_NavigationItems_AnnounceTheirDestination() => Run(app =>
    {
        // The specific regression from #304: every rail item was an unlabelled container. This is
        // the narrow, non-ratcheted assertion — navigation labels are not allowed to regress at
        // all, budget or no budget.
        foreach (var orientation in new[] { ScreenOrientation.Portrait, ScreenOrientation.Landscape })
        {
            app.Rotate(orientation);
            app.Navigation.GoTo(NavDestination.Home);
            app.Home.WaitUntilVisible();

            var placement = orientation == ScreenOrientation.Portrait ? "bottom tab bar" : "left rail";

            foreach (var destination in Enum.GetValues<NavDestination>())
            {
                var element = app.Navigation.Item(destination);
                var description = element.GetAttribute("content-desc") ?? string.Empty;
                var text = element.Text ?? string.Empty;
                var announced = !string.IsNullOrWhiteSpace(description) ? description : text;

                _output.WriteLine($"{placement}/{destination}: announced as '{announced}'");

                Assert.False(string.IsNullOrWhiteSpace(announced),
                    $"The {destination} item in the {placement} announces nothing to a screen " +
                    "reader. This is the pre-#304 rail failure mode.");

                Assert.False(TestIds.All.Contains(announced),
                    $"The {destination} item in the {placement} announces '{announced}', which is " +
                    "an automation id rather than human language. A test hook is not an " +
                    "accessibility label.");
            }
        }
    });

    [RetryableSkippableFact]
    public void Accessibility_RailItems_AnnounceSelectedDestination() => Run(app =>
    {
        // The rail only exists in landscape/Expanded; the bottom tab bar (portrait) is untouched
        // by this change and carries its own native selected state (#345 home-nav-06).
        app.Rotate(ScreenOrientation.Landscape);

        try
        {
            app.Navigation.GoTo(NavDestination.Home);
            app.Home.WaitUntilVisible();
            Assert.True(app.Navigation.AnnouncesSelected(NavDestination.Home));
            Assert.False(app.Navigation.AnnouncesSelected(NavDestination.Settings));

            app.Navigation.GoTo(NavDestination.Settings);
            app.Settings.WaitUntilVisible();
            Assert.True(app.Navigation.AnnouncesSelected(NavDestination.Settings));
            Assert.False(app.Navigation.AnnouncesSelected(NavDestination.Home));
        }
        finally
        {
            // Other tests in this suite assume portrait.
            app.Rotate(ScreenOrientation.Portrait);
        }
    });

    [RetryableSkippableFact]
    public void Accessibility_AutomationIds_AreNotUsedAsScreenReaderLabels() => Run(app =>
    {
        // Phase 1 added 99 AutomationIds. If MAUI were to surface those as contentDescription,
        // the app would now announce "home.startViewingLast" to TalkBack users — a regression
        // introduced by the very work that made the suite testable. This asserts it did not, and
        // reports what is actually in the tree either way so the answer is on the record rather
        // than assumed.
        app.Rotate(ScreenOrientation.Portrait);

        var offenders = new List<string>();

        foreach (var (destination, screen) in PrimaryScreens)
        {
            app.Navigation.GoTo(destination);
            WaitForScreen(app, destination);

            foreach (var node in app.Accessibility.ReadTree().Where(n => n.Displayed))
            {
                if (!string.IsNullOrEmpty(node.ContentDescription) &&
                    TestIds.All.Contains(node.ContentDescription))
                {
                    offenders.Add($"{screen}: {node.Describe()} announces '{node.ContentDescription}'");
                }
            }
        }

        _output.WriteLine(offenders.Count == 0
            ? "No element announces an automation id — AutomationId and contentDescription are distinct."
            : $"{offenders.Count} element(s) announce an automation id:{Environment.NewLine}  " +
              string.Join($"{Environment.NewLine}  ", offenders));

        Assert.True(offenders.Count == 0,
            $"{offenders.Count} element(s) expose an automation id as their screen-reader " +
            $"announcement:{Environment.NewLine}  " +
            string.Join($"{Environment.NewLine}  ", offenders.Take(20)) +
            $"{Environment.NewLine}Give them a SemanticProperties.Description in human language.");
    });

    /// <summary>
    /// Audits the Settings edit-server dialog (#467), which only exists once a server row does.
    /// </summary>
    /// <remarks>
    /// The dialog is a modal over a scrim, not a native alert, so the Discovery panel beneath it
    /// is still in the accessibility tree; the audit is scoped to the dialog card's bounds so
    /// those findings are not counted a second time. The row is created and removed here with the
    /// same helpers the Discovery lifecycle tests use (a non-routable host and an unusual port keep
    /// cleanup unambiguous), and the dialog is always closed again so later audits start clean.
    /// Opens the Discovery section itself; the caller may leave Settings on any section.
    /// </remarks>
    private static IReadOnlyList<A11yViolation> AuditEditServerDialog(NdiApp app)
    {
        const string host = "10.255.255.4";
        const string port = "45962";

        app.Settings.OpenSection(SettingsSection.Discovery);
        var baseline = app.Settings.ServerRowCount;
        app.Settings.AddServer(host, port);

        try
        {
            app.Settings.WaitForServerRow($"{host}:{port}");
            app.Settings.EditServer($"{host}:{port}");

            var dialog = app.Settings.EditServerDialogBounds;
            var violations = app.Accessibility.Run("Settings/EditServer", TestIds.All, dialog);

            app.Settings.CancelEditServer();
            return violations;
        }
        finally
        {
            if (app.Settings.IsEditServerDialogOpen)
                app.Settings.CancelEditServer();

            app.Settings.RemoveServersDownTo(baseline);
        }
    }

    /// <summary>Best-effort: a summary that cannot be written must not fail the audit.</summary>
    private static void WriteSummaryArtifact(string summary)
    {
        try
        {
            Directory.CreateDirectory(FailureEvidence.ArtifactDirectory);
            File.WriteAllText(
                Path.Combine(FailureEvidence.ArtifactDirectory, "accessibility-summary.txt"),
                summary);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[a11y] could not write the summary artifact: {ex.Message}");
        }
    }

    private static readonly (NavDestination Destination, string Screen)[] PrimaryScreens =
    [
        (NavDestination.Home,     "Home"),
        (NavDestination.Stream,   "Output"),
        (NavDestination.View,     "Sources"),
        (NavDestination.Settings, "Settings"),
    ];

    /// <summary>
    /// Settings sections audited in addition to the General landing section, in the order they
    /// are opened. Only the budget test walks these; the other audits keep the four-screen
    /// <see cref="PrimaryScreens"/> walk.
    /// </summary>
    private static readonly (SettingsSection Section, string Screen)[] AuditedSettingsSections =
    [
        (SettingsSection.Discovery,  "Settings/Discovery"),
        (SettingsSection.Appearance, "Settings/Appearance"),
    ];

    private static void WaitForScreen(NdiApp app, NavDestination destination)
    {
        switch (destination)
        {
            case NavDestination.Home:     app.Home.WaitUntilVisible();     break;
            case NavDestination.Stream:   app.Output.WaitUntilVisible();   break;
            case NavDestination.View:     app.Sources.WaitUntilVisible();  break;
            case NavDestination.Settings: app.Settings.WaitUntilVisible(); break;
        }
    }
}

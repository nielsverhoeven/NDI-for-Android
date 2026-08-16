using System.Text;
using OpenQA.Selenium;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests;

/// <summary>
/// Answers the two questions holding up Phase 2, without asserting anything about them.
/// </summary>
/// <remarks>
/// <para>
/// Two Phase 2 failures each have two possible explanations, and the difference decides whether
/// the fix belongs in the app or in the tests:
/// </para>
/// <list type="number">
///   <item>
///     The theme radio buttons never report <c>checked=true</c> after a tap. Either users genuinely
///     cannot select a theme, or <c>checked</c> is simply not carried on the node holding the
///     automation id, making the test's verification invalid.
///   </item>
///   <item>
///     Tapping a navigation item in landscape loses the app to the launcher. Either rail navigation
///     exits the app, or something else removes it around that moment.
///   </item>
/// </list>
/// <para>
/// <b>This class asserts nothing and cannot fail the build.</b> That is deliberate: its output is
/// evidence, and a diagnostic that fails would be indistinguishable from the defects it is meant to
/// characterise. Findings go to stdout and to <c>phase2-diagnostics.txt</c> in the artifact
/// directory, which the run script echoes at the end of the log.
/// </para>
/// </remarks>
[Collection("AppiumSession")]
public sealed class PhaseTwoDiagnostics : UiTestBase
{
    private readonly ITestOutputHelper _output;

    public PhaseTwoDiagnostics(AppiumDriverFixture fixture, ITestOutputHelper output)
        : base(fixture) => _output = output;

    [SkippableFact]
    public void Diagnose_ThemeSelectionAndRailNavigation() => Run(app =>
    {
        var report = new StringBuilder()
            .AppendLine("═══ Phase 2 diagnostics ═══════════════════════════════");

        SafeSection(report, "Q1 — is 'checked' even exposed on the theme radio buttons?",
            () => DiagnoseThemeCheckedAttribute(app, report));

        SafeSection(report, "Q2 — does applying a theme change what is on screen?",
            () => DiagnoseThemeActuallyApplies(app, report));

        SafeSection(report, "Q3 — does tapping a rail item in landscape remove the app?",
            () => DiagnoseRailNavigation(app, report));

        report.AppendLine("═══════════════════════════════════════════════════════");

        var text = report.ToString();
        _output.WriteLine(text);
        Console.WriteLine(text);
        WriteArtifact(text);
    });

    /// <summary>
    /// The zero-tap half of Q1, and the decisive one.
    /// </summary>
    /// <remarks>
    /// The app always has a theme, so exactly one of these three radio buttons is selected at any
    /// moment. If the tree reports <c>checked=false</c> for <b>all three</b>, the attribute is not
    /// carrying state on the node that holds the automation id — which means the test's
    /// verification was reading the wrong thing and the app is not implicated at all. No tap is
    /// needed to establish that, so nothing about the tap can confound it.
    /// </remarks>
    private void DiagnoseThemeCheckedAttribute(NdiApp app, StringBuilder report)
    {
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Appearance);

        var states = app.Settings.DescribeThemeOptionNodes();
        foreach (var line in states)
            report.AppendLine($"    {line}");

        var anyChecked = states.Any(s => s.Contains("checked='true'", StringComparison.Ordinal));

        report.AppendLine()
              .AppendLine(anyChecked
                  ? "  VERDICT: 'checked' IS exposed — exactly one option reports true, so the "
                    + "attribute is a valid signal and a tap that leaves it false really did not select."
                  : "  VERDICT: 'checked' is NOT exposed — no option reports true even though the app "
                    + "always has a theme selected. The test's verification was invalid; this is a "
                    + "TEST defect, not a product one.");
    }

    /// <summary>
    /// Q2 — an observable that does not depend on the <c>checked</c> attribute at all.
    /// </summary>
    /// <remarks>
    /// Applies Light then Dark and compares the navigation background pixels. If the two differ,
    /// theme selection works end to end whatever the accessibility tree says about it.
    /// </remarks>
    private void DiagnoseThemeActuallyApplies(NdiApp app, StringBuilder report)
    {
        var light = ApplyAndSampleNavBackground(app, ThemeOption.Light, report);
        var dark  = ApplyAndSampleNavBackground(app, ThemeOption.Dark,  report);

        if (light is null || dark is null)
        {
            report.AppendLine("  VERDICT: inconclusive — could not sample both themes (see above).");
            return;
        }

        report.AppendLine()
              .AppendLine($"    light nav background = {light}, dark = {dark}")
              .AppendLine(light != dark
                  ? "  VERDICT: the theme DOES apply — the two themes paint different pixels, so "
                    + "selection works end to end regardless of what 'checked' reports."
                  : "  VERDICT: the theme does NOT apply — both themes paint identical pixels. "
                    + "Selection genuinely is not taking effect; this is a PRODUCT defect.");
    }

    private SampledColor? ApplyAndSampleNavBackground(NdiApp app, ThemeOption theme, StringBuilder report)
    {
        try
        {
            app.Navigation.GoTo(NavDestination.Settings);
            app.Settings.WaitUntilVisible();
            app.Settings.OpenSection(SettingsSection.Appearance);

            // Best effort: the tap ladder may legitimately fail, which is itself the finding.
            try
            {
                app.Settings.SelectTheme(theme);
                report.AppendLine($"    {theme}: selected via {app.Settings.LastThemeTapStrategy}");
            }
            catch (Exception ex)
            {
                report.AppendLine($"    {theme}: SelectTheme threw — {ex.Message}");
            }

            app.Settings.Apply();
            report.AppendLine($"    {theme}: applied notice visible = {app.Settings.IsApplied}");

            app.Navigation.GoTo(NavDestination.Home);
            app.Home.WaitUntilVisible();

            var item = app.Navigation.Item(NavDestination.Home);
            using var screen = app.CaptureScreen();
            return screen.DominantColorOf(item, inset: 0.15);
        }
        catch (Exception ex)
        {
            report.AppendLine($"    {theme}: could not sample — {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Q3 — tap a rail item in landscape and report whether the app is still on screen.
    /// </summary>
    private void DiagnoseRailNavigation(NdiApp app, StringBuilder report)
    {
        app.Rotate(ScreenOrientation.Landscape);
        report.AppendLine($"    after rotating to landscape: app in foreground = {app.IsInForeground}");

        if (!app.IsInForeground)
        {
            report.AppendLine("  VERDICT: the app was already gone after rotation — the rotation, "
                              + "not the tap, is what removes it.");
            return;
        }

        // Deliberately not app.Navigation.GoTo: that throws its own guard exception, which would
        // abort this section before it can report. Tap and observe instead.
        var item = app.Navigation.Item(NavDestination.Stream);
        report.AppendLine($"    rail item resolved via {app.Navigation.LastResolution}");
        item.Click();
        Thread.Sleep(1500);

        var stillHere = app.IsInForeground;
        report.AppendLine($"    after tapping the Stream rail item: app in foreground = {stillHere}")
              .AppendLine($"    foreground package = '{app.ForegroundPackage}'");

        report.AppendLine()
              .AppendLine(stillHere
                  ? "  VERDICT: rail navigation does NOT remove the app — whatever caused the "
                    + "earlier failures happens elsewhere."
                  : "  VERDICT: tapping a rail item DOES remove the app. Landscape navigation exits "
                    + "the app — a PRODUCT defect, and one a user would hit on any tablet.");
    }

    /// <summary>
    /// Runs a section, recording any exception as a finding rather than letting it end the run.
    /// </summary>
    /// <remarks>
    /// A diagnostic that aborts on its first surprise answers fewer questions per CI cycle than one
    /// that keeps going, and each cycle here costs about ten minutes.
    /// </remarks>
    private static void SafeSection(StringBuilder report, string title, Action section)
    {
        report.AppendLine().AppendLine(title);

        try
        {
            section();
        }
        catch (Exception ex)
        {
            report.AppendLine($"  ABORTED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void WriteArtifact(string text)
    {
        try
        {
            Directory.CreateDirectory(FailureEvidence.ArtifactDirectory);
            File.WriteAllText(
                Path.Combine(FailureEvidence.ArtifactDirectory, "phase2-diagnostics.txt"), text);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[phase2-diagnostics] could not write artifact: {ex.Message}");
        }
    }
}

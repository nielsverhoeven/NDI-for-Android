using System.Text;
using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests;

/// <summary>
/// Answers the question still holding up Phase 2, without asserting anything about it.
/// </summary>
/// <remarks>
/// <para>
/// Two questions have already been settled here and removed. The theme radio buttons report
/// <c>checked='false'</c> on all three options with no tap at all, even though the app is always on
/// some theme — so that attribute was never a valid signal, and the tests that read it have been
/// rewritten to verify the theme by the pixels it paints. Applying Light then Dark produced
/// <c>#E5E5EA</c> then <c>#1C1C1E</c>, so theme selection works end to end and the app was never
/// implicated.
/// </para>
/// <para>
/// What remains is the navigation failure. Tapping <b>Stream</b> in the landscape rail left the app
/// on screen, which looked like an acquittal — but both failing tests tap <b>Home</b> while Home is
/// already showing, and only their landscape halves fail. So the live candidate is re-selecting the
/// destination already loaded, not rail navigation as such.
/// </para>
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
    public void Diagnose_ReselectingTheCurrentNavigationDestination() => Run(app =>
    {
        var report = new StringBuilder()
            .AppendLine("═══ Phase 2 diagnostics ═══════════════════════════════");

        SafeSection(report, "Q4 — does re-tapping the destination already showing remove the app?",
            () => DiagnoseReselectingCurrentDestination(app, report));

        report.AppendLine("═══════════════════════════════════════════════════════");

        var text = report.ToString();
        _output.WriteLine(text);
        Console.WriteLine(text);
        WriteArtifact(text);
    });

    /// <summary>
    /// Q4 — the discriminator Q3's answer demands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Q3 tapped <b>Stream</b> while Home was showing and the app survived, which appeared to clear
    /// rail navigation entirely. But both failing tests tap <b>Home</b> while Home is already
    /// showing, and both fail only in landscape — the portrait half of each passes. So the
    /// candidate is not "rail navigation" but "re-selecting the destination already on screen",
    /// which <c>AppShell.OnRailItemSelected</c> turns into an unconditional
    /// <c>GoToAsync("//home-rail")</c> back to the route already loaded.
    /// </para>
    /// <para>
    /// Three taps separate the two explanations. A fresh destination must survive (it did in Q3
    /// and is the control here), re-tapping that same destination is the suspect, and doing it on
    /// something other than Home rules out Home being special.
    /// </para>
    /// </remarks>
    private void DiagnoseReselectingCurrentDestination(NdiApp app, StringBuilder report)
    {
        app.Rotate(ScreenOrientation.Landscape);
        report.AppendLine($"    after rotating to landscape: app in foreground = {app.IsInForeground}");

        if (!app.IsInForeground)
        {
            report.AppendLine("  VERDICT: the app was already gone after rotation — the rotation, "
                              + "not any tap, is what removes it.");
            return;
        }

        var freshSurvived    = TapAndReport(app, report, NavDestination.Stream, "fresh destination");
        var reselectSurvived = freshSurvived
            ? TapAndReport(app, report, NavDestination.Stream, "SAME destination again")
            : (bool?)null;
        var reselectHome     = reselectSurvived == true
            ? TapAndReport(app, report, NavDestination.Home, "fresh destination (Home)")
              && TapAndReport(app, report, NavDestination.Home, "SAME destination again (Home)")
            : (bool?)null;

        report.AppendLine();

        if (!freshSurvived)
        {
            report.AppendLine("  VERDICT: even a fresh destination removes the app, contradicting Q3. "
                              + "Rail navigation is unsafe in general, not just on re-selection.");
            return;
        }

        if (reselectSurvived == false)
        {
            report.AppendLine("  VERDICT: re-tapping the destination already showing DOES remove the "
                              + "app, while a fresh one does not. That is a PRODUCT defect in "
                              + "AppShell.OnRailItemSelected, which navigates to the current route "
                              + "unconditionally. A user re-tapping the tab they are on exits the app.");
            return;
        }

        if (reselectHome == false)
        {
            report.AppendLine("  VERDICT: Stream survives re-selection but Home does not, so the "
                              + "defect is specific to the Home route rather than to re-selection.");
            return;
        }

        report.AppendLine("  VERDICT: nothing removed the app — neither a fresh tap nor a "
                          + "re-selection, on Stream or Home. The two failing tests must be losing "
                          + "the app to something outside NavigationBar.GoTo.");
    }

    /// <summary>
    /// Taps a navigation item directly and reports whether the app survived.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>app.Navigation.GoTo</c>: that throws its own guard exception, which
    /// would abort the section before it could report the rest of the sequence.
    /// </remarks>
    private static bool TapAndReport(NdiApp app, StringBuilder report, NavDestination destination, string role)
    {
        try
        {
            var item = app.Navigation.Item(destination);
            report.AppendLine($"    tapping {destination} in the rail ({role}), resolved via {app.Navigation.LastResolution}");
            item.Click();
        }
        catch (Exception ex)
        {
            report.AppendLine($"    tapping {destination} ({role}) threw — {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        Thread.Sleep(1500);

        var stillHere = app.IsInForeground;
        report.AppendLine($"      → app in foreground = {stillHere}, foreground package = '{app.ForegroundPackage}'");
        return stillHere;
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

using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.Testing;

namespace NdiForAndroid.UITests.Pages;

/// <summary>The in-memory diagnostic log buffer (last 200 entries).</summary>
public sealed class DiagnosticLogPage : PageObject
{
    public DiagnosticLogPage(AndroidDriver driver) : base(driver) { }

    protected override string PageId => TestIds.DiagnosticLogPage;
    public override string Name => "Diagnostic log";

    /// <summary>Rendered entry count. The list virtualises, so this is what is on screen.</summary>
    public int EntryCount => FindDisplayed(TestIds.DiagnosticLogRowText).Count;

    /// <summary>Messages of the rendered entries, in list order.</summary>
    public IReadOnlyList<string> Messages =>
        FindDisplayed(TestIds.DiagnosticLogRowText).Select(e => e.Text).ToList();

    public void ClearLog() => Tap(TestIds.DiagnosticLogClear);
}

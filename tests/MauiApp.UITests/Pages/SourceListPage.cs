using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.Testing;

namespace NdiForAndroid.UITests.Pages;

/// <summary>The View tab: discovered NDI sources, each with Watch and Output actions.</summary>
/// <remarks>
/// Row ids repeat once per row, so row access is by index into the displayed set. On CI this list
/// is empty — the emulator has no NDI sources on its network — which is a genuine state of the
/// page, not a failure; callers check <see cref="SourceCount"/> before reaching for a row.
/// </remarks>
public sealed class SourceListPage : PageObject
{
    public SourceListPage(AndroidDriver driver) : base(driver) { }

    protected override string PageId => TestIds.SourcesPage;
    public override string Name => "Source list";

    /// <summary>Number of source rows currently rendered.</summary>
    public int SourceCount => FindDisplayed(TestIds.SourceRowName).Count;

    /// <summary>Display names of the rendered rows, in list order.</summary>
    public IReadOnlyList<string> SourceNames =>
        FindDisplayed(TestIds.SourceRowName).Select(e => e.Text).ToList();

    /// <summary>Taps Watch on the row at <paramref name="index"/>.</summary>
    public void WatchSource(int index = 0) => TapRowAction(TestIds.SourceRowWatch, index, "Watch");

    /// <summary>Taps Output on the row at <paramref name="index"/>.</summary>
    public void OutputSource(int index = 0) => TapRowAction(TestIds.SourceRowOutput, index, "Output");

    /// <summary>True when the Expanded-window embedded viewer pane is showing.</summary>
    public bool IsViewerPaneVisible => IsPresent(TestIds.SourcesViewerPane);

    private void TapRowAction(string id, int index, string action)
    {
        var buttons = FindDisplayed(id);

        if (index >= buttons.Count)
            throw new InvalidOperationException(
                $"Cannot tap {action} on source row {index}: only {buttons.Count} row(s) are rendered. " +
                "On CI the emulator discovers no NDI sources, so the list shows its empty view.");

        buttons[index].Click();
    }
}

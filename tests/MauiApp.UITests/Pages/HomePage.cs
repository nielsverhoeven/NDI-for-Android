using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.Testing;

namespace NdiForAndroid.UITests.Pages;

/// <summary>The Home tab: discovery, viewer and output status, plus two quick actions.</summary>
public sealed class HomePage : PageObject
{
    public HomePage(AndroidDriver driver) : base(driver) { }

    protected override string PageId => TestIds.HomePage;
    public override string Name => "Home";

    public string DiscoveryStatus => TextOf(TestIds.HomeDiscoveryStatus);
    public string ViewerStatus    => TextOf(TestIds.HomeViewerStatus);
    public string OutputStatus    => TextOf(TestIds.HomeOutputStatus);
    public string SourceCountText => TextOf(TestIds.HomeSourceCount);

    /// <summary>Number of discovered sources, parsed from the "Sources found: {n}" label.</summary>
    /// <remarks>
    /// Returns <c>null</c> rather than throwing or defaulting to 0 when the label does not carry
    /// a number — "no sources" and "the label is not what we think it is" are different failures
    /// and a caller should be able to tell them apart.
    /// </remarks>
    public int? SourceCount
    {
        get
        {
            var digits = new string(SourceCountText.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var count) ? count : null;
        }
    }

    public bool HasDiscoveryCard => IsPresent(TestIds.HomeDiscoveryStatusCard);
    public bool HasViewerCard    => IsPresent(TestIds.HomeViewerStatusCard);
    public bool HasOutputCard    => IsPresent(TestIds.HomeOutputStatusCard);

    public void StartViewingLastSource() => Tap(TestIds.HomeStartViewingLast);
    public void ResumeOutput()           => Tap(TestIds.HomeResumeOutput);
}

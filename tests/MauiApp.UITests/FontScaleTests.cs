using System.Drawing;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

[Collection("AppiumSession")]
public sealed class FontScaleTests : UiTestBase
{
    public FontScaleTests(AppiumDriverFixture fixture) : base(fixture) { }

    [RetryableSkippableFact]
    public void FontScale_HomePage_NoClippedOrOverlappingTextAndPrimaryActionReachable() => Run(app =>
    {
        app.ResetToHome();
        AssertScreenIsReadable(app, "Home");
        app.Home.ScrollToBottom();
        AssertReachable(app, TestIds.HomeStartViewingLast, "Home start-viewing-last action");
    });

    [RetryableSkippableFact]
    public void FontScale_SettingsSections_NoClippedOrOverlappingTextAndSectionsReachable() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        AssertScreenIsReadable(app, "Settings");

        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            var minPx = app.Metrics.ToPixels(AccessibilityAudit.MinTouchTargetDp);
            var height = app.Settings.SectionButtonHeightPx(section);
            Assert.True(height >= minPx,
                $"{section} section button is {height}px tall at font scale, below the " +
                $"{AccessibilityAudit.MinTouchTargetDp}dp ({minPx}px) minimum.");
        }
    });

    [RetryableSkippableFact]
    public void FontScale_OutputPage_NoClippedOrOverlappingTextAndStartActionReachable() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();
        AssertScreenIsReadable(app, "Output");
        app.Output.ScrollToBottom();
        AssertReachable(app, TestIds.OutputStart, "Output start action");
    });

    /// <summary>Screenshot, clipping and sibling-overlap checks for every visible text node.</summary>
    /// <remarks>
    /// Only horizontal clipping is checked, against nodes that intersect the screen. Settings and
    /// Output are inside a ScrollView (OutputPage.xaml, SettingsPage.xaml), so a node's top/bottom
    /// can legitimately sit outside the screen while scrolled; the window itself never scrolls
    /// sideways, so horizontal clipping is the only bound a font-scale defect can violate.
    /// </remarks>
    private static void AssertScreenIsReadable(NdiApp app, string screenName)
    {
        var window = app.WindowSize;
        var screen = new Rectangle(0, 0, window.Width, window.Height);

        SaveScreenshot(app, screenName);

        var textNodes = app.Accessibility.ReadTree()
            .Where(n => n.Displayed && !string.IsNullOrWhiteSpace(n.Text))
            .Select(n => (n.Text, Bounds: new Rectangle(n.X, n.Y, n.Width, n.Height)))
            .Where(n => n.Bounds.IntersectsWith(screen))
            .ToList();

        foreach (var (text, bounds) in textNodes)
        {
            Assert.True(bounds.Width > 0 && bounds.Height > 0,
                $"{screenName}: text '{text}' has zero area at the current font scale.");
            Assert.True(bounds.Left >= screen.Left && bounds.Right <= screen.Right,
                $"{screenName}: text '{text}' at {bounds} is clipped horizontally by the screen " +
                $"bounds {screen} at the current font scale.");
        }

        for (var i = 0; i < textNodes.Count; i++)
        {
            for (var j = i + 1; j < textNodes.Count; j++)
            {
                var a = textNodes[i];
                var b = textNodes[j];
                var overlaps = a.Bounds.IntersectsWith(b.Bounds)
                    && !a.Bounds.Contains(b.Bounds)
                    && !b.Bounds.Contains(a.Bounds);

                Assert.False(overlaps,
                    $"{screenName}: '{a.Text}' {a.Bounds} overlaps '{b.Text}' {b.Bounds} at the " +
                    "current font scale.");
            }
        }
    }

    /// <remarks>
    /// The caller must scroll the target into view first (see <c>HomePage.ScrollToBottom</c> /
    /// <c>OutputPage.ScrollToBottom</c>): Appium clips the reported bounds of a partially
    /// scrolled-out element, and omits a fully off-screen one, so an un-scrolled call here reports
    /// a spurious red rather than a genuine reachability defect.
    /// </remarks>
    private static void AssertReachable(NdiApp app, string testId, string description)
    {
        var minPx = app.Metrics.ToPixels(AccessibilityAudit.MinTouchTargetDp);
        var node = app.Accessibility.ReadTree()
            .FirstOrDefault(n => n.Displayed && n.ResourceId.EndsWith($":id/{testId}", StringComparison.Ordinal));

        Assert.True(node is not null, $"{description} ('{testId}') is not on screen at the current font scale.");
        Assert.True(node!.Width >= minPx && node.Height >= minPx,
            $"{description} is {node.Width}x{node.Height}px at the current font scale, below the " +
            $"{AccessibilityAudit.MinTouchTargetDp}dp ({minPx}px) minimum.");
    }

    private static void SaveScreenshot(NdiApp app, string screenName)
    {
        try
        {
            var fontScale = Environment.GetEnvironmentVariable("E2E_FONT_SCALE") ?? "1.0";
            var dir = FailureEvidence.ArtifactDirectory;
            Directory.CreateDirectory(dir);
            using var screen = app.CaptureScreen();
            screen.SaveTo(Path.Combine(dir, $"font-scale-{fontScale}-{screenName}.png"));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FontScaleTests] could not save screenshot: {ex.Message}");
        }
    }
}

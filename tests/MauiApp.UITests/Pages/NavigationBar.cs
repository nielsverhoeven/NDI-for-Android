using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;

namespace NdiForAndroid.UITests.Pages;

/// <summary>The four primary destinations, in whichever placement is currently live.</summary>
public enum NavDestination
{
    Home,
    Stream,
    View,
    Settings,
}

/// <summary>
/// The app's primary navigation — the bottom tab bar in portrait, the left rail in landscape.
/// </summary>
/// <remarks>
/// <para>
/// Chrome rather than a page, so it does not extend <see cref="PageObject"/>: it is present on
/// every screen and has no "am I visible" identity of its own.
/// </para>
/// <para>
/// <b>Why lookup is not purely by id.</b> Everywhere else in this suite, an element is found by
/// its automation id and nothing else. Navigation is the one documented exception. The left rail
/// is a <c>Border</c> built in <c>AppShell.xaml.cs</c>, so its <c>AutomationId</c> reaches the
/// Android view tree as a <c>resource-id</c> normally. The bottom bar is not ours — Shell renders
/// it with a native <c>BottomNavigationView</c>, and an <c>AutomationId</c> set on
/// <c>ShellContent</c> is not reliably propagated to the rendered tab. What the tab does always
/// carry is its accessibility label.
/// </para>
/// <para>
/// So this resolves by id first and falls back to <c>content-desc</c>. The fallback is not a
/// papered-over flake: an accessibility label is a real, user-facing contract — it is what
/// TalkBack announces — so a test that depends on it is testing something the app owes its users
/// either way. What the fallback must never become is a way to keep passing while ids disappear,
/// which is why <see cref="LastResolution"/> records which path was taken.
/// </para>
/// <para>
/// Matching on visible text is what the old locator did, and is specifically excluded here.
/// Shell renders the current page's title in the top app bar, so while Home is showing the string
/// "Home" appears three times in the tree — the title, the tab container, and the tab's caption.
/// Only the navigation item carries an accessibility label, which is why text matching picked the
/// title and reported the Home nav element at y=135 on a 2560px screen.
/// </para>
/// </remarks>
public sealed class NavigationBar
{
    private readonly AndroidDriver _driver;

    public NavigationBar(AndroidDriver driver) => _driver = driver;

    /// <summary>How the most recent lookup succeeded — for assertions about the id contract.</summary>
    public enum Resolution
    {
        None,
        ById,
        ByAccessibilityLabel,
    }

    /// <summary>Which path resolved the last successful lookup.</summary>
    public Resolution LastResolution { get; private set; } = Resolution.None;

    /// <summary>Taps a destination and waits for the tap target to exist first.</summary>
    /// <remarks>
    /// Confirms the app is still on screen afterwards. Tapping a navigation item is not supposed
    /// to be able to remove the app, so if it does, that is the single most important fact about
    /// the run — and without this check it surfaces one call later as "the page did not appear",
    /// which points at the destination rather than at the tap that left.
    /// </remarks>
    public void GoTo(NavDestination destination)
    {
        var placement = IsRailPlacement() ? "left rail" : "bottom tab bar";

        Item(destination, Timeouts.Navigation).Click();

        // Give the transition a moment before judging: navigation is asynchronous, and sampling
        // the tree mid-swap would produce a false accusation.
        Thread.Sleep(750);

        if (AppOwnsAnythingOnScreen())
            return;

        // One blank reading is not proof the app is gone, and this guard used to treat it as
        // exactly that. The logcat for a failing run tells against it: every foreground event is
        // a start of our own package, the launcher is never started, and there is no
        // moveTaskToBack, finishActivity or removeTask anywhere. Nothing handed the screen to
        // anyone, so "the app was removed" was an inference from a single sample rather than an
        // observation — and 750ms is not obviously longer than a Shell page swap.
        var deadline = DateTime.UtcNow + Timeouts.Element;
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(250);

            if (AppOwnsAnythingOnScreen())
            {
                BlankTreeRecoveries++;
                return;
            }
        }

        throw new InvalidOperationException(
            $"Tapping the {destination} item in the {placement} left nothing owned by " +
            $"'{NdiApp.PackageName}' in the view tree, and it had still not returned " +
            $"{Timeouts.Element.TotalSeconds:0}s later.{Environment.NewLine}" +
            $"Packages owning nodes on screen: {DescribeOwners()}.{Environment.NewLine}" +
            FailureEvidence.DescribeVisibleIds(_driver));
    }

    /// <summary>
    /// How often the view tree went briefly blank and came back during this session.
    /// </summary>
    /// <remarks>
    /// Deliberately counted rather than ignored. If this stays zero, blank readings really are
    /// permanent and the app genuinely leaves. If it climbs, the suite has been reporting a
    /// transient mid-navigation tree as "the app exited" — which is a very different bug, and one
    /// living in the tests rather than the app.
    /// </remarks>
    public static int BlankTreeRecoveries { get; private set; }

    /// <summary>Which packages own nodes on screen right now, with node counts.</summary>
    private string DescribeOwners()
    {
        try
        {
            var owners = _driver
                .FindElements(By.XPath("//*[@package]"))
                .Select(e =>
                {
                    try { return e.GetAttribute("package") ?? "(none)"; }
                    catch (StaleElementReferenceException) { return "(stale)"; }
                })
                .GroupBy(p => p, StringComparer.Ordinal)
                .Select(g => $"{g.Key} x{g.Count()}")
                .ToList();

            return owners.Count == 0 ? "(the tree is empty)" : string.Join(", ", owners);
        }
        catch (Exception ex)
        {
            return $"(could not enumerate: {ex.GetType().Name}: {ex.Message})";
        }
    }

    /// <summary>True when the landscape left rail is the live placement.</summary>
    private bool IsRailPlacement()
    {
        try
        {
            return _driver.Orientation == ScreenOrientation.Landscape;
        }
        catch
        {
            return false;
        }
    }

    private bool AppOwnsAnythingOnScreen()
    {
        try
        {
            return _driver
                .FindElements(By.XPath($"//*[starts-with(@resource-id, '{NdiApp.PackageName}:')]"))
                .Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// The live navigation element for <paramref name="destination"/> — bottom tab or rail item,
    /// whichever the current window size class is showing.
    /// </summary>
    public IWebElement Item(NavDestination destination, TimeSpan? timeout = null)
    {
        var budget = timeout ?? Timeouts.Navigation;
        var id = IdFor(destination);
        var label = LabelFor(destination);
        var wait = new WebDriverWait(_driver, budget);

        try
        {
            return wait.Until(_ => Resolve(id, label))!;
        }
        catch (WebDriverTimeoutException)
        {
            throw new WebDriverTimeoutException(
                $"No navigation item for {destination} after {budget.TotalSeconds:0}s " +
                $"(id '{id}', accessibility label '{label}').{Environment.NewLine}" +
                Describe(label));
        }
    }

    /// <summary>True when the destination's navigation item is on screen.</summary>
    public bool IsPresent(NavDestination destination) =>
        Resolve(IdFor(destination), LabelFor(destination)) is not null;

    private IWebElement? Resolve(string id, string label)
    {
        try
        {
            var byId = _driver.FindElements(By.Id(id)).FirstOrDefault(Displayed);
            if (byId is not null)
            {
                LastResolution = Resolution.ById;
                return byId;
            }

            var byLabel = _driver
                .FindElements(By.XPath($"//*[@content-desc='{label}']"))
                .FirstOrDefault(Displayed);

            if (byLabel is not null)
            {
                LastResolution = Resolution.ByAccessibilityLabel;
                return byLabel;
            }
        }
        catch (StaleElementReferenceException)
        {
            // Mid-transition rebuild — the caller's wait will poll again.
        }

        return null;
    }

    /// <summary>
    /// Lists everything carrying the label and how each node is classified.
    /// </summary>
    /// <remarks>
    /// This dump is what settled the locator question after two wrong hypotheses: it showed every
    /// candidate reporting <c>clickable=false</c>, which killed the theory that interactivity
    /// could be used as the discriminator, and showed exactly one node carrying a
    /// <c>content-desc</c>.
    /// </remarks>
    private string Describe(string label)
    {
        var report = new StringBuilder("Nodes matching the label:");

        try
        {
            var candidates = _driver.FindElements(
                By.XPath($"//*[@content-desc='{label}' or contains(@content-desc,'{label}') or @text='{label}']"));

            if (candidates.Count == 0)
                report.AppendLine().Append("  (none — the label is not in the tree at all)");

            foreach (var candidate in candidates)
            {
                try
                {
                    report.AppendLine().Append(
                        $"  class={candidate.GetAttribute("class")} " +
                        $"id='{candidate.GetAttribute("resource-id")}' " +
                        $"text='{candidate.Text}' " +
                        $"desc='{candidate.GetAttribute("content-desc")}' " +
                        $"displayed={candidate.Displayed} " +
                        $"at={candidate.Location.X},{candidate.Location.Y} " +
                        $"size={candidate.Size.Width}x{candidate.Size.Height}");
                }
                catch (StaleElementReferenceException)
                {
                    // Skip the node that moved; the rest still describe the tree.
                }
            }
        }
        catch (Exception ex)
        {
            report.AppendLine().Append($"  (could not enumerate: {ex.GetType().Name}: {ex.Message})");
        }

        return report.ToString();
    }

    private static bool Displayed(IWebElement element)
    {
        try
        {
            return element.Displayed;
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    private static string IdFor(NavDestination destination) => destination switch
    {
        NavDestination.Home     => TestIds.NavHome,
        NavDestination.Stream   => TestIds.NavStream,
        NavDestination.View     => TestIds.NavView,
        NavDestination.Settings => TestIds.NavSettings,
        _ => throw new ArgumentOutOfRangeException(nameof(destination)),
    };

    /// <summary>
    /// The accessibility label Shell and the rail expose for each destination.
    /// </summary>
    /// <remarks>
    /// These match the <c>Label</c> values in <c>PrimaryNavigationMetadata</c> and the
    /// <c>ShellContent.Title</c> values in <c>AppShell.xaml</c> — which is the contract TalkBack
    /// reads, so it is deliberately the user-visible string here rather than an internal id.
    /// </remarks>
    private static string LabelFor(NavDestination destination) => destination switch
    {
        NavDestination.Home     => "Home",
        NavDestination.Stream   => "Stream",
        NavDestination.View     => "View",
        NavDestination.Settings => "Settings",
        _ => throw new ArgumentOutOfRangeException(nameof(destination)),
    };
}

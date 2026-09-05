using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using NdiForAndroid.UITests.Infrastructure;

namespace NdiForAndroid.UITests.Pages;

/// <summary>
/// Base for every page object: the one place that knows how to find and wait for an element.
/// </summary>
/// <remarks>
/// <para>
/// Test methods must not contain locators. Before this layer existed, raw XPath was inlined and
/// duplicated across test methods, so a change to one screen meant hunting every string that
/// might have matched it — and a locator that silently matched the wrong node (the Shell page
/// title rather than the nav item) was invisible at the call site.
/// </para>
/// <para>
/// Everything here resolves by automation id, which MAUI maps to Android's <c>resource-id</c>.
/// Ids are declared once in <see cref="NdiForAndroid.Testing.TestIds"/> and shared with the app
/// itself, so a rename breaks the build rather than the suite.
/// </para>
/// </remarks>
public abstract class PageObject
{
    protected AndroidDriver Driver { get; }

    protected PageObject(AndroidDriver driver) => Driver = driver;

    /// <summary>Id of the element proving this page is the one on screen.</summary>
    protected abstract string PageId { get; }

    /// <summary>Human name used in failure messages.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Blocks until this page is rendered. Returns itself so callers can chain.
    /// </summary>
    public void WaitUntilVisible(TimeSpan? timeout = null) =>
        WaitFor(PageId, timeout ?? Timeouts.Navigation, $"{Name} did not become visible");

    /// <summary>True when this page is currently on screen. Does not wait.</summary>
    public bool IsVisible => FindAll(PageId).Any(IsDisplayed);

    // ── Element access ───────────────────────────────────────────────────────

    /// <summary>
    /// Waits for the element with <paramref name="id"/> to be displayed and returns it.
    /// </summary>
    /// <exception cref="WebDriverTimeoutException">
    /// Thrown with the ids that <i>were</i> on screen attached. A bare "timed out" says nothing
    /// about whether the app was on the wrong page, still loading, or genuinely missing the
    /// element; the id dump distinguishes all three without another run.
    /// </exception>
    protected IWebElement WaitFor(string id, TimeSpan? timeout = null, string? because = null)
    {
        var budget = timeout ?? Timeouts.Element;
        var wait = new WebDriverWait(Driver, budget);

        try
        {
            return wait.Until(_ =>
            {
                try
                {
                    return FindAll(id).FirstOrDefault(IsDisplayed);
                }
                catch (StaleElementReferenceException)
                {
                    // The tree changed mid-scan — normal during a transition. Retry next poll.
                    return null;
                }
            })!;
        }
        catch (WebDriverTimeoutException)
        {
            var reason = because ?? $"No displayed element with id '{id}'";

            // Distinguish "the page is wrong" from "the app is gone". They look identical from
            // here — both are just a missing element — but they are different bugs, and reporting
            // the first when it is really the second sent several rounds of investigation at the
            // page instead of at what removed the app.
            if (!OwnsAnythingOnScreen())
                throw new WebDriverTimeoutException(
                    $"The app is no longer on screen, so {Name} could never appear. Nothing under " +
                    $"'{NdiApp.PackageName}:id/' is in the view tree — the app was killed, crashed, " +
                    $"or was sent to the background during this test.{Environment.NewLine}" +
                    FailureEvidence.DescribeVisibleIds(Driver));

            throw new WebDriverTimeoutException(
                $"{reason} on {Name} after {budget.TotalSeconds:0}s.{Environment.NewLine}" +
                FailureEvidence.DescribeVisibleIds(Driver));
        }
    }

    /// <summary>Taps the element with <paramref name="id"/> once it is displayed.</summary>
    protected void Tap(string id, TimeSpan? timeout = null) => WaitFor(id, timeout).Click();

    /// <summary>
    /// Toggles a checkable control, trying the ways a tap can reach it until one takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain <c>Click()</c> on the element is not enough for this app's radio buttons. They use
    /// a MAUI <c>ControlTemplate</c> rather than the native Android control — the repo's theming
    /// rules require it, because <c>MaterialRadioButton</c> ignores <c>DynamicResource</c> — so
    /// the automation id lands on a container while the node that actually responds to touch is
    /// somewhere else in the subtree. Tapping the container reported <c>checked='false'</c>
    /// afterwards, every time.
    /// </para>
    /// <para>
    /// Rather than guess which node is the right one, this tries the plausible targets in order
    /// and stops at the first that changes the state. The strategy that worked is returned so the
    /// caller can report it: if the direct tap ever starts working, that tells us the template
    /// changed, and if the list is ever exhausted the failure names everything that was tried
    /// instead of just saying the control did not respond.
    /// </para>
    /// </remarks>
    /// <param name="id">Automation id of the control.</param>
    /// <param name="isSet">Reads the control's current state.</param>
    /// <returns>Name of the strategy that worked.</returns>
    protected string TapUntilSet(string id, Func<bool> isSet)
    {
        var element = WaitFor(id);

        if (isSet())
            return "already set";

        // 1. The element itself — correct for a native control, and the cheapest.
        element.Click();
        if (isSet())
            return "direct tap";

        // 2. The nearest clickable node at or below the id. A templated control puts the touch
        //    handler on an inner view, so this is the one most likely to work here.
        foreach (var descendant in FindClickableWithin(id))
        {
            descendant.Click();
            if (isSet())
                return "tap on clickable descendant";
        }

        // 3. The element's centre as a raw pointer gesture. Bypasses the view tree entirely, so
        //    it works when the handler is on a node the tree does not expose as clickable.
        TapAtCentre(element);
        if (isSet())
            return "pointer tap at centre";

        throw new InvalidOperationException(
            $"'{id}' did not change state after a direct tap, a tap on each clickable node " +
            $"beneath it ({FindClickableWithin(id).Count} tried), and a pointer tap at its " +
            "centre. The control is not responding to synthetic input at all.");
    }

    /// <summary>True when any view in the tree belongs to our package.</summary>
    private bool OwnsAnythingOnScreen()
    {
        try
        {
            return Driver
                .FindElements(By.XPath($"//*[starts-with(@resource-id, '{NdiApp.PackageName}:')]"))
                .Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private IReadOnlyList<IWebElement> FindClickableWithin(string id)
    {
        try
        {
            return Driver.FindElements(
                By.XPath($"//*[@resource-id='{NdiApp.PackageName}:id/{id}']//*[@clickable='true']"));
        }
        catch (Exception)
        {
            return [];
        }
    }

    private void TapAtCentre(IWebElement element)
    {
        var location = element.Location;
        var size = element.Size;
        var x = location.X + size.Width / 2;
        var y = location.Y + size.Height / 2;

        var touch = new PointerInputDevice(PointerKind.Touch, "finger");
        var sequence = new ActionSequence(touch, 0);

        sequence.AddAction(touch.CreatePointerMove(
            CoordinateOrigin.Viewport, x, y, TimeSpan.Zero));
        sequence.AddAction(touch.CreatePointerDown(MouseButton.Touch));
        sequence.AddAction(touch.CreatePointerUp(MouseButton.Touch));

        Driver.PerformActions([sequence]);
    }

    /// <summary>Replaces the text of the input with <paramref name="id"/>.</summary>
    protected void SetText(string id, string value)
    {
        var element = WaitFor(id);
        element.Clear();
        element.SendKeys(value);
    }

    /// <summary>Reads an element's text, waiting for it to appear.</summary>
    protected string TextOf(string id, TimeSpan? timeout = null) => WaitFor(id, timeout).Text;

    /// <summary>
    /// True when at least one element with <paramref name="id"/> is displayed right now.
    /// </summary>
    /// <remarks>
    /// Deliberately does not wait: this answers "is it there", and a waiting version would turn
    /// every negative check into a full timeout.
    /// </remarks>
    protected bool IsPresent(string id) => FindAll(id).Any(IsDisplayed);

    /// <summary>All elements carrying <paramref name="id"/>, displayed or not.</summary>
    /// <remarks>
    /// Row-template ids repeat once per row, so this is how a page object reaches "the third
    /// source" or counts what is on screen.
    /// </remarks>
    protected IReadOnlyList<IWebElement> FindAll(string id)
    {
        try
        {
            return Driver.FindElements(By.Id(id));
        }
        catch (NoSuchElementException)
        {
            return [];
        }
    }

    /// <summary>Elements with <paramref name="id"/> that are actually on screen.</summary>
    protected IReadOnlyList<IWebElement> FindDisplayed(string id) =>
        FindAll(id).Where(IsDisplayed).ToList();

    private static bool IsDisplayed(IWebElement element)
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
}

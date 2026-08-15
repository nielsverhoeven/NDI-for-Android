using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
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
            throw new WebDriverTimeoutException(
                $"{reason} on {Name} after {budget.TotalSeconds:0}s.{Environment.NewLine}" +
                FailureEvidence.DescribeVisibleIds(Driver));
        }
    }

    /// <summary>Taps the element with <paramref name="id"/> once it is displayed.</summary>
    protected void Tap(string id, TimeSpan? timeout = null) => WaitFor(id, timeout).Click();

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

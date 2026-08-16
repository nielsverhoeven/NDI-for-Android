using System.Text;
using System.Xml.Linq;
using OpenQA.Selenium.Appium.Android;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>What an accessibility rule found wrong with one element.</summary>
public sealed record A11yViolation(
    string Rule,
    string Element,
    string Detail)
{
    public override string ToString() => $"[{Rule}] {Element} — {Detail}";
}

/// <summary>One element as the Android accessibility tree describes it.</summary>
public sealed record A11yNode(
    string Class,
    string ResourceId,
    string ContentDescription,
    string Text,
    bool Clickable,
    bool Focusable,
    bool Displayed,
    int X,
    int Y,
    int Width,
    int Height)
{
    /// <summary>Short identifier for failure messages — id if there is one, else class + position.</summary>
    public string Describe() =>
        !string.IsNullOrEmpty(ResourceId) ? ResourceId
        : !string.IsNullOrEmpty(ContentDescription) ? $"{Short(Class)}('{ContentDescription}')"
        : !string.IsNullOrEmpty(Text) ? $"{Short(Class)}('{Text}')"
        : $"{Short(Class)}@{X},{Y}";

    private static string Short(string cls) => cls.Split('.').LastOrDefault() ?? cls;

    /// <summary>
    /// True when a user can act on this element, so it owes them a label and a usable target.
    /// </summary>
    /// <remarks>
    /// Both flags are needed. Shell handles taps above its navigation items, so those report
    /// <c>clickable=false</c> while still being focusable and operable — a clickable-only filter
    /// would exempt the exact controls whose missing labels started #314.
    /// </remarks>
    public bool IsInteractive => Displayed && (Clickable || Focusable) && Width > 0 && Height > 0;
}

/// <summary>
/// Audits the live Android accessibility tree for label, touch-target and identity problems.
/// </summary>
/// <remarks>
/// <para>
/// Reads the same tree a screen reader consumes — Appium's page source is a serialisation of
/// <c>AccessibilityNodeInfo</c> — so a violation here is a violation TalkBack would hit.
/// </para>
/// <para>
/// Contrast is not audited from this tree, because colour is not in it. That check lives in the
/// theme tests, which sample real pixels.
/// </para>
/// </remarks>
public sealed class AccessibilityAudit
{
    /// <summary>Android's minimum accessible touch target.</summary>
    public const double MinTouchTargetDp = 48;

    private readonly AndroidDriver _driver;
    private readonly DeviceMetrics _metrics;

    public AccessibilityAudit(AndroidDriver driver, DeviceMetrics metrics)
    {
        _driver = driver;
        _metrics = metrics;
    }

    /// <summary>Every element currently in the accessibility tree.</summary>
    public IReadOnlyList<A11yNode> ReadTree()
    {
        var source = _driver.PageSource;
        XDocument document;

        try
        {
            document = XDocument.Parse(source);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not parse the page source as XML ({ex.Message}). The audit cannot run.", ex);
        }

        return document.Descendants()
            .Select(Parse)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();
    }

    /// <summary>
    /// Runs every rule against the current screen.
    /// </summary>
    /// <param name="screen">Screen name, used in violation messages.</param>
    /// <param name="knownIds">
    /// Automation ids the app declares. Used by the "description is not an id" rule: an
    /// announcement of "home.startViewingLast" is technically non-empty and completely useless to
    /// a screen-reader user, and it is what happens if a framework reuses the test hook as the
    /// accessibility label.
    /// </param>
    public IReadOnlyList<A11yViolation> Run(string screen, IReadOnlySet<string> knownIds)
    {
        var violations = new List<A11yViolation>();
        var minPx = _metrics.ToPixels(MinTouchTargetDp);

        foreach (var node in ReadTree().Where(n => n.IsInteractive))
        {
            var name = $"{screen}:{node.Describe()}";
            var label = FirstNonEmpty(node.ContentDescription, node.Text);

            if (string.IsNullOrWhiteSpace(label))
            {
                violations.Add(new A11yViolation(
                    "missing-label", name,
                    "interactive element exposes neither a content description nor text, so a " +
                    "screen reader announces nothing"));
            }
            else if (knownIds.Contains(label))
            {
                violations.Add(new A11yViolation(
                    "label-is-automation-id", name,
                    $"announced as '{label}', which is an automation id rather than human " +
                    "language — a test hook is not an accessibility label"));
            }

            // Bounds smaller than the minimum are only a defect for something you must hit. A
            // focusable-but-not-clickable node (a label in a form) has no tap target to speak of.
            if (node.Clickable && (node.Width < minPx || node.Height < minPx))
            {
                violations.Add(new A11yViolation(
                    "touch-target", name,
                    $"{node.Width}x{node.Height}px is below the {MinTouchTargetDp}dp " +
                    $"({minPx}x{minPx}px) minimum"));
            }
        }

        return violations;
    }

    /// <summary>
    /// Renders a summary for the run output.
    /// </summary>
    /// <remarks>
    /// #314 requires results to be readable without downloading artifacts, so this goes to stdout
    /// and lands in the job log. Grouped by rule, because "11 elements are unlabelled" is the
    /// actionable shape, not eleven separate lines to correlate by eye.
    /// </remarks>
    public static string Summarise(IReadOnlyList<A11yViolation> violations, int budget)
    {
        var report = new StringBuilder()
            .AppendLine("── Accessibility audit ──────────────────────────────────")
            .AppendLine($"violations: {violations.Count}   budget: {budget}");

        if (violations.Count == 0)
        {
            report.AppendLine("No accessibility violations found.");
            return report.ToString();
        }

        foreach (var group in violations.GroupBy(v => v.Rule).OrderByDescending(g => g.Count()))
        {
            report.AppendLine().AppendLine($"{group.Key} ({group.Count()})");
            foreach (var violation in group.Take(25))
                report.AppendLine($"  {violation.Element} — {violation.Detail}");

            if (group.Count() > 25)
                report.AppendLine($"  ... and {group.Count() - 25} more");
        }

        return report.ToString();
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static A11yNode? Parse(XElement element)
    {
        var bounds = element.Attribute("bounds")?.Value;
        if (bounds is null)
            return null;

        // Android serialises bounds as "[left,top][right,bottom]".
        var numbers = bounds
            .Split(['[', ']', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .ToList();

        if (numbers.Count != 4 || numbers.Any(n => n is null))
            return null;

        int left = numbers[0]!.Value, top = numbers[1]!.Value;
        int right = numbers[2]!.Value, bottom = numbers[3]!.Value;

        return new A11yNode(
            Class:              Attr(element, "class"),
            ResourceId:         Attr(element, "resource-id"),
            ContentDescription: Attr(element, "content-desc"),
            Text:               Attr(element, "text"),
            Clickable:          Flag(element, "clickable"),
            Focusable:          Flag(element, "focusable"),
            Displayed:          Flag(element, "displayed", defaultValue: true),
            X: left,
            Y: top,
            Width:  right - left,
            Height: bottom - top);
    }

    private static string Attr(XElement element, string name) =>
        element.Attribute(name)?.Value ?? string.Empty;

    private static bool Flag(XElement element, string name, bool defaultValue = false) =>
        element.Attribute(name)?.Value is { } value
            ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            : defaultValue;
}

using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>
/// Writes everything needed to diagnose a UI failure without re-running it (#312).
/// </summary>
/// <remarks>
/// <para>
/// A UI test that fails with "element not found" and nothing else is a request for another
/// 8-minute CI round trip. The previous cycle spent several of those purely on opacity: two
/// wrong hypotheses about a locator were only settled once the run started dumping the actual
/// candidate elements. A screenshot would have settled it on the first run.
/// </para>
/// <para>
/// Captured per failure, into <c>E2E_ARTIFACT_DIR</c> (default <c>./e2e-artifacts</c>):
/// </para>
/// <list type="bullet">
///   <item><c>&lt;test&gt;.png</c> — what the user would have seen</item>
///   <item><c>&lt;test&gt;.xml</c> — the full view hierarchy, with ids, text and bounds</item>
///   <item><c>&lt;test&gt;.txt</c> — activity, orientation, window size, and the exception</item>
/// </list>
/// <para>
/// Every capture is best-effort. A driver that has already died must not turn a meaningful
/// assertion failure into an unrelated exception from the diagnostics — the original failure is
/// the thing under investigation, so collection errors are recorded in the text file and
/// swallowed.
/// </para>
/// </remarks>
public static class FailureEvidence
{
    /// <summary>Directory evidence is written to; created on first use.</summary>
    public static string ArtifactDirectory =>
        Environment.GetEnvironmentVariable("E2E_ARTIFACT_DIR") is { Length: > 0 } dir
            ? Path.GetFullPath(dir)
            : Path.GetFullPath("e2e-artifacts");

    /// <summary>
    /// Runs <paramref name="action"/>, capturing evidence if it throws, then rethrows.
    /// </summary>
    /// <remarks>
    /// The rethrow preserves the original exception and stack — the evidence is a side effect,
    /// never a replacement for the failure the test actually hit.
    /// </remarks>
    public static void Capture(AndroidDriver? driver, string testName, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Write(driver, testName, ex);
            throw;
        }
    }

    /// <summary>Writes the evidence set for a named test. Never throws.</summary>
    public static void Write(AndroidDriver? driver, string testName, Exception? failure)
    {
        var slug = Slug(testName);
        var notes = new StringBuilder()
            .AppendLine($"test:      {testName}")
            .AppendLine($"utc:       {DateTime.UtcNow:O}");

        if (failure is not null)
        {
            notes.AppendLine($"exception: {failure.GetType().FullName}")
                 .AppendLine(failure.Message)
                 .AppendLine()
                 .AppendLine(failure.StackTrace);
        }

        string dir;
        try
        {
            dir = ArtifactDirectory;
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            // Nowhere to write to — say so on the console, which is captured in the run log,
            // rather than losing the failure behind an IO exception.
            Console.Error.WriteLine($"[FailureEvidence] cannot create artifact directory: {ex.Message}");
            return;
        }

        if (driver is null)
        {
            notes.AppendLine().AppendLine("driver: null — no session, so no screenshot or hierarchy.");
            TryWriteText(Path.Combine(dir, $"{slug}.txt"), notes.ToString());
            return;
        }

        notes.AppendLine()
             .AppendLine($"activity:    {Probe(() => driver.CurrentActivity)}")
             .AppendLine($"package:     {Probe(() => driver.CurrentPackage)}")
             .AppendLine($"orientation: {Probe(() => driver.Orientation.ToString())}")
             .AppendLine($"window:      {Probe(() => driver.Manage().Window.Size.ToString())}");

        TryWriteText(Path.Combine(dir, $"{slug}.txt"), notes.ToString());

        try
        {
            driver.GetScreenshot().SaveAsFile(Path.Combine(dir, $"{slug}.png"));
        }
        catch (Exception ex)
        {
            TryAppendText(Path.Combine(dir, $"{slug}.txt"), $"\nscreenshot failed: {ex.Message}\n");
        }

        try
        {
            File.WriteAllText(Path.Combine(dir, $"{slug}.xml"), driver.PageSource);
        }
        catch (Exception ex)
        {
            TryAppendText(Path.Combine(dir, $"{slug}.txt"), $"\npage source failed: {ex.Message}\n");
        }
    }

    /// <summary>
    /// Lists every element carrying an automation id, with its text and bounds.
    /// </summary>
    /// <remarks>
    /// Included in locator-timeout messages. Answering "what ids were actually on screen" inline
    /// is usually enough to identify a wrong-page or not-yet-rendered failure without opening the
    /// hierarchy dump at all.
    /// </remarks>
    public static string DescribeVisibleIds(AndroidDriver driver)
    {
        var report = new StringBuilder("Elements carrying a resource-id at the time of failure:");

        try
        {
            var elements = driver.FindElements(By.XPath("//*[@resource-id and @resource-id!='']"));

            if (elements.Count == 0)
                return report.AppendLine().Append("  (none — the page exposes no automation ids)").ToString();

            foreach (var element in elements)
            {
                try
                {
                    report.AppendLine().Append(
                        $"  id='{element.GetAttribute("resource-id")}' " +
                        $"text='{element.Text}' " +
                        $"displayed={element.Displayed} " +
                        $"at={element.Location.X},{element.Location.Y}");
                }
                catch (StaleElementReferenceException)
                {
                    // The tree moved under us mid-dump; the rest of the list is still useful.
                }
            }
        }
        catch (Exception ex)
        {
            report.AppendLine().Append($"  (could not enumerate: {ex.GetType().Name}: {ex.Message})");
        }

        return report.ToString();
    }

    private static string Probe(Func<string?> read)
    {
        try
        {
            return read() ?? "(null)";
        }
        catch (Exception ex)
        {
            return $"(unavailable: {ex.GetType().Name})";
        }
    }

    private static void TryWriteText(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FailureEvidence] cannot write {path}: {ex.Message}");
        }
    }

    private static void TryAppendText(string path, string content)
    {
        try
        {
            File.AppendAllText(path, content);
        }
        catch
        {
            // Already reported the primary write; a failed append is not worth further noise.
        }
    }

    private static string Slug(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_');
        return new string(chars.ToArray());
    }
}

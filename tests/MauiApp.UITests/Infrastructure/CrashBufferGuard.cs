using NdiForAndroid.UITests.Pages;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>
/// Crash-buffer gate for the deep-link, permission and lifecycle tests. Records where the
/// device's crash buffer ends before a test runs and fails only if our package's name shows up in
/// what was added to it afterwards.
/// </summary>
/// <remarks>
/// Deliberately does not clear the buffer: it is device-wide, so clearing it before every test
/// would blame an unrelated system-app crash on the test that happened to run next, and would
/// erase the whole-run diagnostic <c>testing/e2e/scripts/run-emulator-tests.sh</c> dumps at the
/// end of a failed run.
/// </remarks>
public static class CrashBufferGuard
{
    private static int _baselineLength;

    public static void Mark(NdiApp app) =>
        _baselineLength = app.ShellCommand("logcat", "-b", "crash", "-d").Length;

    public static void AssertNoNewCrash(NdiApp app, string testName)
    {
        var dump = app.ShellCommand("logcat", "-b", "crash", "-d");
        if (dump.Length <= _baselineLength)
            return;

        var added = dump[_baselineLength..];
        if (!added.Contains(NdiApp.PackageName, StringComparison.Ordinal))
            return;

        throw new InvalidOperationException(
            $"{testName}: {NdiApp.PackageName} entered the logcat crash buffer during this " +
            $"test.{Environment.NewLine}{added}");
    }
}

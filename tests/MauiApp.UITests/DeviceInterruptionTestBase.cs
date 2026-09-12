using System.Runtime.CompilerServices;
using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;

namespace NdiForAndroid.UITests;

/// <summary>
/// Base for the deep-link, permission and lifecycle tests: every test ends with the crash-buffer
/// gate (<see cref="CrashBufferGuard"/>) on top of the screenshot/hierarchy capture
/// <see cref="UiTestBase.Run"/> already provides.
/// </summary>
public abstract class DeviceInterruptionTestBase : UiTestBase
{
    protected DeviceInterruptionTestBase(AppiumDriverFixture fixture) : base(fixture) { }

    protected void RunWithCrashGate(Action<NdiApp> body, [CallerMemberName] string testName = "") =>
        Run(app =>
        {
            CrashBufferGuard.Mark(app);
            try
            {
                body(app);
            }
            catch
            {
                // A test that throws mid-interruption (e.g. mid-rotation, still on a pushed page)
                // never reaches its own cleanup, leaving the device stuck for whatever runs next.
                // Best-effort only: this must not hide the real failure behind a recovery failure.
                try { app.Rotate(ScreenOrientation.Portrait); } catch { }
                try { app.Terminate(); } catch { }
                throw;
            }

            CrashBufferGuard.AssertNoNewCrash(app, testName);
        }, testName);
}

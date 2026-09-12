using System.Runtime.CompilerServices;
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
            body(app);
            CrashBufferGuard.AssertNoNewCrash(app, testName);
        }, testName);
}

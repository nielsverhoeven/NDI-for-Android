using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;

namespace NdiForAndroid.UITests.Pages;

/// <summary>The Stream tab: configure and run an outgoing NDI send.</summary>
public sealed class OutputPage : PageObject
{
    public OutputPage(AndroidDriver driver) : base(driver) { }

    protected override string PageId => TestIds.OutputPage;
    public override string Name => "Output";

    public string StreamName
    {
        get => TextOf(TestIds.OutputStreamName);
        set => SetText(TestIds.OutputStreamName, value);
    }

    public string Status => TextOf(TestIds.OutputStatus);

    /// <summary>
    /// True while a send is running — read from which of Start/Stop is on screen, since the two
    /// buttons are mutually exclusive on <c>IsOutputActive</c>.
    /// </summary>
    public bool IsOutputActive => IsPresent(TestIds.OutputStop);

    /// <summary>Connection count, or <c>null</c> while the label is hidden (output stopped).</summary>
    public int? ConnectionCount
    {
        get
        {
            if (!IsPresent(TestIds.OutputConnectionCount))
                return null;

            var digits = new string(TextOf(TestIds.OutputConnectionCount).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var count) ? count : null;
        }
    }

    /// <summary>True when the source is on program and the ON AIR tally is showing.</summary>
    public bool IsOnAir => IsPresent(TestIds.OutputOnAirTally);

    public void Start() => Tap(TestIds.OutputStart);
    public void Stop()  => Tap(TestIds.OutputStop);

    /// <summary>Starts the send and waits for the running state to be reflected in the UI.</summary>
    public void StartAndWaitUntilActive()
    {
        Start();
        WaitFor(TestIds.OutputStop, Timeouts.Network, "Output did not report as started");
    }

    public void ToggleMicrophone() => Tap(TestIds.OutputMicrophoneToggle);
    public void ToggleReStreamMode() => Tap(TestIds.OutputModeToggle);
}

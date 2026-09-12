using OpenQA.Selenium;
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
    /// True when the status line is showing an error. Reads the warning glyph, which is only in
    /// the view tree while <c>IsStatusError</c> is true — a non-colour cue, so this holds in
    /// grayscale and pins no hex value.
    /// </summary>
    public bool IsStatusError => IsPresent(TestIds.OutputStatusErrorIcon);

    /// <summary>The status label element, for pixel sampling with <c>ScreenSampler</c>.</summary>
    public IWebElement StatusElement => WaitFor(TestIds.OutputStatus);

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

    /// <summary>What TalkBack would announce for the mode Switch — its content-desc.</summary>
    public string ModeToggleLabel => WaitFor(TestIds.OutputModeToggle).GetAttribute("content-desc") ?? string.Empty;

    /// <summary>What TalkBack would announce for the microphone Switch.</summary>
    public string MicrophoneToggleLabel => WaitFor(TestIds.OutputMicrophoneToggle).GetAttribute("content-desc") ?? string.Empty;

    /// <summary>True when the mode switch sits in its re-stream position (IsToggled binds the INVERSE of IsReStreamMode).</summary>
    public bool IsReStreamMode => !IsChecked(TestIds.OutputModeToggle);

    public bool IsMicrophoneOn => IsChecked(TestIds.OutputMicrophoneToggle);

    public System.Drawing.Rectangle StartButtonBounds   => BoundsOf(TestIds.OutputStart);
    public System.Drawing.Rectangle ModeRowBounds       => BoundsOf(TestIds.OutputModeRow);
    public System.Drawing.Rectangle MicrophoneRowBounds => BoundsOf(TestIds.OutputMicrophoneRow);

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

    /// <summary>Taps the 'Re-stream:' label — the row, not the switch — to prove the whole row toggles (#346).</summary>
    public void TapReStreamLabel()   => Tap(TestIds.OutputModeReStreamLabel);
    public void TapMicrophoneLabel() => Tap(TestIds.OutputMicrophoneLabel);

    /// <summary>True when the re-stream source Picker (not the free-text fallback) is on screen.</summary>
    public bool IsReStreamSourcePickerShown => IsPresent(TestIds.OutputReStreamSourcePicker);

    /// <summary>True when the free-text fallback Entry is on screen (no cached/discovered sources).</summary>
    public bool IsReStreamManualEntryShown => IsPresent(TestIds.OutputReStreamSourceId);

    /// <summary>What TalkBack would announce for the re-stream source Picker — its content-desc.</summary>
    public string ReStreamSourcePickerAccessibleName =>
        IsReStreamSourcePickerShown
            ? WaitFor(TestIds.OutputReStreamSourcePicker).GetAttribute("content-desc") ?? string.Empty
            : string.Empty;

    /// <summary>Text currently shown in the re-stream source Picker (the selected source's display name).</summary>
    public string ReStreamSourcePickerText => TextOf(TestIds.OutputReStreamSourcePicker);

    /// <summary>Taps the re-stream source Picker to open its native selection dialog.</summary>
    public void OpenReStreamSourcePicker() => Tap(TestIds.OutputReStreamSourcePicker);
}

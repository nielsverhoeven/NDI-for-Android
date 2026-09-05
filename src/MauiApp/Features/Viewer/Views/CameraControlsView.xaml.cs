using NdiForAndroid.Features.Viewer.ViewModels;
using Timer = System.Threading.Timer;

namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>PTZ endpoint chip + pad + zoom + presets, bound to the hosting page's <c>ViewerViewModel</c>.</summary>
public partial class CameraControlsView : ContentView
{
    private const int LongPressThresholdMs = 600;

    private readonly List<Timer> _presetTimers = new();

    public CameraControlsView()
    {
        InitializeComponent();

        WirePreset(Preset1Button, 1);
        WirePreset(Preset2Button, 2);
        WirePreset(Preset3Button, 3);
        WirePreset(Preset4Button, 4);
        WirePreset(Preset5Button, 5);
        WirePreset(Preset6Button, 6);
        WirePreset(Preset7Button, 7);
        WirePreset(Preset8Button, 8);

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        foreach (var timer in _presetTimers)
            timer.Dispose();
        _presetTimers.Clear();
    }

    private void WirePreset(Button button, int presetNumber)
    {
        Timer? longPressTimer = null;
        var longPressFired = false;

        button.Pressed += (_, _) =>
        {
            longPressTimer?.Dispose();
            longPressFired = false;
            longPressTimer = new Timer(_ =>
            {
                longPressFired = true;
                Dispatcher.Dispatch(() =>
                {
                    if (BindingContext is ViewerViewModel vm && vm.PtzStorePresetCommand.CanExecute(presetNumber))
                        vm.PtzStorePresetCommand.Execute(presetNumber);
                });
            }, null, LongPressThresholdMs, Timeout.Infinite);
            _presetTimers.Add(longPressTimer);
        };

        button.Released += (_, _) =>
        {
            longPressTimer?.Dispose();
            longPressTimer = null;

            if (longPressFired)
                return;

            Dispatcher.Dispatch(() =>
            {
                if (BindingContext is ViewerViewModel vm && vm.PtzRecallPresetCommand.CanExecute(presetNumber))
                    vm.PtzRecallPresetCommand.Execute(presetNumber);
            });
        };
    }
}

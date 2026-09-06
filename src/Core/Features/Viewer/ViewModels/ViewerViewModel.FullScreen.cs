using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NdiForAndroid.Features.Viewer.ViewModels;

public partial class ViewerViewModel
{
    private const int OverlayAutoHideSeconds = 3;

    private ITimer? _overlayAutoHideTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreControlsVisible))]
    private bool _isFullScreen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreControlsVisible))]
    private bool _isControlsOverlayVisible = true;

    /// <summary>Controls are visible in normal mode, or in full screen while the overlay hasn't auto-hidden.</summary>
    public bool AreControlsVisible => !IsFullScreen || IsControlsOverlayVisible;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsPlaying))
        {
            _immersiveMode.KeepScreenOn(IsPlaying);
        }
    }

    partial void OnIsFullScreenChanged(bool value)
    {
        if (value)
        {
            IsControlsOverlayVisible = true;
            ResetOverlayAutoHideTimer();
        }
        else
        {
            _overlayAutoHideTimer?.Dispose();
            _overlayAutoHideTimer = null;
            IsControlsOverlayVisible = true;
        }
    }

    [RelayCommand]
    private void ToggleFullScreen()
    {
        if (!IsPlaying && !IsFullScreen)
            return;

        IsFullScreen = !IsFullScreen;
    }

    [RelayCommand]
    private void ShowControlsOverlay() => NotifyControlInteraction();

    /// <summary>Resets the auto-hide countdown while full screen; no-op otherwise.</summary>
    public void NotifyControlInteraction()
    {
        if (IsFullScreen)
            ResetOverlayAutoHideTimer();
    }

    private void ResetOverlayAutoHideTimer()
    {
        IsControlsOverlayVisible = true;
        var due = TimeSpan.FromSeconds(OverlayAutoHideSeconds);

        if (_overlayAutoHideTimer is null)
            _overlayAutoHideTimer = _timeProvider.CreateTimer(
                _ => _dispatcher.BeginInvokeOnMainThread(HideControlsOverlay), null, due, Timeout.InfiniteTimeSpan);
        else
            _overlayAutoHideTimer.Change(due, Timeout.InfiniteTimeSpan);
    }

    private void HideControlsOverlay()
    {
        if (IsFullScreen)
            IsControlsOverlayVisible = false;
    }
}

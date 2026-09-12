using System.ComponentModel;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.Services;

/// <summary>
/// Owns full-screen chrome for whichever host page is currently showing the viewer: immersive
/// mode, that page's own Shell nav bar and tab bar, the shared Shell rail suppression flag, and
/// (#384 slice 3) the device orientation lock. Transient — each host page (<c>ViewerPage</c>,
/// <c>SourceListPage</c>) gets its own instance and Attaches/Detaches it across its own
/// OnAppearing/OnDisappearing, so only the page actually on screen ever owns the shared
/// <see cref="AdaptiveShellStateViewModel.IsChromeSuppressed"/> flag.
/// </summary>
public sealed class ViewerFullScreenChromeController
{
    private readonly IImmersiveModeService _immersiveMode;
    private readonly AdaptiveShellStateViewModel _shellState;
    private readonly IOrientationLockService _orientationLock;

    private Page? _page;
    private ViewerViewModel? _viewModel;

    public ViewerFullScreenChromeController(
        IImmersiveModeService immersiveMode,
        AdaptiveShellStateViewModel shellState,
        IOrientationLockService orientationLock)
    {
        _immersiveMode = immersiveMode;
        _shellState = shellState;
        _orientationLock = orientationLock;
    }

    /// <summary>Wires chrome ownership to <paramref name="page"/>/<paramref name="viewModel"/>. Idempotent
    /// and safe to call repeatedly with the same pair (e.g. a revisited singleton host page).</summary>
    public void Attach(Page page, ViewerViewModel viewModel)
    {
        if (ReferenceEquals(_page, page) && ReferenceEquals(_viewModel, viewModel))
            return;

        Detach();

        _page = page;
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyChrome(_viewModel.IsFullScreen);
    }

    /// <summary>
    /// Unconditionally releases chrome ownership: forces full screen off and abandons any
    /// in-flight orientation request (<see cref="ViewerViewModel.ForceExitFullScreen"/>), clears
    /// the shared suppression flag, exits immersive mode, releases the orientation lock, and
    /// restores the page's own nav bar and tab bar. Safe to call when not attached.
    /// </summary>
    public void Detach()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ForceExitFullScreen();
        }

        _shellState.IsChromeSuppressed = false;
        _immersiveMode.ExitImmersive();
        _orientationLock.Release();
        if (_page is not null)
        {
            Shell.SetNavBarIsVisible(_page, true);
            Shell.SetTabBarIsVisible(_page, true);
        }

        _page = null;
        _viewModel = null;
    }

    /// <summary>Full screen's Back handling: delegates to <see cref="ViewerViewModel.HandleBackButtonPress"/>
    /// for the PTZ-layer / full-screen / pending-orientation order. Returns <c>false</c> (not
    /// consumed) when not attached, so the caller falls through to its own default Back behaviour.</summary>
    public bool HandleBackButton() => _viewModel is not null && _viewModel.HandleBackButtonPress();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsFullScreen) && _viewModel is not null)
            ApplyChrome(_viewModel.IsFullScreen);
    }

    private void ApplyChrome(bool isFullScreen)
    {
        _shellState.IsChromeSuppressed = isFullScreen;

        if (isFullScreen)
            _immersiveMode.EnterImmersive();
        else
            _immersiveMode.ExitImmersive();

        if (_page is not null)
        {
            Shell.SetNavBarIsVisible(_page, !isFullScreen);
            Shell.SetTabBarIsVisible(_page, !isFullScreen);
        }
    }
}

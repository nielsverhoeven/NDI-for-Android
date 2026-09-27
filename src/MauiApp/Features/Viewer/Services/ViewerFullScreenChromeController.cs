using System.ComponentModel;
using NdiForAndroid.Features.DiagOverlay.Services;
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
    private readonly IDiagnosticOverlayService? _diagnostics;

    private Page? _page;
    private ViewerViewModel? _viewModel;

    /// <summary>True while this controller has written the page-scoped chrome overrides onto
    /// <see cref="_page"/>. Gates the restore so a page that never went full screen keeps
    /// Shell's own values (this controller is the app's only writer of those two attached
    /// properties).</summary>
    private bool _chromeOverridden;

    public ViewerFullScreenChromeController(
        IImmersiveModeService immersiveMode,
        AdaptiveShellStateViewModel shellState,
        IOrientationLockService orientationLock,
        IDiagnosticOverlayService? diagnostics = null)
    {
        _immersiveMode = immersiveMode;
        _shellState = shellState;
        _orientationLock = orientationLock;
        _diagnostics = diagnostics;
    }

    /// <summary>Wires chrome ownership to <paramref name="page"/>/<paramref name="viewModel"/>. Idempotent
    /// and safe to call repeatedly with the same pair (e.g. a revisited singleton host page).</summary>
    public void Attach(Page page, ViewerViewModel viewModel)
    {
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "chrome.attach.begin");

        if (ReferenceEquals(_page, page) && ReferenceEquals(_viewModel, viewModel))
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "chrome.attach.end", $"ms={Environment.TickCount64 - t0}");
            return;
        }

        Detach();

        _page = page;
        _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyChrome(_viewModel.IsFullScreen);
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "chrome.attach.end", $"ms={Environment.TickCount64 - t0}");
    }

    /// <summary>
    /// Unconditionally releases chrome ownership: forces full screen off and abandons any
    /// in-flight orientation request (<see cref="ViewerViewModel.ForceExitFullScreen"/>), clears
    /// the shared suppression flag, exits immersive mode, releases the orientation lock, and
    /// reverts the page's own nav bar and tab bar to Shell's defaults (see
    /// <see cref="RestoreChrome"/>). Safe to call when not attached.
    /// </summary>
    public void Detach()
    {
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "chrome.detach.begin");

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ForceExitFullScreen();
        }

        _shellState.IsChromeSuppressed = false;
        _immersiveMode.ExitImmersive();
        _orientationLock.Release();
        RestoreChrome();

        _page = null;
        _viewModel = null;

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "chrome.detach.end", $"ms={Environment.TickCount64 - t0}");
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
        {
            _immersiveMode.EnterImmersive();
            OverrideChrome();
        }
        else
        {
            _immersiveMode.ExitImmersive();
            RestoreChrome();
        }
    }

    /// <summary>Hides the host page's own nav bar and tab bar, page-scoped. This is the one
    /// mechanism that hides the bottom bar for full screen; the left rail is driven separately,
    /// through <see cref="AdaptiveShellStateViewModel.IsChromeSuppressed"/>.</summary>
    /// <remarks>
    /// Page scope is this controller's alone (#395). <c>AppShell.ApplyPlacement</c> writes the other
    /// scope — <c>Shell.TabBarIsVisible</c> on the <c>PrimaryTabBar</c> ShellItem, from the placement —
    /// and page scope wins over item scope, so the <c>false</c> written here hides the bar whatever
    /// the placement says. Setting the value on the page also raises the page's own
    /// <c>PropertyChanged</c>, which is what makes Android re-evaluate the bar.
    /// </remarks>
    private void OverrideChrome()
    {
        if (_page is null)
            return;

        Shell.SetNavBarIsVisible(_page, false);
        Shell.SetTabBarIsVisible(_page, false);
        _chromeOverridden = true;
    }

    /// <summary>
    /// Reverts to Shell's own per-page defaults by clearing the attached properties. Never writes
    /// <c>true</c>, and never touches a property this controller did not set — writing
    /// <c>true</c> is not the inverse of writing <c>false</c>: a page-scoped <c>true</c> would
    /// outrank the placement value for good.
    /// </summary>
    /// <remarks>
    /// With the page value cleared, <c>Shell.TabBarIsVisible</c> resolves up the tree to the value
    /// <c>AppShell.ApplyPlacement</c> keeps on <c>PrimaryTabBar</c> (#395): hidden under the rail,
    /// shown on bottom placement. So exiting full screen on a rail device can never bring the bottom
    /// bar back (the #401 class of chip), and a rotation made while full screen is already reflected
    /// in the value this falls back to. <c>ClearValue</c> raises the page's <c>PropertyChanged</c>,
    /// which makes Android re-evaluate the bar.
    /// </remarks>
    private void RestoreChrome()
    {
        if (_page is not null && _chromeOverridden)
        {
            _page.ClearValue(Shell.NavBarIsVisibleProperty);
            _page.ClearValue(Shell.TabBarIsVisibleProperty);
        }

        _chromeOverridden = false;
    }
}

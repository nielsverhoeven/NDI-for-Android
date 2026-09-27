using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Home.ViewModels;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Home.Views;

/// <summary>Home tab root. Singleton (matches <see cref="HomeViewModel"/>, #352/#359).</summary>
/// <remarks>
/// Hosted by the one <c>home</c> <c>ShellContent</c> (#395); a placement change (rotation, size class) only
/// swaps the chrome around it, so it raises no Disappearing/Appearing. Never dispose the ViewModel from
/// page lifecycle — per-visit work is <see cref="OnAppearing"/> re-running <c>RefreshCommand</c>.
/// </remarks>
public partial class HomePage : ContentPage, IShellChromeHost
{
    private readonly IDiagnosticOverlayService? _diagnostics;

    public HomePage(HomeViewModel viewModel, IDiagnosticOverlayService? diagnostics = null)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _diagnostics = diagnostics;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.begin", "name=Home");
        (BindingContext as HomeViewModel)?.RefreshCommand.Execute(null);
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "page.appearing.end", $"name=Home ms={Environment.TickCount64 - t0}");
    }

    /// <inheritdoc cref="IShellChromeHost.RefreshShellChrome"/>
    public void RefreshShellChrome() => OnPropertyChanged(Shell.TabBarIsVisibleProperty.PropertyName);
}

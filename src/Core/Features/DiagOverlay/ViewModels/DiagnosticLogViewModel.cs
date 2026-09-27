using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.DiagOverlay.ViewModels;

/// <summary>
/// The in-memory diagnostic log page (#241), opened from Settings → Developer tools (#437).
/// While the page is on screen (<see cref="Activate"/> / <see cref="Deactivate"/>, driven by the
/// page's OnAppearing / OnDisappearing) the list refreshes every 2 s on the injected
/// <see cref="TimeProvider"/>.
/// </summary>
public partial class DiagnosticLogViewModel : ObservableObject
{
    /// <summary>Shell route of the page: registered in AppShell, navigated to from Settings.</summary>
    public const string Route = "diagnostic-log";

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private readonly IDiagnosticOverlayService _overlayService;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    // Only runs between Activate and Deactivate. The page and this ViewModel are transient, so a
    // timer started in the constructor (as before #437) outlived every visit and kept refreshing
    // an invisible list forever.
    private ITimer? _refreshTimer;

    [ObservableProperty]
    private IReadOnlyList<LogEntryViewModel> _logEntries = Array.Empty<LogEntryViewModel>();

    public DiagnosticLogViewModel(
        IDiagnosticOverlayService overlayService,
        IMainThreadDispatcher dispatcher,
        TimeProvider? timeProvider = null)
    {
        _overlayService = overlayService;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider ?? TimeProvider.System;
        RefreshLog();
    }

    /// <summary>Refreshes now and every 2 s until <see cref="Deactivate"/>. Idempotent.</summary>
    public void Activate()
    {
        RefreshLog();
        _refreshTimer ??= _timeProvider.CreateTimer(
            _ => _dispatcher.BeginInvokeOnMainThread(RefreshLog), null, RefreshInterval, RefreshInterval);
    }

    /// <summary>Stops the periodic refresh. Safe to call when not active.</summary>
    public void Deactivate()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    [RelayCommand]
    private void Clear()
    {
        _overlayService.LogBuffer.Clear();
        RefreshLog();
    }

    private void RefreshLog()
    {
        var entries = _overlayService.LogBuffer.GetEntries();
        LogEntries = entries.Select(e => new LogEntryViewModel(
            e.TimestampEpochMillis, e.Category, e.Message, e.Level)).ToList().AsReadOnly();
    }
}

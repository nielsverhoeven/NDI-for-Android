using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Navigation.Services;

public sealed class NdiNavigationHandoffService : INavigationHandoffService
{
    private readonly INdiViewerBridge _viewerBridge;
    private readonly IDiagnosticOverlayService? _diagnostics;

    public NdiNavigationHandoffService(INdiViewerBridge viewerBridge, IDiagnosticOverlayService? diagnostics = null)
    {
        _viewerBridge = viewerBridge;
        _diagnostics = diagnostics;
    }

    public event EventHandler? ViewerReceiverStopped;

    public Task HandlePrimaryDestinationChangeAsync(
        PrimaryNavDestination from,
        PrimaryNavDestination to,
        CancellationToken cancellationToken = default)
    {
        var t0 = Environment.TickCount64;
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.begin", $"from={from} to={to}");

        if (from == to || from != PrimaryNavDestination.View)
        {
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.end", $"ms={Environment.TickCount64 - t0}");
            return Task.CompletedTask;
        }

        // Only the synchronous prologue (signal the pumps, tag the stop as intentional) runs in
        // this call's turn; the returned task completes when the native teardown has, on the
        // bridge's own lifecycle worker. Nothing on the incoming page depends on it.
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.stop.begin");
        var stopTask = _viewerBridge.StopReceiverAsync();
        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.stop.end", $"ms={Environment.TickCount64 - t0}");

        // After the stop request, so the bridge is already Disconnected (Intentional) when the
        // owning ViewModel reconciles its state (#410).
        RaiseViewerReceiverStopped();

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.end", $"ms={Environment.TickCount64 - t0}");
        return stopTask;
    }

    /// <summary>One subscriber at a time, each guarded: this runs inside a navigation, the stop has
    /// already been requested, and one faulting ViewModel must neither fail the handoff nor keep
    /// the others claiming to play a receiver that is gone.</summary>
    private void RaiseViewerReceiverStopped()
    {
        if (ViewerReceiverStopped is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.stopped.fault",
                    $"type={ex.GetType().Name}");
            }
        }
    }
}

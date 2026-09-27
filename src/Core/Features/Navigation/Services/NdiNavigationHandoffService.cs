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

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.end", $"ms={Environment.TickCount64 - t0}");
        return stopTask;
    }
}

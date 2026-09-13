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

        if (from != to && from == PrimaryNavDestination.View)
        {
            var stopStartedAt = Environment.TickCount64;
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.stop.begin");
            _viewerBridge.StopReceiver();
            _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.stop.end", $"ms={Environment.TickCount64 - stopStartedAt}");
        }

        _diagnostics?.Trace(DiagnosticOverlayService.NavigationLogTag, "handoff.svc.end", $"ms={Environment.TickCount64 - t0}");
        return Task.CompletedTask;
    }
}

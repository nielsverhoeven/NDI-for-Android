using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Navigation.Services;

public sealed class NdiNavigationHandoffService : INavigationHandoffService
{
    private readonly INdiViewerBridge _viewerBridge;

    public NdiNavigationHandoffService(INdiViewerBridge viewerBridge)
    {
        _viewerBridge = viewerBridge;
    }

    public Task HandlePrimaryDestinationChangeAsync(
        PrimaryNavDestination from,
        PrimaryNavDestination to,
        CancellationToken cancellationToken = default)
    {
        if (from != to && from == PrimaryNavDestination.View)
            _viewerBridge.StopReceiver();

        return Task.CompletedTask;
    }
}

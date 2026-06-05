using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Navigation.Services;

public sealed class NdiNavigationHandoffService : INavigationHandoffService
{
    private readonly INdiViewerBridge _viewerBridge;
    private readonly INdiOutputBridge _outputBridge;

    public NdiNavigationHandoffService(INdiViewerBridge viewerBridge, INdiOutputBridge outputBridge)
    {
        _viewerBridge = viewerBridge;
        _outputBridge = outputBridge;
    }

    public async Task HandlePrimaryDestinationChangeAsync(
        PrimaryNavDestination from,
        PrimaryNavDestination to,
        CancellationToken cancellationToken = default)
    {
        if (from == to)
            return;

        if (from == PrimaryNavDestination.View)
            _viewerBridge.StopReceiver();

        if (from == PrimaryNavDestination.Stream)
            await _outputBridge.StopOutputAsync(cancellationToken);
    }
}

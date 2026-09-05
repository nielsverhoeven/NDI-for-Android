using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Navigation.Services;

public sealed class NdiNavigationHandoffService : INavigationHandoffService
{
    private readonly INdiViewerBridge _viewerBridge;
    private readonly INdiOutputBridge _outputBridge;
    private readonly IAppStateRepository _appStateRepo;

    public NdiNavigationHandoffService(
        INdiViewerBridge viewerBridge,
        INdiOutputBridge outputBridge,
        IAppStateRepository appStateRepo)
    {
        _viewerBridge = viewerBridge;
        _outputBridge = outputBridge;
        _appStateRepo = appStateRepo;
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
        {
            var state = await _appStateRepo.RestoreStateAsync();
            await _appStateRepo.SaveAsync(new AppStateSnapshot(state.LastViewerSourceId, state.StreamName, false, state.LastSelectedSourceId));
            await _outputBridge.StopOutputAsync(cancellationToken);
        }
    }
}

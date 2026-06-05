using NdiForAndroid.Features.Navigation.Models;

namespace NdiForAndroid.Features.Navigation.Services;

public interface INavigationHandoffService
{
    Task HandlePrimaryDestinationChangeAsync(
        PrimaryNavDestination from,
        PrimaryNavDestination to,
        CancellationToken cancellationToken = default);
}

using NdiForAndroid.Features.Navigation.Models;

namespace NdiForAndroid.Features.Navigation.Services;

public interface INavigationHandoffService
{
    Task HandlePrimaryDestinationChangeAsync(
        PrimaryNavDestination from,
        PrimaryNavDestination to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised after leaving the View destination has asked the viewer bridge to stop its receiver,
    /// in the caller's turn (the UI thread in the app, but subscribers must not rely on that). The
    /// bridge is shared, so the stop ends whichever viewer session owned the receiver without that
    /// ViewModel having asked for it: the owning <c>ViewerViewModel</c> ends its session like a
    /// Stop (#410); every other subscriber ignores it. Carries no payload: ownership is read from
    /// the bridge's <c>ReceiverGeneration</c>, which a stop does not change.
    /// </summary>
    event EventHandler? ViewerReceiverStopped;
}

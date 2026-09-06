using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Ptz.Services;

/// <inheritdoc cref="IPtzControllerFactory"/>
public sealed class PtzControllerFactory : IPtzControllerFactory
{
    private readonly INdiViewerBridge _bridge;
    private readonly IViscaTransportFactory _transportFactory;

    public PtzControllerFactory(INdiViewerBridge bridge, IViscaTransportFactory transportFactory)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(transportFactory);
        _bridge = bridge;
        _transportFactory = transportFactory;
    }

    public IPtzController Create(PtzEndpoint? endpoint) =>
        endpoint is null
            ? new NdiPtzController(_bridge)
            : new ViscaPtzController(_transportFactory.Create(), endpoint);
}

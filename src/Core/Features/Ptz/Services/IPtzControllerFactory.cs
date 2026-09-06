using NdiForAndroid.Features.Ptz.Models;

namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>
/// Selects the PTZ backend to use: VISCA-over-TCP when an endpoint is configured, otherwise the
/// connected NDI source's own PTZ. Takes a <see cref="PtzEndpoint"/> rather than a source model so
/// that <c>Features.Ptz</c> has no dependency on <c>Features.Sources</c>.
/// </summary>
public interface IPtzControllerFactory
{
    /// <summary>Creates a controller for <paramref name="endpoint"/>, or for the NDI receiver's own PTZ when null.</summary>
    IPtzController Create(PtzEndpoint? endpoint);
}

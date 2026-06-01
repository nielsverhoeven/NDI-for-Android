using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

public sealed class TelemetryService : ITelemetryService
{
    public void Track(string eventName, IDictionary<string, string>? properties = null)
    {
        // TODO (T010): wire to Application Insights or local telemetry sink.
        System.Diagnostics.Debug.WriteLine(
            $"[Telemetry] {eventName} {(properties is null ? "" : string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")))}");
    }
}

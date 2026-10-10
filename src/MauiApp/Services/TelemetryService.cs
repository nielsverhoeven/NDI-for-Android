using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

public sealed class TelemetryService : ITelemetryService
{
    public void Track(string eventName, IDictionary<string, string>? properties = null)
    {
        // TODO (T010): wire to Application Insights or local telemetry sink.
        // Any off-device telemetry requires updating docs/privacy-policy.md and the Play Data-safety form first.
        System.Diagnostics.Debug.WriteLine(
            $"[Telemetry] {eventName} {(properties is null ? "" : string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}")))}");
    }
}

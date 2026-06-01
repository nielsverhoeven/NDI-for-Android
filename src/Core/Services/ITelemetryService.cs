namespace NdiForAndroid.Services;

public interface ITelemetryService
{
    void Track(string eventName, IDictionary<string, string>? properties = null);
}

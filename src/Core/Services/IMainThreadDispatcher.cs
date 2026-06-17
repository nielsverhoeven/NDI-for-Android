namespace NdiForAndroid.Services;

/// <summary>
/// Marshals an action onto the UI/main thread. A MAUI-free seam so Core ViewModels
/// can dispatch observable mutations from timer/poll callbacks without referencing MAUI.
/// On Android the MAUI implementation wraps <c>MainThread.BeginInvokeOnMainThread</c>;
/// unit tests use a synchronous inline fake.
/// </summary>
public interface IMainThreadDispatcher
{
    /// <summary>Schedules <paramref name="action"/> to run on the main thread.</summary>
    void Invoke(Action action);

    /// <summary>Schedules <paramref name="action"/> to run on the main thread and awaits it.</summary>
    Task InvokeAsync(Func<Task> action);
}

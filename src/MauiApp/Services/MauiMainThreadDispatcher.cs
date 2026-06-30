using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

/// <summary>
/// MAUI implementation of <see cref="IMainThreadDispatcher"/> that marshals work onto the
/// application main thread via <see cref="MainThread"/>. The Core project cannot reference
/// MAUI, so Core ViewModels depend on the abstraction and this implementation is registered
/// in <c>MauiProgram.cs</c>.
/// </summary>
public sealed class MauiMainThreadDispatcher : IMainThreadDispatcher
{
    public void Invoke(Action action) => MainThread.BeginInvokeOnMainThread(action);

    public Task InvokeAsync(Func<Task> action) => MainThread.InvokeOnMainThreadAsync(action);
}

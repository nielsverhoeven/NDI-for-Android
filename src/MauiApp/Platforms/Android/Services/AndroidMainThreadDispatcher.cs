using Microsoft.Maui.Controls;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Main thread dispatcher backed by MAUI's BeginInvokeOnMainThread.
/// </summary>
internal sealed class AndroidMainThreadDispatcher : IMainThreadDispatcher
{
    public void BeginInvokeOnMainThread(Action action) =>
        MainThread.BeginInvokeOnMainThread(action);
}

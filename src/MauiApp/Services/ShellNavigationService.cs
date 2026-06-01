using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

/// <summary>
/// MAUI Shell implementation of <see cref="INavigationService"/>.
/// Registered in DI so ViewModels stay free of MAUI Shell references.
/// </summary>
public sealed class ShellNavigationService : INavigationService
{
    public Task NavigateToAsync(string route) =>
        Shell.Current.GoToAsync(route);

    public Task GoBackAsync() =>
        Shell.Current.GoToAsync("..");
}

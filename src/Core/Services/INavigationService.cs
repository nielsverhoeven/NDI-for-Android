namespace NdiForAndroid.Services;

/// <summary>Abstraction over MAUI Shell navigation, allowing ViewModels to be tested without MAUI runtime.</summary>
public interface INavigationService
{
    Task NavigateToAsync(string route);
    Task GoBackAsync();
}

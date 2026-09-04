using NdiForAndroid.Features.Navigation.Models;

namespace NdiForAndroid.Services;

/// <summary>Abstraction over MAUI Shell navigation, allowing ViewModels to be tested without MAUI runtime.</summary>
public interface INavigationService
{
    Task NavigateToAsync(string route);
    Task NavigateToPrimaryAsync(PrimaryNavDestination destination, string? queryString = null);
    Task GoBackAsync();
}

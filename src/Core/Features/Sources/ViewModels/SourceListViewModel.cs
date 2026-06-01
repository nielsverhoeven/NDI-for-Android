using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Sources.ViewModels;

public partial class SourceListViewModel : ObservableObject
{
    private readonly ISourceRepository _repository;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private IReadOnlyList<NdiSource> _sources = Array.Empty<NdiSource>();

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _errorMessage;

    public SourceListViewModel(ISourceRepository repository, INavigationService navigation)
    {
        _repository = repository;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        ErrorMessage = null;
        try
        {
            var snapshot = await _repository.DiscoverAsync(cancellationToken);
            Sources = snapshot.Sources;
            if (snapshot.Status == DiscoveryStatus.Failure)
                ErrorMessage = snapshot.ErrorMessage;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task NavigateToViewerAsync(NdiSource source) =>
        _navigation.NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(source.SourceId)}");

    [RelayCommand]
    private Task NavigateToOutputAsync(NdiSource source) =>
        _navigation.NavigateToAsync($"output?sourceId={Uri.EscapeDataString(source.SourceId)}");
}

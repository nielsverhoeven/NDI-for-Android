using NdiForAndroid.Features.AppState.Models;

namespace NdiForAndroid.Features.AppState.Repositories;

/// <summary>
/// Repository for persisting application state that survives backgrounding and process death.
/// </summary>
public interface IAppStateRepository
{
    Task SaveAsync(AppStateSnapshot snapshot);
    Task<AppStateSnapshot> RestoreStateAsync();
}

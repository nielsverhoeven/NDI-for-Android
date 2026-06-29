using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using Xunit;

namespace MauiApp.Tests.Features.AppState;

public class AppStateRepositoryTests
{
    [Fact]
    public async Task SaveAndRestore_PersistsAllFields()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test-app-state-{Guid.NewGuid()}.db3");
        try
        {
            using var repo = new AppStateRepository(dbPath);

            var snapshot = new AppStateSnapshot("viewer-src-42", "StreamX", true, "source-selected-7");

            await repo.SaveAsync(snapshot);
            var restored = await repo.RestoreStateAsync();

            Assert.Equal("viewer-src-42", restored.LastViewerSourceId);
            Assert.Equal("StreamX", restored.StreamName);
            Assert.True(restored.IsOutputActive);
            Assert.Equal("source-selected-7", restored.LastSelectedSourceId);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task RestoreStateAsync_WhenNoKeysExist_ReturnsEmptySnapshot()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test-app-state-{Guid.NewGuid()}.db3");
        try
        {
            using var repo = new AppStateRepository(dbPath);

            var result = await repo.RestoreStateAsync();

            Assert.Equal(AppStateSnapshot.Empty, result);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SaveAsync_WithEmptyValues_PersistsNullsCorrectly()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test-app-state-{Guid.NewGuid()}.db3");
        try
        {
            using var repo = new AppStateRepository(dbPath);

            var empty = AppStateSnapshot.Empty;

            await repo.SaveAsync(empty);
            var restored = await repo.RestoreStateAsync();

            Assert.Null(restored.LastViewerSourceId);
            Assert.Null(restored.StreamName);
            Assert.False(restored.IsOutputActive);
            Assert.Null(restored.LastSelectedSourceId);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SaveAsync_OverwritesPreviousState()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"test-app-state-{Guid.NewGuid()}.db3");
        try
        {
            using var repo = new AppStateRepository(dbPath);

            var first = new AppStateSnapshot("first-src", "S1", false, null);
            var second = new AppStateSnapshot("second-src", "S2", true, "sel-99");

            await repo.SaveAsync(first);
            await repo.SaveAsync(second);
            var restored = await repo.RestoreStateAsync();

            Assert.Equal("second-src", restored.LastViewerSourceId);
            Assert.Equal("S2", restored.StreamName);
            Assert.True(restored.IsOutputActive);
            Assert.Equal("sel-99", restored.LastSelectedSourceId);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}

using NdiForAndroid.Data;
using NdiForAndroid.Features.Settings.Models;
using Xunit;

namespace NdiForAndroid.Tests.Data;

/// <summary>
/// Schema-level guards for <see cref="NdiDatabase"/>. These would have caught the
/// "table ... has more than one primary key" crash: sqlite-net-pcl rejects composite
/// primary keys declared via multiple [PrimaryKey] attributes, so InitAsync's
/// CreateTableAsync calls throw at startup / first write.
/// </summary>
public class NdiDatabaseSchemaTests
{
    private static string TempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"test-ndi-db-{Guid.NewGuid()}.db3");

    [Fact]
    public async Task InitAsync_CreatesEveryTable_WithoutThrowing()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);

            // Faults if any entity declares a composite primary key (multiple [PrimaryKey]).
            await db.InitAsync();
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsDiscoveryServer_WithoutThrowing()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);
            var settings = NdiSettingsSnapshot.CreateDefault() with
            {
                UpdatedAtEpochMillis = 1234,
                DiscoveryServers = new[]
                {
                    new DiscoveryServerPreference("192.168.1.50", 5959, true, 0, "Studio server"),
                },
            };

            // Exact path that crashed the app: Settings → save → SaveSettingsAsync.
            await db.SaveSettingsAsync(settings);

            var restored = await db.GetSettingsAsync();
            var server = Assert.Single(restored.DiscoveryServers);
            Assert.Equal("192.168.1.50", server.Host);
            Assert.Equal(5959, server.Port);
            Assert.Equal("Studio server", server.DisplayName);
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SaveSourceServerCrossref_SamePairIsIdempotent_DifferentServerAddsRow()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);

            await db.SaveSourceServerCrossrefAsync(new CachedSourceCrossrefEntity
            {
                SourceId = "src-1", ServerId = "srv-A", FirstSeenViaServerAtEpochMillis = 1,
            });
            // Same (source, server) pair: the surrogate key collapses to one row (replace, not duplicate).
            await db.SaveSourceServerCrossrefAsync(new CachedSourceCrossrefEntity
            {
                SourceId = "src-1", ServerId = "srv-A", FirstSeenViaServerAtEpochMillis = 2,
            });
            await db.SaveSourceServerCrossrefAsync(new CachedSourceCrossrefEntity
            {
                SourceId = "src-1", ServerId = "srv-B", FirstSeenViaServerAtEpochMillis = 3,
            });

            var afterInserts = await db.GetSourceServerCrossrefsAsync("src-1");
            Assert.Equal(2, afterInserts.Count);
            Assert.Equal(2, afterInserts.Single(c => c.ServerId == "srv-A").FirstSeenViaServerAtEpochMillis);

            await db.DeleteSourceServerCrossrefAsync("src-1", "srv-A");
            var afterDelete = await db.GetSourceServerCrossrefsAsync("src-1");
            Assert.Equal("srv-B", Assert.Single(afterDelete).ServerId);
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}

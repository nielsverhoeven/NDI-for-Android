using NdiForAndroid.Data;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Sources.Models;
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

    [Fact]
    public async Task SavePtzOverrideAsync_RoundTrips_AndSurvivesDiscoveryUpsert()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);
            var source = new NdiSource("src-1", "Cam 1", "192.168.1.10", true, 1000);
            await db.UpsertSourceAsync(source);

            await db.SavePtzOverrideAsync("src-1", "192.168.1.99", 1234);

            var afterSave = Assert.Single(await db.GetSourcesAsync());
            Assert.Equal("192.168.1.99", afterSave.PtzOverrideHost);
            Assert.Equal(1234, afterSave.PtzOverridePort);

            // Simulate a later discovery poll rebuilding the same source with the PTZ fields left null.
            await db.UpsertSourceAsync(source with { IsAvailable = false });

            var afterDiscovery = Assert.Single(await db.GetSourcesAsync());
            Assert.Equal("192.168.1.99", afterDiscovery.PtzOverrideHost);
            Assert.Equal(1234, afterDiscovery.PtzOverridePort);
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    // #339 follow-up: the VISCA endpoint override used to be keyed purely by SourceId (host:port),
    // so when the sender's IP changed, discovery created a brand-new row and the saved override
    // did not follow. It is now resolved by the stable NDI source NAME first, falling back to the
    // address (SourceId) match.
    [Fact]
    public async Task SavePtzOverrideAsync_ThenDiscoveryGivesTheSourceANewAddress_OverrideFollowsTheName()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);
            var original = new NdiSource("192.168.1.10:5961", "Studio Cam", "192.168.1.10:5961", true, 1000);
            await db.UpsertSourceAsync(original);
            await db.SavePtzOverrideAsync(original.SourceId, "10.0.0.50", 5678);

            // Simulate the sender's IP changing: discovery now reports the SAME name at a
            // different address, i.e. a brand-new SourceId with no history of its own.
            var afterIpChange = new NdiSource("192.168.1.11:5961", "Studio Cam", "192.168.1.11:5961", true, 2000);
            await db.UpsertSourceAsync(afterIpChange);

            var rows = await db.GetSourcesAsync();
            var newRow = Assert.Single(rows, r => r.SourceId == afterIpChange.SourceId);
            Assert.Equal("10.0.0.50", newRow.PtzOverrideHost);
            Assert.Equal(5678, newRow.PtzOverridePort);
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SavePtzOverrideAsync_UpdatesEveryCachedRowSharingTheSameName()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);
            // Two cached rows for the same camera under different addresses (e.g. the old row has
            // not expired yet after an IP change).
            await db.UpsertSourceAsync(new NdiSource("old-addr:5961", "Studio Cam", "old-addr:5961", false, 1000));
            await db.UpsertSourceAsync(new NdiSource("new-addr:5961", "Studio Cam", "new-addr:5961", true, 2000));

            await db.SavePtzOverrideAsync("new-addr:5961", "10.0.0.50", 5678);

            var rows = await db.GetSourcesAsync();
            Assert.All(rows, r =>
            {
                Assert.Equal("10.0.0.50", r.PtzOverrideHost);
                Assert.Equal(5678, r.PtzOverridePort);
            });
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task SavePtzOverrideAsync_WithNoCachedRowYet_IsANoOpWithoutThrowing()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);

            // No row exists yet for this SourceId (e.g. a deep-linked source not yet discovered).
            // This matches the pre-#339-follow-up behaviour: there is nothing to key the update
            // by (neither an address row nor a name), so it is a harmless no-op, not a throw.
            await db.SavePtzOverrideAsync("not-yet-cached:5961", "10.0.0.50", 5678);

            Assert.Empty(await db.GetSourcesAsync());
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task UpsertSourceAsync_DifferentNameAtSameAddressHistory_DoesNotInheritAnUnrelatedOverride()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);
            await db.UpsertSourceAsync(new NdiSource("cam-a:5961", "Camera A", "cam-a:5961", true, 1000));
            await db.SavePtzOverrideAsync("cam-a:5961", "10.0.0.50", 5678);

            // A different camera name should never inherit Camera A's override, even via the
            // address-fallback path (it is a different SourceId with no history of its own).
            await db.UpsertSourceAsync(new NdiSource("cam-b:5961", "Camera B", "cam-b:5961", true, 2000));

            var camB = Assert.Single(await db.GetSourcesAsync(), r => r.SourceId == "cam-b:5961");
            Assert.Null(camB.PtzOverrideHost);
            Assert.Null(camB.PtzOverridePort);
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task UpsertSourceAsync_WithoutPtzOverride_DefaultsToNull()
    {
        var dbPath = TempDbPath();
        NdiDatabase? db = null;
        try
        {
            db = new NdiDatabase(dbPath);
            var source = new NdiSource("src-2", "Cam 2", "192.168.1.11", true, 2000);

            await db.UpsertSourceAsync(source);

            var saved = Assert.Single(await db.GetSourcesAsync());
            Assert.Null(saved.PtzOverrideHost);
            Assert.Null(saved.PtzOverridePort);
        }
        finally
        {
            db?.Dispose();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}

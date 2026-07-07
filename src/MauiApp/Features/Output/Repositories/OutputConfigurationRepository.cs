using NdiForAndroid.Data;

namespace NdiForAndroid.Features.Output.Repositories;

/// <summary>
/// SQLite-backed output configuration persistence — wraps the forward-declared
/// output_configuration table in the shared ndi.db3 database (issue #240).
/// String ↔ enum mapping is delegated to <see cref="OutputConfigurationMapping"/>
/// in Core so it stays unit-testable without this Android-targeted assembly.
/// </summary>
public sealed class OutputConfigurationRepository : IOutputConfigurationRepository
{
    private readonly NdiDatabase _database;

    public OutputConfigurationRepository(NdiDatabase database)
    {
        _database = database;
    }

    public async Task<OutputConfiguration?> GetAsync()
    {
        var entity = await _database.GetOutputConfigurationAsync();
        if (entity is null)
            return null;

        return new OutputConfiguration(
            entity.PreferredStreamName,
            OutputConfigurationMapping.ParseInputKind(entity.LastInputKind),
            entity.CaptureMicrophone);
    }

    public async Task SaveAsync(OutputConfiguration config)
    {
        // Preserve the untouched legacy columns (LastSourceId, retry settings)
        // by updating the existing row when present.
        var entity = await _database.GetOutputConfigurationAsync()
            ?? new OutputConfigurationEntity { Id = 1 };

        entity.PreferredStreamName = config.PreferredStreamName;
        entity.LastInputKind = OutputConfigurationMapping.ToStorageString(config.InputKind);
        entity.CaptureMicrophone = config.CaptureMicrophone;

        await _database.SaveOutputConfigurationAsync(entity);
    }
}

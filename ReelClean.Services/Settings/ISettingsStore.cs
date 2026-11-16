namespace ReelClean.Services.Settings;

public interface ISettingsStore
{
    void Apply(ReelCleanOptions options);

    Task SaveAsync(ReelCleanOptions options, CancellationToken cancellationToken = default);
}

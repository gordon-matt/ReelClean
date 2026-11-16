using System.Text.Json;

namespace ReelClean.Services.Settings;

public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public void Apply(ReelCleanOptions options)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var stored = JsonSerializer.Deserialize<ReelCleanOptions>(File.ReadAllText(path), Json);
        if (stored is null)
        {
            return;
        }

        options.Tmdb.ApiKey = stored.Tmdb?.ApiKey ?? string.Empty;
        options.ScanSubdirectories = stored.ScanSubdirectories;
        options.IncludeHiddenFiles = stored.IncludeHiddenFiles;
        options.IncludeEpisodeTitles = stored.IncludeEpisodeTitles;
        options.TemplateName = string.IsNullOrWhiteSpace(stored.TemplateName) ? Naming.RenameTemplates.PlexName : stored.TemplateName;
        options.CustomTemplate = stored.CustomTemplate ?? string.Empty;
    }

    public async Task SaveAsync(ReelCleanOptions options, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, options, Json, cancellationToken);
    }
}

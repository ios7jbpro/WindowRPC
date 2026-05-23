using System.Text.Json;
using WindowRPC.Models;

namespace WindowRPC.Services;

internal sealed class ConfigurationService : IDisposable
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private FileSystemWatcher? _watcher;

    public ConfigurationService(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        Current = LoadConfiguration();
    }

    public string BaseDirectory { get; }

    public RuntimeConfiguration Current { get; private set; }

    public event EventHandler? ConfigurationChanged;

    public void Start()
    {
        EnsureDefaultFiles();

        _watcher = new FileSystemWatcher(BaseDirectory)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnWatchedFileChanged;
        _watcher.Created += OnWatchedFileChanged;
        _watcher.Renamed += OnWatchedFileChanged;
    }

    public void Reload()
    {
        Current = LoadConfiguration();
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveOverrides(IReadOnlyDictionary<string, OverrideEntry> overrides)
    {
        var overridesPath = Path.Combine(BaseDirectory, "overrides.json");
        File.WriteAllText(overridesPath, JsonSerializer.Serialize(overrides, _jsonOptions));
        Reload();
    }

    public void SaveDefaultSettings(DefaultSettings settings)
    {
        var defaultPath = Path.Combine(BaseDirectory, "default.json");
        var document = new DefaultSettingsDocument
        {
            Default = settings
        };

        File.WriteAllText(defaultPath, JsonSerializer.Serialize(document, _jsonOptions));
        Reload();
    }

    public void Dispose()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnWatchedFileChanged;
        _watcher.Created -= OnWatchedFileChanged;
        _watcher.Renamed -= OnWatchedFileChanged;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        var fileName = Path.GetFileName(e.FullPath);
        if (!string.Equals(fileName, "default.json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fileName, "overrides.json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Reload();
        }
        catch
        {
            // Keep the last good configuration if a save is incomplete.
        }
    }

    private void EnsureDefaultFiles()
    {
        var defaultPath = Path.Combine(BaseDirectory, "default.json");
        if (!File.Exists(defaultPath))
        {
            var defaultDocument = new DefaultSettingsDocument();
            File.WriteAllText(defaultPath, JsonSerializer.Serialize(defaultDocument, _jsonOptions));
        }

        var overridesPath = Path.Combine(BaseDirectory, "overrides.json");
        if (!File.Exists(overridesPath))
        {
            var starter = new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["Notepad"] = new()
                {
                    Logo = "rpc_icon",
                    Details = "Currently coding",
                    State = "appname"
                }
            };

            File.WriteAllText(overridesPath, JsonSerializer.Serialize(starter, _jsonOptions));
        }
    }

    private RuntimeConfiguration LoadConfiguration()
    {
        EnsureDefaultFiles();

        var defaultPath = Path.Combine(BaseDirectory, "default.json");
        var overridesPath = Path.Combine(BaseDirectory, "overrides.json");

        var defaultDocument = ReadJson<DefaultSettingsDocument>(defaultPath) ?? new DefaultSettingsDocument();
        var overrideEntries = ReadJson<Dictionary<string, OverrideEntry>>(overridesPath)
            ?? new Dictionary<string, OverrideEntry>(StringComparer.OrdinalIgnoreCase);

        var sortedRules = overrideEntries
            .Select(pair => new OverrideRule
            {
                Name = pair.Key,
                Entry = pair.Value
            })
            .OrderByDescending(rule => rule.Name.Length)
            .ToList();

        return new RuntimeConfiguration(defaultDocument.Default, sortedRules);
    }

    private T? ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soundboard.Core;

public sealed class ConfigMigrationResult
{
    public bool Migrated { get; init; }
    public string? SourceConfigPath { get; init; }
    public string DestConfigPath { get; init; } = "";
    public int FilesCopied { get; init; }
    public int FilesMissing { get; init; }
}

public sealed class ConfigStore
{
    public const string FileName = "config.json";
    public const string ExampleFileName = "config.example.json";
    public const string AppDataFolderName = "Soundboard";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new HotkeyModeJsonConverter(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ConfigPath { get; }
    public string ConfigDirectory => Path.GetDirectoryName(ConfigPath) ?? ".";
    public string ExeDirectory { get; }
    public string LibraryDirectory => MediaLibrary.DirectoryFor(ConfigDirectory);

    public ConfigStore(string? exeDirectory = null, string? configPath = null)
    {
        ExeDirectory = string.IsNullOrWhiteSpace(exeDirectory)
            ? PersistentExeDirectory()
            : exeDirectory;
        ConfigPath = string.IsNullOrWhiteSpace(configPath)
            ? GetDefaultConfigPath()
            : configPath;
    }

    /// <summary>
    /// Directory of the real process image. For single-file publish this is the
    /// folder containing Soundboard.exe, not the extract cache in BaseDirectory.
    /// </summary>
    public static string PersistentExeDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                return dir;
            }
        }

        return AppContext.BaseDirectory;
    }

    public static string GetAppDataDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolderName);

    public static string GetDefaultConfigPath() =>
        Path.Combine(GetAppDataDirectory(), FileName);

    /// <summary>
    /// Production store: config and library always live under %AppData%\Soundboard,
    /// so putting the exe on Desktop / OneDrive does not scatter files.
    /// </summary>
    public static ConfigStore OpenAppData(string? exeDirectory = null)
    {
        var exe = string.IsNullOrWhiteSpace(exeDirectory)
            ? PersistentExeDirectory()
            : exeDirectory;
        var appData = GetAppDataDirectory();
        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(MediaLibrary.DirectoryFor(appData));
        return new ConfigStore(exe, Path.Combine(appData, FileName));
    }

    /// <summary>
    /// If AppData has no config yet but the old exe-side <c>config.json</c> exists
    /// (upgrade from versions that stored config next to the exe), copy it and
    /// import still-present audio files into the library.
    /// </summary>
    public static ConfigMigrationResult TryMigrateFromExeDirectory(string exeDirectory, string appDataDirectory)
    {
        var destConfig = Path.Combine(appDataDirectory, FileName);
        var sourceConfig = Path.Combine(exeDirectory, FileName);
        if (File.Exists(destConfig) || !File.Exists(sourceConfig))
        {
            return new ConfigMigrationResult
            {
                Migrated = false,
                SourceConfigPath = File.Exists(sourceConfig) ? sourceConfig : null,
                DestConfigPath = destConfig
            };
        }

        Directory.CreateDirectory(appDataDirectory);
        Directory.CreateDirectory(MediaLibrary.DirectoryFor(appDataDirectory));
        File.Copy(sourceConfig, destConfig, overwrite: false);

        var store = new ConfigStore(exeDirectory, destConfig);
        var config = store.Load();
        var sourceDir = Path.GetDirectoryName(sourceConfig) ?? exeDirectory;
        var (copied, missing) = MediaLibrary.ImportReferencedFiles(
            config, appDataDirectory, sourceDir, exeDirectory);
        store.Save(config);

        return new ConfigMigrationResult
        {
            Migrated = true,
            SourceConfigPath = sourceConfig,
            DestConfigPath = destConfig,
            FilesCopied = copied,
            FilesMissing = missing
        };
    }

    public AppConfig LoadOrCreate()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LibraryDirectory);

        if (!File.Exists(ConfigPath))
        {
            var example = Path.Combine(ExeDirectory, ExampleFileName);
            if (File.Exists(example))
            {
                File.Copy(example, ConfigPath, overwrite: false);
                var seeded = Load();
                MediaLibrary.ImportReferencedFiles(seeded, ConfigDirectory, ExeDirectory, ConfigDirectory);
                Save(seeded);
            }
            else
            {
                Save(AppConfig.CreateDefault());
            }
        }

        return Load();
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var created = AppConfig.CreateDefault();
            Save(created);
            return created;
        }

        var json = File.ReadAllText(ConfigPath);
        var config = Deserialize(json);
        config.Normalize();
        return config;
    }

    public void Save(AppConfig config)
    {
        config.Normalize();
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LibraryDirectory);
        var json = Serialize(config);
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, ConfigPath, overwrite: true);
        File.Delete(tmp);
    }

    public static string Serialize(AppConfig config)
    {
        config.Normalize();
        return JsonSerializer.Serialize(config, JsonOptions) + Environment.NewLine;
    }

    public static AppConfig Deserialize(string json)
    {
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions)
                     ?? throw new InvalidDataException("配置文件为空或格式无效。");
        config.Normalize();
        return config;
    }

    public static bool IsDirectoryWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

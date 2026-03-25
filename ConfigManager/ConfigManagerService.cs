using ConfigManager.Models;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConfigManager;

public sealed class ConfigManagerService
{
    private const string ConfigFileName = "config.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string AppName => Assembly.GetEntryAssembly()?.GetName().Name ?? "AutoWindowCapture";

    public static string AppDirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppName);

    public static string ConfigFilePath => Path.Combine(AppDirectoryPath, ConfigFileName);

    private static string DefaultRecordingSaveDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), AppName);

    public AppConfig Load()
    {
        _ = Directory.CreateDirectory(AppDirectoryPath);

        if (!File.Exists(ConfigFilePath))
        {
            return CreateDefault();
        }

        try
        {
            string json = File.ReadAllText(ConfigFilePath);
            AppConfig? config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return Normalize(config ?? CreateDefault());
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save(AppConfig config)
    {
        AppConfig normalized = Normalize(config);

        _ = Directory.CreateDirectory(AppDirectoryPath);
        _ = Directory.CreateDirectory(normalized.RecordingSaveDirectory);

        string json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    private static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            RecordingSaveDirectory = DefaultRecordingSaveDirectory,
            AutoRecordingExecutableNames = [],
            RecordingCodec = RecordingCodec.H264,
            RecordingResolution = RecordingFormatOptions.ToSize(RecordingResolutionPreset.FHD_1920x1080)
        };
    }

    private static AppConfig Normalize(AppConfig config)
    {
        string configuredPath = string.IsNullOrWhiteSpace(config.RecordingSaveDirectory)
            ? DefaultRecordingSaveDirectory
            : config.RecordingSaveDirectory.Trim();

        if (!Path.IsPathRooted(configuredPath))
        {
            configuredPath = Path.Combine(AppDirectoryPath, configuredPath);
        }

        string fullPath = Path.GetFullPath(configuredPath);

        RecordingCodec normalizedCodec = Enum.IsDefined(config.RecordingCodec)
            ? config.RecordingCodec
            : RecordingCodec.H264;

        RecordingResolutionSize normalizedResolution = RecordingFormatOptions.IsSupported(config.RecordingResolution)
            ? config.RecordingResolution
            : RecordingFormatOptions.ToSize(RecordingResolutionPreset.FHD_1920x1080);

        return new AppConfig
        {
            RecordingSaveDirectory = fullPath,
            AutoRecordingExecutableNames = [.. config.AutoRecordingExecutableNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)],
            RecordingCodec = normalizedCodec,
            RecordingResolution = normalizedResolution
        };
    }
}


using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConfigManager;
using ConfigManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace ConfigPage.ViewModels;

public sealed partial class ConfigPageViewModel : ObservableObject
{
    private readonly ConfigManagerService _configManager = new();

    [ObservableProperty]
    public partial string RecordingSaveDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial RecordingCodec SelectedRecordingCodec { get; set; } = RecordingCodec.H264;

    [ObservableProperty]
    public partial RecordingResolutionPreset SelectedRecordingResolution { get; set; } = RecordingResolutionPreset.FHD_1920x1080;

    public string? SelectedExecutableName
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<string> AutoRecordingExecutableNames { get; } = [];

    public ConfigPageViewModel()
    {
        AutoRecordingExecutableNames.CollectionChanged += OnAutoRecordingExecutableNamesChanged;
    }

    partial void OnRecordingSaveDirectoryChanged(string value)
    {
        SaveConfig();
    }

    partial void OnSelectedRecordingCodecChanged(RecordingCodec value)
    {
        SaveConfig();
    }

    partial void OnSelectedRecordingResolutionChanged(RecordingResolutionPreset value)
    {
        SaveConfig();
    }

    public void LoadConfig()
    {
        lock (_configManager)
        {
            AppConfig config = _configManager.Load();
            RecordingSaveDirectory = config.RecordingSaveDirectory;
            SelectedRecordingCodec = config.RecordingCodec;
            SelectedRecordingResolution = RecordingFormatOptions.ToPreset(config.RecordingResolution);

            AutoRecordingExecutableNames.Clear();
            foreach (string executableName in config.AutoRecordingExecutableNames)
            {
                AutoRecordingExecutableNames.Add(executableName);
            }
        }
    }

    private void SaveConfig()
    {
        lock (_configManager)
        {
            AppConfig config = new()
            {
                RecordingSaveDirectory = RecordingSaveDirectory,
                AutoRecordingExecutableNames = [.. AutoRecordingExecutableNames],
                RecordingCodec = SelectedRecordingCodec,
                RecordingResolution = RecordingFormatOptions.ToSize(SelectedRecordingResolution)
            };

            _configManager.Save(config);
        }
    }

    [RelayCommand]
    private void RemoveSelectedExecutable()
    {
        if (string.IsNullOrWhiteSpace(SelectedExecutableName))
        {
            return;
        }

        _ = AutoRecordingExecutableNames.Remove(SelectedExecutableName);
        SelectedExecutableName = null;
    }

    public void SetRecordingSaveDirectory(string directoryPath)
    {
        RecordingSaveDirectory = directoryPath;
    }

    public void AddExecutableNames(IEnumerable<string> executableNames)
    {
        foreach (string executableName in executableNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {

            if (!AutoRecordingExecutableNames.Any(x => string.Equals(x, executableName, StringComparison.OrdinalIgnoreCase)))
            {
                AutoRecordingExecutableNames.Add(executableName);
            }
        }
    }

    private void OnAutoRecordingExecutableNamesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SaveConfig();
    }
}


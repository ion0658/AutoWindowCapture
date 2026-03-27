using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConfigManager.Models;

public sealed class AppConfig
{
    public string RecordingSaveDirectory { get; set; } = string.Empty;

    public List<string> AutoRecordingExecutableNames { get; set; } = [];

    public RecordingCodec RecordingCodec { get; set; } = RecordingCodec.H264;

    public RecordingResolutionSize RecordingResolution { get; set; } = RecordingFormatOptions.ToSize(RecordingResolutionPreset.FHD_1920x1080);
}

[JsonSerializable(typeof(AppConfig))]
public partial class AppConfigJsonContext : JsonSerializerContext
{
}
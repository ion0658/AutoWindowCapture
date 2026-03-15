using Windows.Graphics;

namespace ConfigManager.Models;

public enum RecordingCodec {
    H264,
    HEVC
}

public enum RecordingResolutionPreset {
    Source,
    HD_1280x720,
    FHD_1920x1080,
    UHD_3840x2160,
}

public readonly record struct RecordingResolutionSize(int Width, int Height);

public static class RecordingFormatOptions {
    public static RecordingResolutionSize ToSize(RecordingResolutionPreset preset) {
        return preset switch {
            RecordingResolutionPreset.Source => new RecordingResolutionSize(0, 0),
            RecordingResolutionPreset.HD_1280x720 => new RecordingResolutionSize(1280, 720),
            RecordingResolutionPreset.FHD_1920x1080 => new RecordingResolutionSize(1920, 1080),
            RecordingResolutionPreset.UHD_3840x2160 => new RecordingResolutionSize(3840, 2160),
            _ => new RecordingResolutionSize(0, 0)
        };
    }

    public static SizeInt32 ToSizeUInt32(RecordingResolutionPreset preset) {
        RecordingResolutionSize size = ToSize(preset);
        return new(size.Width, size.Height);
    }

    public static RecordingResolutionPreset ToPreset(RecordingResolutionSize size) {
        return size switch {
            { Width: 0, Height: 0 } => RecordingResolutionPreset.Source,
            { Width: 1280, Height: 720 } => RecordingResolutionPreset.HD_1280x720,
            { Width: 1920, Height: 1080 } => RecordingResolutionPreset.FHD_1920x1080,
            { Width: 3840, Height: 2160 } => RecordingResolutionPreset.UHD_3840x2160,
            _ => RecordingResolutionPreset.Source
        };
    }

    public static bool IsSupported(RecordingResolutionSize size) {
        return size is { Width: 0, Height: 0 } or
        { Width: 1280, Height: 720 } or
        { Width: 1920, Height: 1080 } or
        { Width: 3840, Height: 2160 };
    }
}


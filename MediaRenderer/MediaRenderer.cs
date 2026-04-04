using WindowAudioCaptureNative;
using ConfigManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Channels;
using System.Threading.Tasks;
using Windows.Devices.PointOfService;
using Windows.Graphics.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MediaRenderer;

public sealed partial class MediaRenderer : IDisposable
{

    private MediaStreamSource? _mediaStreamSource = null;
    private MediaStreamSourceSampleRequest? _pendingVideoRequest = null;
    private MediaStreamSourceSampleRequestDeferral? _pendingVideoDeferral = null;
    private MediaStreamSourceSampleRequest? _pendingAudioRequest = null;
    private MediaStreamSourceSampleRequestDeferral? _pendingAudioDeferral = null;

    private readonly WindowAudioCapture _audioCapture = new();
    private readonly Channel<MediaStreamSample> _videoSampleChannel = Channel.CreateUnbounded<MediaStreamSample>();
    private readonly Channel<MediaStreamSample> _audioSampleChannel = Channel.CreateUnbounded<MediaStreamSample>();
    private readonly object _frameLock = new();
    private readonly object _audioLock = new();
    private IRandomAccessStream? _outputStream = null;
    private readonly Task? _transcodeTask = null;
    private TimeSpan _firstFrameTime = TimeSpan.Zero;
    private TimeSpan _audioTime = TimeSpan.Zero;
    VideoStreamDescriptor? _videoStreamDescriptor;
    AudioStreamDescriptor? _audioStreamDescriptor;

    public MediaRenderer(GraphicsCaptureItem item, int proc_id, string process_name, AppConfig config)
    {
        _transcodeTask = Start(item, proc_id, process_name, config);
    }

    private static VideoEncodingQuality GetVideoEncodingQuality(SizeUInt32 size)
    {
        return size.Height switch
        {
            >= 2160 => VideoEncodingQuality.Uhd2160p,
            >= 1080 => VideoEncodingQuality.HD1080p,
            _ => VideoEncodingQuality.Auto,
        };
    }

    private static MediaEncodingProfile createEncodingProfile(RecordingCodec codec, SizeUInt32 size, AudioFormat audioFormat)
    {
        VideoEncodingQuality quality = GetVideoEncodingQuality(size);
        MediaEncodingProfile profile = codec switch
        {
            RecordingCodec.H264 => MediaEncodingProfile.CreateMp4(quality),
            RecordingCodec.HEVC => MediaEncodingProfile.CreateHevc(quality),
            RecordingCodec.AV1 => MediaEncodingProfile.CreateAv1(quality),
            _ => throw new NotImplementedException(),
        };
        uint audioBitrate = Math.Clamp((uint)audioFormat.Channels * 192_000u, 96_000u, 384_000u);
        profile.Audio = AudioEncodingProperties.CreateAac(
            (uint)audioFormat.SampleRate,
            (uint)audioFormat.Channels,
            audioBitrate);
        profile.Video!.Width = ToEvenValue(size.Width);
        profile.Video!.Height = ToEvenValue(size.Height);
        profile.Video!.FrameRate.Numerator = 60;
        profile.Video!.FrameRate.Denominator = 1;
        return profile;
    }

    private static uint ToEvenValue(uint value)
    {
        return (value / 2) * 2;
    }

    private async Task Start(GraphicsCaptureItem item, int proc_id, string process_name, AppConfig config)
    {
        SizeUInt32 dst_size = new SizeUInt32((uint)config.RecordingResolution.Width, (uint)config.RecordingResolution.Height);
        Debug.WriteLine($"Starting MediaRenderer for process '{process_name}' (PID: {proc_id}) with resolution {dst_size.Width}x{dst_size.Height} and codec {config.RecordingCodec}");

        _audioCapture.AudioDataReceived += OnPCMArrived;
        _audioCapture.StartCapture((uint)proc_id);
        var audio_format = _audioCapture.CaptureFormat;

        VideoEncodingProperties sourceVideoProps = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)item.Size.Width, (uint)item.Size.Height);
        _videoStreamDescriptor = new(sourceVideoProps);

        AudioEncodingProperties sourceAudioProps = AudioEncodingProperties.CreatePcm(
            (uint)audio_format.SampleRate,
            (uint)audio_format.Channels,
            (uint)audio_format.BitsPerSample);
        _audioStreamDescriptor = new(sourceAudioProps);

        _mediaStreamSource = new MediaStreamSource(_videoStreamDescriptor, _audioStreamDescriptor)
        {
            BufferTime = TimeSpan.Zero,
            IsLive = true,
        };
        _mediaStreamSource.Starting += OnMediaStreamSourceStarting;
        _mediaStreamSource.SampleRequested += OnSampleRequested;

        var encodingProfile = createEncodingProfile(config.RecordingCodec, dst_size, audio_format);

        string startDate = DateTime.Now.ToString("yyyy-MM-dd");
        string startTime = DateTime.Now.ToString("HH-mm-ss");
        string outputDir = Path.Combine(Path.Combine(config.RecordingSaveDirectory, process_name), startDate);
        _ = Directory.CreateDirectory(outputDir);
        string outputFilePath = Path.Combine(outputDir, $"{process_name}_{startTime}.mp4");
        Debug.WriteLine($"Output file path: {outputFilePath}");
        // 出力ファイルを作成してストリームを開く
        StorageFolder outputFolder = await StorageFolder.GetFolderFromPathAsync(
            Path.GetDirectoryName(outputFilePath)!);
        StorageFile outputFile = await outputFolder.CreateFileAsync(
            Path.GetFileName(outputFilePath),
            CreationCollisionOption.ReplaceExisting);
        _outputStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);

        // ハードウェアエンコーダーを有効にして MediaTranscoder を準備
        MediaTranscoder transcoder = new() { HardwareAccelerationEnabled = true };
        PrepareTranscodeResult prepareResult = await transcoder.PrepareMediaStreamSourceTranscodeAsync(
            _mediaStreamSource, _outputStream, encodingProfile);

        if (!prepareResult.CanTranscode)
        {
            throw new InvalidOperationException($"Cannot transcode: {prepareResult.FailureReason}");
        }
        await prepareResult.TranscodeAsync();
    }

    public async Task StopAsync()
    {
        Debug.WriteLine("Stopping MediaRenderer...");
        lock (_frameLock)
        {
            if (_pendingVideoRequest != null)
            {
                _pendingVideoRequest.Sample = null;
                _pendingVideoRequest = null;
                _pendingVideoDeferral?.Complete();
                _pendingVideoDeferral = null;
            }

            if (_pendingAudioRequest != null)
            {
                _pendingAudioRequest.Sample = null;
                _pendingAudioRequest = null;
                _pendingAudioDeferral?.Complete();
                _pendingAudioDeferral = null;
            }
        }
        _ = _videoSampleChannel.Writer.TryComplete();
        _ = _audioSampleChannel.Writer.TryComplete();
        if (_transcodeTask != null)
        {
            Debug.WriteLine("Waiting for transcoding to complete...");
            await _transcodeTask;
            Debug.WriteLine("Transcoding completed.");
        }
        _outputStream?.Dispose();
        _audioCapture.StopCapture();
        Debug.WriteLine("MediaRenderer stopped and resources disposed.");
    }

    public void PutFrame(Direct3D11CaptureFrame frame)
    {
        if (_firstFrameTime == TimeSpan.Zero)
        {
            _firstFrameTime = frame.SystemRelativeTime;
        }

        _videoStreamDescriptor?.EncodingProperties.Width = (uint)frame.ContentSize.Width;
        _videoStreamDescriptor?.EncodingProperties.Height = (uint)frame.ContentSize.Height;

        TimeSpan frame_time = frame.SystemRelativeTime - _firstFrameTime;
        MediaStreamSample sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, frame_time);
        lock (_frameLock)
        {
            if (_pendingVideoRequest != null)
            {
                _pendingVideoRequest.Sample = sample;
                _pendingVideoRequest = null;
                _pendingVideoDeferral?.Complete();
                _pendingVideoDeferral = null;
                return;
            }
        }
        _ = _videoSampleChannel.Writer.TryWrite(sample);
    }

    private void OnPCMArrived(byte[] pcmData, AudioFormat fmt)
    {
        if (pcmData.Length == 0 || fmt.SampleRate <= 0 || fmt.Channels <= 0 || fmt.BitsPerSample <= 0)
        {
            return;
        }

        TimeSpan timestamp;
        TimeSpan duration;
        lock (_audioLock)
        {
            timestamp = _audioTime;
            double bytesPerSecond = fmt.SampleRate * fmt.Channels * (fmt.BitsPerSample / 8.0);
            duration = TimeSpan.FromSeconds(pcmData.Length / bytesPerSecond);
            _audioTime += duration;
        }

        IBuffer buffer = pcmData.AsBuffer();
        MediaStreamSample sample = MediaStreamSample.CreateFromBuffer(buffer, timestamp);
        sample.Duration = duration;

        lock (_frameLock)
        {
            if (_pendingAudioRequest != null)
            {
                _pendingAudioRequest.Sample = sample;
                _pendingAudioRequest = null;
                _pendingAudioDeferral?.Complete();
                _pendingAudioDeferral = null;
                return;
            }
        }
        _ = _audioSampleChannel.Writer.TryWrite(sample);
    }

    private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
    {
        Debug.WriteLine("MediaStreamSource Starting");
        args.Request.SetActualStartPosition(TimeSpan.Zero);
    }

    private void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
    {
        bool isAudio = args.Request.StreamDescriptor is AudioStreamDescriptor;
        ChannelReader<MediaStreamSample> channelReader = isAudio ? _audioSampleChannel.Reader : _videoSampleChannel.Reader;
        if (channelReader.TryRead(out MediaStreamSample? sample))
        {
            args.Request.Sample = sample;
        }
        else if (channelReader.Completion.IsCompleted)
        {
            args.Request.Sample = null;
        }
        else
        {
            lock (_frameLock)
            {
                MediaStreamSourceSampleRequestDeferral deferral = args.Request.GetDeferral();
                if (isAudio)
                {
                    _pendingAudioDeferral = deferral;
                    _pendingAudioRequest = args.Request;
                }
                else
                {
                    _pendingVideoDeferral = deferral;
                    _pendingVideoRequest = args.Request;
                }
            }
        }
    }

    public void Dispose()
    {
        _ = StopAsync();
    }
}


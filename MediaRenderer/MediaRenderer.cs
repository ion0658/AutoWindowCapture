using AudioCapture;
using ConfigManager.Models;
using System;
using System.Diagnostics;
using System.IO;
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
    private MediaStreamSourceSampleRequest? _pendingRequest = null;
    private MediaStreamSourceSampleRequestDeferral? _pendingDeferral = null;

    private readonly LoopbackAudioCapture _audioCapture = new();
    private readonly Channel<MediaStreamSample> _sampleChannel = Channel.CreateUnbounded<MediaStreamSample>();
    private readonly object _frameLock = new();
    private IRandomAccessStream? _outputStream = null;
    private readonly Task? _transcodeTask = null;
    private TimeSpan _firstFrameTime = TimeSpan.Zero;

    public MediaRenderer(GraphicsCaptureItem item, int proc_id, string process_name, AppConfig config)
    {
        _transcodeTask = Start(item, proc_id, process_name, config);
    }

    private VideoEncodingQuality GetVideoEncodingQuality(SizeUInt32 size)
    {
        return size.Height switch
        {
            >= 2160 => VideoEncodingQuality.Uhd2160p,
            >= 1080 => VideoEncodingQuality.HD1080p,
            _ => VideoEncodingQuality.Auto,
        };
    }

    private async Task Start(GraphicsCaptureItem item, int proc_id, string process_name, AppConfig config)
    {
        SizeUInt32 dst_size = config.RecordingResolution switch
        {
            { Width: 0, Height: 0 } => new SizeUInt32((uint)item.Size.Width, (uint)item.Size.Height),
            { Width: var w, Height: var h } => new SizeUInt32((uint)w, (uint)h)
        };

        _audioCapture.StartCapture(proc_id, OnPCMArrived);
        var a_fmt = _audioCapture.CaptureFormat;
        VideoEncodingProperties sourceVideoProps = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)item.Size.Width, (uint)item.Size.Height);
        VideoStreamDescriptor videoDescriptor = new(sourceVideoProps);

        _mediaStreamSource = new MediaStreamSource(videoDescriptor)
        {
            BufferTime = TimeSpan.Zero,
            IsLive = true,
        };
        _mediaStreamSource.Starting += OnMediaStreamSourceStarting;
        _mediaStreamSource.SampleRequested += OnSampleRequested;

        VideoEncodingQuality quality = GetVideoEncodingQuality(dst_size);

        MediaEncodingProfile encodingProfile = config.RecordingCodec == RecordingCodec.H264
            ? MediaEncodingProfile.CreateMp4(quality)
            : MediaEncodingProfile.CreateHevc(quality);

        encodingProfile.Video!.Width = dst_size.Width;
        encodingProfile.Video!.Height = dst_size.Height;
        encodingProfile.Video!.FrameRate.Numerator = 60;
        encodingProfile.Video!.FrameRate.Denominator = 1;

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
            if (_pendingRequest != null)
            {
                _pendingRequest.Sample = null;
                _pendingRequest = null;
                _pendingDeferral?.Complete();
                _pendingDeferral = null;
            }
        }
        _ = _sampleChannel.Writer.TryComplete();
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
        TimeSpan frame_time = frame.SystemRelativeTime - _firstFrameTime;
        MediaStreamSample sample = MediaStreamSample.CreateFromDirect3D11Surface(frame.Surface, frame_time);
        lock (_frameLock)
        {
            if (_pendingRequest != null)
            {
                _pendingRequest.Sample = sample;
                _pendingRequest = null;
                _pendingDeferral?.Complete();
                _pendingDeferral = null;
                return;
            }
        }
        _ = _sampleChannel.Writer.TryWrite(sample);
    }

    private void OnPCMArrived(byte[] pcmData, AudioFormat fmt)
    {

    }

    private void OnMediaStreamSourceStarting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
    {
        Debug.WriteLine("MediaStreamSource Starting");
        args.Request.SetActualStartPosition(TimeSpan.Zero);
    }

    private void OnSampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
    {
        if (_sampleChannel.Reader.TryRead(out MediaStreamSample? sample))
        {
            args.Request.Sample = sample;
        }
        else if (_sampleChannel.Reader.Completion.IsCompleted)
        {
            args.Request.Sample = null;
        }
        else
        {
            lock (_frameLock)
            {
                _pendingDeferral = args.Request.GetDeferral();
                _pendingRequest = args.Request;
            }
        }
    }

    public void Dispose()
    {
        _ = StopAsync();
    }
}


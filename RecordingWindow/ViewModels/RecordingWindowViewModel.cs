using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ConfigManager;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowCapture;
using WindowEnumerator;
using Windows.Foundation;
using Windows.Graphics.Capture;
using AudioCapture;

namespace RecordingWindow.ViewModels;

public sealed partial class RecordingWindowViewModel : ObservableObject, IDisposable
{
    private readonly ConfigManagerService _configManager = new();
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    private CanvasSwapChain? _swapChain = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    private bool _isRecording = false;

    public string RecordButtonText => IsRecording ? "StopRecording" : "Start Recording";

    public event Action? CloseRequested;

    private readonly CanvasDevice _device;
    private readonly GraphicsCaptureItem _captureItem;
    private readonly WindowCapture.WindowCapture _capture;
    private readonly WindowInfo _windowInfo;
    private MediaRenderer.MediaRenderer? _mediaRenderer = null;

    private readonly LoopbackAudioCapture _audioCapture = new();

    public RecordingWindowViewModel(WindowInfo targetWindow, CanvasDevice device, DispatcherQueue dispatcher)
    {
        _dispatcherQueue = dispatcher;
        _windowInfo = targetWindow;
        _device = device;
        _captureItem = CaptureHelper.CreateItemForWindow(targetWindow.Handle);
        _capture = new WindowCapture.WindowCapture(_device, _captureItem);
        _capture.FrameArrived += OnFrameArrived;
        _capture.CaptureStopped += OnCaptureStopped;
        _swapChain = new CanvasSwapChain(_device, _captureItem.Size.Width, _captureItem.Size.Height, 96) ?? throw new InvalidOperationException("Failed to create swap chain.");
        if (!_audioCapture.Initialize((int)targetWindow.ProcessId, false))
        {
            Debug.WriteLine("Failed to initialize audio capture.");
        }
        _audioCapture.StartCapture(OnAudioDataArrived);
    }

    private void OnCaptureStopped()
    {
        if (SwapChain is not null)
        {
            using (CanvasDrawingSession ds = SwapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent)) { }
            SwapChain.Present();
        }

        if (IsRecording && _mediaRenderer is not null)
        {
            _ = ClickCapture();
        }
        CloseRequested?.Invoke();
    }

    private void OnFrameArrived(Direct3D11CaptureFrame frame)
    {
        if (frame is null || SwapChain is null)
        {
            return;
        }

        using (CanvasDrawingSession ds = SwapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent))
        using (CanvasBitmap bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface))
        {
            Size target_size = SwapChain.Size;
            Size source_size = bitmap.Size;

            double scale = Math.Min(target_size.Width / source_size.Width, target_size.Height / source_size.Height);

            double draw_width = source_size.Width * scale;
            double draw_height = source_size.Height * scale;

            double draw_x = (target_size.Width - draw_width) / 2;
            double draw_y = (target_size.Height - draw_height) / 2;

            ds.DrawImage(bitmap, new Rect(draw_x, draw_y, draw_width, draw_height), bitmap.Bounds, 1.0f, CanvasImageInterpolation.Cubic);
        }

        SwapChain.Present();

        if (IsRecording)
        {
            _mediaRenderer?.PutFrame(frame);
        }
    }

    private void OnAudioDataArrived(byte[] audio_data, AudioFormat fmt)
    {
        Debug.WriteLine($"Audio format: {fmt.SampleRate} Hz, {fmt.BitsPerSample} bits, {fmt.Channels} channels");
        Debug.WriteLine($"Audio data arrived: {audio_data.Length} bytes");
    }

    [RelayCommand]
    private async Task ClickCapture()
    {
        if (IsRecording && _mediaRenderer is not null)
        {
            Debug.WriteLine("Stop recording...");
            await _mediaRenderer.StopAsync();
            _mediaRenderer?.Dispose();
            _mediaRenderer = null;
        }
        else if (!IsRecording)
        {
            Debug.WriteLine("Start recording...");
            _mediaRenderer = new MediaRenderer.MediaRenderer(_captureItem, _windowInfo.ProcessName, _configManager.Load());
        }
        _ = _dispatcherQueue.TryEnqueue(() =>
        {
            IsRecording = !IsRecording;
        });
    }

    public void Dispose()
    {
        SwapChain?.Dispose();
        SwapChain = null;
        _mediaRenderer?.Dispose();
        _mediaRenderer = null;
        _capture.Dispose();
        _audioCapture.StopCapture();
        _audioCapture.Dispose();
    }
}


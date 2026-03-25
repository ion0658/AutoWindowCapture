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

namespace RecordingWindow.ViewModels;

public sealed partial class RecordingWindowViewModel : ObservableObject, IDisposable
{
    private readonly ConfigManagerService _configManager = new();
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    public partial CanvasSwapChain? SwapChain { get; set; } = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    public partial bool IsRecording { get; set; } = false;

    public string RecordButtonText => IsRecording ? "StopRecording" : "Start Recording";

    public event Action? CloseRequested;

    private readonly CanvasDevice _device;
    private readonly GraphicsCaptureItem _captureItem;
    private readonly WindowCapture.WindowCapture _capture;
    private readonly WindowInfo _windowInfo;
    private MediaRenderer.MediaRenderer? _mediaRenderer = null;

    public RecordingWindowViewModel(WindowInfo targetWindow, bool recOnStart, CanvasDevice device, DispatcherQueue dispatcher)
    {
        _dispatcherQueue = dispatcher;
        _windowInfo = targetWindow;
        _device = device;
        _captureItem = CaptureHelper.CreateItemForWindow(targetWindow.Handle);
        _capture = new WindowCapture.WindowCapture(_device, _captureItem);
        _capture.FrameArrived += OnFrameArrived;
        _capture.CaptureStopped += OnCaptureStopped;
        SwapChain = new CanvasSwapChain(_device, _captureItem.Size.Width, _captureItem.Size.Height, 96) ?? throw new InvalidOperationException("Failed to create swap chain.");
        if (recOnStart)
        {
            _dispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(5_000); // Wait for the window to be ready, otherwise the recording will be black.
                await ClickCapture();
            });
        }
    }

    private void OnCaptureStopped()
    {

        if (SwapChain is not null)
        {
            using (CanvasDrawingSession ds = SwapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent)) { }
            SwapChain.Present();
        }
        _dispatcherQueue.TryEnqueue(async () =>
        {
            if (IsRecording && _mediaRenderer is not null)
            {
                await _mediaRenderer.StopAsync();
            }
            CloseRequested?.Invoke();
        });
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

            ds.DrawImage(bitmap, new Rect(draw_x, draw_y, draw_width, draw_height), bitmap.Bounds, 1.0f, CanvasImageInterpolation.NearestNeighbor);
        }

        SwapChain.Present();

        if (IsRecording)
        {
            _mediaRenderer?.PutFrame(frame);
        }
    }

    [RelayCommand]
    public async Task ClickCapture()
    {
        _ = _dispatcherQueue.TryEnqueue(async () =>
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
                _mediaRenderer = new MediaRenderer.MediaRenderer(_captureItem, (int)_windowInfo.ProcessId, _windowInfo.ProcessName, _configManager.Load());
            }

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
    }
}


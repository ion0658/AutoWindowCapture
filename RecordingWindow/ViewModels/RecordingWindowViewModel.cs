using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Controls;
using System;
using WindowCapture;
using WindowEnumerator;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace RecordingWindow.ViewModels;

public sealed partial class RecordingWindowViewModel : ObservableObject, IDisposable {

    [ObservableProperty]
    private CanvasSwapChain? _swapChain = null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    private bool _isRecording = false;

    public string RecordButtonText => IsRecording ? "StopRecording" : "Start Recording";

    private readonly CanvasDevice _device;
    private readonly GraphicsCaptureItem _captureItem;
    private readonly WindowCapture.WindowCapture _capture;

    public RecordingWindowViewModel(WindowInfo targetWindow, CanvasDevice device) {
        _device = device;
        _captureItem = CaptureHelper.CreateItemForWindow(targetWindow.Handle);
        _capture = new WindowCapture.WindowCapture(_device, _captureItem);
        _capture.FrameArrived += OnFrameArrived;
        _capture.CaptureStopped += OnCaptureStopped;
        _swapChain = new CanvasSwapChain(_device, _captureItem.Size.Width, _captureItem.Size.Height, 96) ?? throw new InvalidOperationException("Failed to create swap chain.");
    }

    private void OnCaptureStopped() {
        if (SwapChain is null) {
            return;
        }

        using (CanvasDrawingSession ds = SwapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent)) { }
        SwapChain.Present();
    }

    private void OnFrameArrived(Direct3D11CaptureFrame frame) {
        if (frame is null || SwapChain is null) {
            return;
        }

        using (CanvasDrawingSession ds = SwapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent))
        using (CanvasBitmap bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface)) {
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
    }

    [RelayCommand]
    private void ClickCapture() {
        IsRecording = !IsRecording;
    }

    public void Dispose() {
        _capture.Dispose();
        SwapChain?.Dispose();
    }
}

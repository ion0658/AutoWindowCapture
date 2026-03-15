using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Graphics.Canvas;
using System;
using System.Diagnostics;
using WindowCapture;
using WindowEnumerator;
using Windows.Graphics.Capture;

namespace RecordingWindow.ViewModels;

public sealed partial class RecordingWindowViewModel : ObservableObject, IDisposable {

    [ObservableProperty]
    private CanvasSwapChain? _swapChain = null;

    private readonly CanvasDevice _device;
    private readonly GraphicsCaptureItem _captureItem;
    private readonly WindowCapture.WindowCapture _capture;

    public RecordingWindowViewModel(WindowInfo targetWindow, CanvasDevice device) {
        _device = device;
        _captureItem = CaptureHelper.CreateItemForWindow(targetWindow.Handle);
        _capture = new WindowCapture.WindowCapture(_device, _captureItem);
        _capture.FrameArrived += OnFrameArrived;
        _capture.CaptureStopped += OnCaptureStopped;
        _swapChain = new CanvasSwapChain(_device, (float)_captureItem.Size.Width, (float)_captureItem.Size.Height, 96) ?? throw new InvalidOperationException("Failed to create swap chain.");
    }

    private void OnCaptureStopped() {
        if (SwapChain is null) {
            return;
        }

        using (var ds = SwapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent)) { }
        SwapChain.Present();
    }

    private void OnFrameArrived(Direct3D11CaptureFrame frame) {
        if (frame is null || SwapChain is null) {
            return;
        }

        using (var ds = SwapChain.CreateDrawingSession(Microsoft.UI.Colors.Transparent))
        using (var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface)) {
            var target_size = SwapChain.Size;
            var source_size = bitmap.Size;

            var scale = Math.Min(target_size.Width / source_size.Width, target_size.Height / source_size.Height);

            var draw_width = source_size.Width * scale;
            var draw_height = source_size.Height * scale;

            var draw_x = (target_size.Width - draw_width) / 2;
            var draw_y = (target_size.Height - draw_height) / 2;

            ds.DrawImage(bitmap, new Windows.Foundation.Rect(draw_x, draw_y, draw_width, draw_height), bitmap.Bounds, 1.0f, CanvasImageInterpolation.Cubic);
        }

        SwapChain.Present();
    }

    public void Dispose() {
        _capture.Dispose();
        SwapChain?.Dispose();
    }
}

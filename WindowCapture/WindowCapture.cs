using System;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace WindowCapture;

public delegate void FrameArrivedHandler(Direct3D11CaptureFrame frame);
public delegate void CaptureStoppedHandler();

public sealed class WindowCapture : IDisposable
{
    private static readonly DirectXPixelFormat PIX_FORMAT = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private static readonly int NUM_OF_BUFFERS = 2;

    private readonly GraphicsCaptureItem _captureItem;
    private readonly Direct3D11CaptureFramePool? _framePool;
    private readonly GraphicsCaptureSession? _session;
    private readonly IDirect3DDevice _device;
    private SizeInt32 _lastFrameSize = new();

    public event FrameArrivedHandler? FrameArrived;
    public event CaptureStoppedHandler? CaptureStopped;

    public WindowCapture(IDirect3DDevice device, GraphicsCaptureItem item)
    {
        _device = device;
        _captureItem = item;

        _captureItem.Closed += (s, e) => StopCapture();
        _captureItem.Closed += (s, e) => CaptureStopped?.Invoke();

        _framePool = Direct3D11CaptureFramePool.Create(_device, PIX_FORMAT, NUM_OF_BUFFERS, item.Size) ?? throw new InvalidOperationException("Failed to create frame pool.");
        _framePool.FrameArrived += OnFrameArrived;
        _session = _framePool.CreateCaptureSession(_captureItem) ?? throw new InvalidOperationException("Failed to create capture session.");
        _session.IsBorderRequired = false;
        _session.StartCapture();
    }

    private void StopCapture()
    {
        _session?.Dispose();
        _framePool?.Dispose();
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        using Direct3D11CaptureFrame frame = sender.TryGetNextFrame();
        if (frame == null)
        {
            return;
        }
        if (frame.ContentSize != _lastFrameSize)
        {
            _lastFrameSize = frame.ContentSize;
            _framePool?.Recreate(_device, PIX_FORMAT, NUM_OF_BUFFERS, _lastFrameSize);
            return;
        }

        FrameArrived?.Invoke(frame);
    }

    public void Dispose()
    {
        StopCapture();
    }

}


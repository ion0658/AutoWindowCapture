using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Win32;
using Windows.Win32.System.WinRT.Graphics.Capture;

namespace WindowCapture;

public static class CaptureHelper
{
    public static GraphicsCaptureItem CreateItemForWindow(long handle)
    {
        Windows.Win32.Foundation.HWND hwnd = new((IntPtr)handle);
        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid window handle.", nameof(hwnd));
        }
        if (!PInvoke.IsWindow(hwnd))
        {
            throw new ArgumentException("Invalid window handle.", nameof(hwnd));
        }

        Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        IGraphicsCaptureItemInterop interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        unsafe
        {
            interop.CreateForWindow(hwnd, &GraphicsCaptureItemGuid, out object? itemPointer);
            IntPtr pUnknown = Marshal.GetIUnknownForObject(itemPointer);
            try
            {
                return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(pUnknown);
            }
            finally
            {
                _ = Marshal.Release(pUnknown);
            }
        }
    }

}


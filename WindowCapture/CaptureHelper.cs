using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using WinRT;

namespace WindowCapture;

public static class CaptureHelper {
    static readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    interface IGraphicsCaptureItemInterop {
        [PreserveSig]
        int CreateForWindow(IntPtr hWnd, [In] in Guid iid, out IntPtr result);
    }

    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd) {
        if (hwnd == IntPtr.Zero) {
            throw new ArgumentException("Invalid window handle.", nameof(hwnd));
        }
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var hr = interop.CreateForWindow(hwnd, in GraphicsCaptureItemGuid, out IntPtr itemPointer);
        if (hr < 0 || itemPointer == IntPtr.Zero) {
            throw new InvalidOperationException("Failed to create GraphicsCaptureItem for the specified window.");
        }
        var item = MarshalGeneric<GraphicsCaptureItem>.FromAbi(itemPointer);
        Marshal.Release(itemPointer);
        if (item == null) {
            throw new InvalidOperationException("Failed to create GraphicsCaptureItem for the specified window.");
        }
        return item;
    }

}

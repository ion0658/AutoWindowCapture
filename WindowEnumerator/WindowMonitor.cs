using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WindowEnumerator;

public delegate void WindowInfoChangedHandler(object? sender, WindowInfo window);

public sealed class WindowInfo : IEquatable<WindowInfo> {
    public WindowInfo(IntPtr handle, string title, uint processId, string processName) {
        Handle = handle;
        Title = title;
        ProcessId = processId;
        ProcessName = processName;
    }

    public WindowInfo(IntPtr handle) {
        Handle = handle;
        Title = string.Empty;
        ProcessId = 0;
        ProcessName = string.Empty;
    }

    public IntPtr Handle { get; }

    public string Title { get; }

    public uint ProcessId { get; }

    public string ProcessName { get; }

    public string HandleDisplay => $"0x{Handle.ToInt64():X}";

    public override string ToString() {
        if (string.IsNullOrWhiteSpace(ProcessName)) {
            return $"{Title} ({HandleDisplay})";
        }

        return $"{Title} - {ProcessName} ({HandleDisplay})";
    }

    public bool Equals(WindowInfo? other) {
        if (other is null) {
            return false;
        }

        return Handle == other.Handle
            && ProcessId == other.ProcessId
            && string.Equals(Title, other.Title, StringComparison.Ordinal)
            && string.Equals(ProcessName, other.ProcessName, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => obj is WindowInfo other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Handle, Title, ProcessId, ProcessName);
}

public sealed class WindowMonitor : IDisposable {
    private readonly NativeMethods.WinEventProc _winEventProc;
    private readonly List<uint> _hook_targets = [
        NativeMethods.EVENT_OBJECT_SHOW,
        NativeMethods.EVENT_OBJECT_HIDE
    ];

    public WindowMonitor() {
        _winEventProc = OnWinEvent;
        foreach (var target in _hook_targets) {
            RegisterHook(target);
        }
    }


    public event WindowInfoChangedHandler? WindowAdded;
    public event WindowInfoChangedHandler? WindowRemoved;

    public void Stop() {
        foreach (var target in _hook_targets) {
            NativeMethods.UnhookWinEvent(RegisterHook(target));
        }
    }

    public IReadOnlyList<WindowInfo> EnumerateWindows() {
        var windows = new List<WindowInfo>();

        _ = NativeMethods.EnumWindows((windowHandle, _) => {
            var windowInfo = TryCreateWindowInfo(windowHandle);
            if (windowInfo is null) {
                return true;
            }

            windows.Add(windowInfo);
            return true;
        }, IntPtr.Zero);

        return windows
            .OrderBy(static window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static window => window.Handle)
            .ToArray();
    }

    public void Dispose() {
        Stop();
    }

    private WindowInfo? TryCreateWindowInfo(IntPtr windowHandle) {
        if (windowHandle == IntPtr.Zero) {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0) {
            return null;
        }

        if (!NativeMethods.IsWindowVisible(windowHandle)) {
            return null;
        }

        if (NativeMethods.GetParent(windowHandle) != IntPtr.Zero) {
            return null;
        }

        if (NativeMethods.GetAncestor(windowHandle, NativeMethods.GA_ROOT) != windowHandle) {
            return null;
        }

        if (NativeMethods.GetWindow(windowHandle, NativeMethods.GW_OWNER) != IntPtr.Zero) {
            return null;
        }

        var style = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GWL_STYLE).ToInt64();
        if ((style & NativeMethods.WS_CHILD) == NativeMethods.WS_CHILD) {
            return null;
        }

        var titleLength = NativeMethods.GetWindowTextLength(windowHandle);
        if (titleLength <= 0) {
            return null;
        }

        var builder = new StringBuilder(titleLength + 1);
        _ = NativeMethods.GetWindowText(windowHandle, builder, builder.Capacity);

        var title = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(title)) {
            return null;
        }

        var processName = GetProcessName(processId);
        return new WindowInfo(windowHandle, title, processId, processName);
    }

    private IntPtr RegisterHook(uint eventId) {
        var hook = NativeMethods.SetWinEventHook(
            eventId,
            eventId,
            IntPtr.Zero,
            _winEventProc,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (hook == IntPtr.Zero) {
            throw new InvalidOperationException($"WinEvent hook の登録に失敗しました: 0x{eventId:X}");
        }

        return hook;
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime) {
        if (hwnd == IntPtr.Zero) {
            return;
        }

        if (idObject != NativeMethods.OBJID_WINDOW || idChild != NativeMethods.CHILDID_SELF) {
            return;
        }

        if (eventType == NativeMethods.EVENT_OBJECT_CREATE || eventType == NativeMethods.EVENT_OBJECT_SHOW) {
            var windowInfo = TryCreateWindowInfo(hwnd);
            if (windowInfo is not null) {
                WindowAdded?.Invoke(this, windowInfo);
            }
        } else if (eventType == NativeMethods.EVENT_OBJECT_DESTROY || eventType == NativeMethods.EVENT_OBJECT_HIDE) {
            var windowInfo = new WindowInfo(hwnd);
            WindowRemoved?.Invoke(this, windowInfo);
        }
    }



    private static string GetProcessName(uint processId) {
        try {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        } catch {
            return string.Empty;
        }
    }

    private static partial class NativeMethods {
        internal const uint EVENT_OBJECT_CREATE = 0x8000;
        internal const uint EVENT_OBJECT_DESTROY = 0x8001;
        internal const uint EVENT_OBJECT_SHOW = 0x8002;
        internal const uint EVENT_OBJECT_HIDE = 0x8003;

        internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        internal const int OBJID_WINDOW = 0;
        internal const int CHILDID_SELF = 0;
        internal const uint GA_ROOT = 2;
        internal const uint GW_OWNER = 4;
        internal const int GWL_STYLE = -16;
        internal const long WS_CHILD = 0x40000000L;

        internal delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr lParam);
        internal delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }
}

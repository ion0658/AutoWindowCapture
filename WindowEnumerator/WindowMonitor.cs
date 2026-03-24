using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WindowEnumerator;

public delegate void WindowInfoChangedHandler(object? sender, WindowInfo window);

public sealed class WindowInfo : IEquatable<WindowInfo> {
    public WindowInfo(Windows.Win32.Foundation.HWND handle, string title, uint processId, string processName) {
        Handle = handle;
        Title = title;
        ProcessId = processId;
        ProcessName = processName;
    }

    public WindowInfo(Windows.Win32.Foundation.HWND handle) {
        Handle = handle;
        Title = string.Empty;
        ProcessId = 0;
        ProcessName = string.Empty;
    }

    public Windows.Win32.Foundation.HWND Handle { get; }

    public string Title { get; }

    public uint ProcessId { get; }

    public string ProcessName { get; }


    public bool Equals(WindowInfo? other) {
        return other is not null && Handle == other.Handle;
    }

    public override bool Equals(object? obj) {
        return obj is WindowInfo other && Equals(other);
    }

    public override int GetHashCode() {
        return HashCode.Combine(Handle, Title, ProcessId, ProcessName);
    }
}

public sealed class WindowMonitor : IDisposable {
    private readonly Windows.Win32.UI.Accessibility.WINEVENTPROC _winEventProc;
    private readonly int CurrentProcessId = Process.GetCurrentProcess().Id;

    private readonly List<Windows.Win32.UI.Accessibility.HWINEVENTHOOK> _hooks = [];

    public WindowMonitor() {
        _winEventProc = OnWinEvent;
        List<uint> _hook_targets = [
            PInvoke.EVENT_OBJECT_SHOW,
            PInvoke.EVENT_OBJECT_HIDE,
            PInvoke.EVENT_OBJECT_NAMECHANGE,
        ];
        foreach (uint target in _hook_targets) {
            _hooks.Add(RegisterHook(target));
        }
    }


    public event WindowInfoChangedHandler? WindowAdded;
    public event WindowInfoChangedHandler? WindowRemoved;

    public void Stop() {
        foreach (HWINEVENTHOOK hook in _hooks) {
            _ = PInvoke.UnhookWinEvent(hook);
        }
    }

    public IReadOnlyList<WindowInfo> EnumerateWindows() {
        List<WindowInfo> windows = [];

        _ = PInvoke.EnumWindows((windowHandle, _) => {
            WindowInfo? windowInfo = TryCreateWindowInfo(windowHandle);
            if (windowInfo is null) {
                return true;
            }

            windows.Add(windowInfo);
            return true;
        }, IntPtr.Zero);

        return windows
            .OrderBy(static window => window.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static window => window.ProcessId)
            .ThenBy(static window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public void Dispose() {
        Stop();
    }

    private WindowInfo? TryCreateWindowInfo(Windows.Win32.Foundation.HWND hwnd) {
        if (hwnd == IntPtr.Zero) {
            return null;
        }

        _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint processId);
        if (processId == 0 || processId == CurrentProcessId) {
            return null;
        }

        if (!PInvoke.IsWindowVisible(hwnd)) {
            return null;
        }

        if (PInvoke.GetParent(hwnd) != IntPtr.Zero) {
            return null;
        }

        if (PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) != hwnd) {
            return null;
        }

        if (PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER) != IntPtr.Zero) {
            return null;
        }

        long style = new IntPtr(PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE)).ToInt64();
        if ((style & (long)WINDOW_STYLE.WS_CHILD) == (long)WINDOW_STYLE.WS_CHILD) {
            return null;
        }

        int titleLength = PInvoke.GetWindowTextLength(hwnd);
        if (titleLength <= 0) {
            return null;
        }

        Span<char> buffer = new char[titleLength + 1];
        _ = PInvoke.GetWindowText(hwnd, buffer);

        string title = new string(buffer).Trim();
        if (string.IsNullOrWhiteSpace(title)) {
            return null;
        }

        string processName = GetProcessName(processId);
        return new WindowInfo(hwnd, title, processId, processName);
    }

    private Windows.Win32.UI.Accessibility.HWINEVENTHOOK RegisterHook(uint eventId) {
        return PInvoke.SetWinEventHook(
             eventId,
             eventId,
             Windows.Win32.Foundation.HMODULE.Null,
             _winEventProc,
             0,
             0,
             PInvoke.WINEVENT_OUTOFCONTEXT);
    }

    private void OnWinEvent(Windows.Win32.UI.Accessibility.HWINEVENTHOOK hWinEventHook, uint eventType, Windows.Win32.Foundation.HWND hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime) {
        if (hwnd == IntPtr.Zero) {
            return;
        }

        if (idObject != (int)OBJECT_IDENTIFIER.OBJID_WINDOW || idChild != PInvoke.CHILDID_SELF) {
            return;
        }

        if (eventType is PInvoke.EVENT_OBJECT_CREATE or PInvoke.EVENT_OBJECT_SHOW or PInvoke.EVENT_OBJECT_NAMECHANGE) {
            WindowInfo? windowInfo = TryCreateWindowInfo(hwnd);
            if (windowInfo is not null) {
                WindowAdded?.Invoke(this, windowInfo);
            }
        } else if (eventType is PInvoke.EVENT_OBJECT_DESTROY or PInvoke.EVENT_OBJECT_HIDE) {
            WindowInfo windowInfo = new(hwnd);
            WindowRemoved?.Invoke(this, windowInfo);
        }
    }

    private static string GetProcessName(uint processId) {
        try {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        } catch {
            return string.Empty;
        }
    }
}


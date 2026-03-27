using CommunityToolkit.Mvvm.ComponentModel;
using ConfigManager;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WindowEnumeratorNative;
using WindowListPage.Services;

namespace WindowListPage.ViewModels;

public sealed partial class WindowListViewModel : ObservableObject, IDisposable
{
    private readonly ConfigManagerService _configManager = new();
    private readonly WindowMonitor _observer = new();
    private IRecordingWindowLauncher? _recordingWindowLauncher;

    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty]
    public partial ObservableCollection<WindowInfo> Windows { get; set; } = [];

    [ObservableProperty]
    public partial WindowInfo? SelectedWindow { get; set; } = null;

    public WindowListViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _observer.WindowAdded += (window) => OnWindowAdded(window);
        _observer.WindowRemoved += (window) => OnWindowRemoved(window);

        Windows.CollectionChanged += (sender, args) =>
        {
            WindowInfo? selected_window = SelectedWindow;
            if (selected_window == null)
            {
                return;
            }
            SelectedWindow = Windows.Contains(selected_window) ? selected_window : null;
        };

        _ = Task.Run(() => { SetWindows(_observer.EnumerateWindows()); });
    }

    public void Dispose()
    {
        _observer.Stop();
    }

    public void SetRecordingWindowLauncher(IRecordingWindowLauncher launcher)
    {
        _recordingWindowLauncher = launcher;
    }

    partial void OnSelectedWindowChanged(WindowInfo? value)
    {
        if (value is null)
        {
            return;
        }

        _recordingWindowLauncher?.OpenOrActivate(value, false);
        SelectedWindow = null;
    }

    public void SetWindows(IEnumerable<WindowInfo> windows)
    {
        _ = _dispatcher.TryEnqueue(() =>
        {
            foreach (WindowInfo window in windows)
            {
                Debug.WriteLine($"Window: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
                AddOrUpdateWindow(window);
            }
        });
    }

    private void OnWindowAdded(WindowInfo window)
    {
        _ = _dispatcher.TryEnqueue(() =>
        {
            bool isNewWindow = AddOrUpdateWindow(window);
            bool shouldAutoRecord = ShouldAutoRecord(window);
            if (!isNewWindow || _recordingWindowLauncher is null || !shouldAutoRecord)
            {
                return;
            }
            _recordingWindowLauncher.OpenOrActivate(window, shouldAutoRecord);
        });
    }

    private bool AddOrUpdateWindow(WindowInfo window)
    {
        WindowInfo? existingWindow = Windows.FirstOrDefault(w => w.Handle == window.Handle);
        if (existingWindow is null)
        {
            Debug.WriteLine($"Window Added: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
            Windows.Add(window);
            return true;
        }

        Debug.WriteLine($"Window Updated: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
        Debug.WriteLine($"Old Window: {existingWindow.Title} (Handle: {existingWindow.Handle}, Process: {existingWindow.ProcessName})");
        Windows[Windows.IndexOf(existingWindow)] = window;
        return false;
    }

    private void OnWindowRemoved(WindowInfo window)
    {
        _ = _dispatcher.TryEnqueue(() =>
        {
            WindowInfo? existingWindow = Windows.FirstOrDefault(w => w.Handle == window.Handle);
            if (existingWindow != null)
            {
                Debug.WriteLine($"Window Removed: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
                _ = Windows.Remove(existingWindow);
            }
        });
    }

    private bool ShouldAutoRecord(WindowInfo window)
    {
        if (string.IsNullOrWhiteSpace(window.ProcessName))
        {
            return false;
        }
        return _configManager.Load().AutoRecordingExecutableNames
            .Select(static executableName => Path.GetFileNameWithoutExtension(Path.GetFileName(executableName.Trim())))
            .Any(executableName => string.Equals(executableName, window.ProcessName, StringComparison.OrdinalIgnoreCase));
    }
}


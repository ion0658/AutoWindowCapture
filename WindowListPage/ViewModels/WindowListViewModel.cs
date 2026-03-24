using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WindowEnumerator;
using WindowListPage.Services;

namespace WindowListPage.ViewModels;

public sealed partial class WindowListViewModel : ObservableObject, IDisposable
{
    private readonly WindowMonitor _observer = new();
    private IRecordingWindowLauncher? _recordingWindowLauncher;

    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty]
    private ObservableCollection<WindowInfo> _windows = [];

    [ObservableProperty]
    private WindowInfo? _selectedWindow = null;


    public WindowListViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _observer.WindowAdded += (sender, window) => OnWindowAdded(window);
        _observer.WindowRemoved += (sender, window) => OnWindowRemoved(window);

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
        _observer.Dispose();
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

        _recordingWindowLauncher?.OpenOrActivate(value);
        _selectedWindow = null;
    }

    public void SetWindows(IEnumerable<WindowInfo> windows)
    {
        foreach (WindowInfo window in windows)
        {
            Debug.WriteLine($"Window: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
            OnWindowAdded(window);
        }
    }

    private void OnWindowAdded(WindowInfo window)
    {
        _ = _dispatcher.TryEnqueue(() =>
        {
            WindowInfo? existingWindow = Windows.FirstOrDefault(w => w.Handle == window.Handle);
            if (existingWindow is null)
            {
                Debug.WriteLine($"Window Added: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
                Windows.Add(window);
            }
            else
            {
                Debug.WriteLine($"Window Updated: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
                Debug.WriteLine($"Old Window: {existingWindow.Title} (Handle: {existingWindow.Handle}, Process: {existingWindow.ProcessName})");
                Windows[Windows.IndexOf(existingWindow)] = window;
            }
        });
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
}


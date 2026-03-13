using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using WindowEnumerator;
using Windows.System;

namespace AutoWindowCapture.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject {
    private WindowMonitor _observer = new();

    [ObservableProperty]
    private ObservableCollection<WindowInfo> _windows = [];

    [ObservableProperty]
    private WindowInfo? _selectedWindow = null;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _settingsMessage = "設定項目は未実装です。";

    public MainWindowViewModel() {
        _observer.WindowAdded += (sender, window) => OnWindowAdded(window);
        _observer.WindowRemoved += (sender, window) => OnWindowRemoved(window);

        Windows.CollectionChanged += (sender, args) => { StatusMessage = $"{Windows.Count} 件のウィンドウを表示中"; };
        Windows.CollectionChanged += (sender, args) => { var selectedHandle = SelectedWindow?.Handle; SelectedWindow = selectedHandle.HasValue ? Windows.FirstOrDefault(window => window.Handle == selectedHandle.Value) : null; };

        SetWindows(_observer.EnumerateWindows());
    }

    public void SetWindows(IEnumerable<WindowInfo> windows) {
        Windows.Clear();
        foreach (var window in windows) {
            Debug.WriteLine($"Window: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
            Windows.Add(window);
        }
    }

    private void OnWindowAdded(WindowInfo window) {
        if (Windows.Any(w => w.Handle == window.Handle)) {
            return;
        }
        Debug.WriteLine($"Window Added: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
        Windows.Add(window);
    }

    private void OnWindowRemoved(WindowInfo window) {
        var existingWindow = Windows.FirstOrDefault(w => w.Handle == window.Handle);
        if (existingWindow != null) {
            Debug.WriteLine($"Window Removed: {window.Title} (Handle: {window.Handle}, Process: {window.ProcessName})");
            Windows.Remove(existingWindow);
        }
    }
}

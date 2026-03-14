using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WindowEnumerator;

namespace WindowListPage.ViewModels;

public sealed partial class WindowListViewModel : ObservableObject {
    private WindowMonitor _observer = new();

    [ObservableProperty]
    private ObservableCollection<WindowInfo> _windows = [];

    [ObservableProperty]
    private WindowInfo? _selectedWindow = null;


    public WindowListViewModel() {
        _observer.WindowAdded += (sender, window) => OnWindowAdded(window);
        _observer.WindowRemoved += (sender, window) => OnWindowRemoved(window);

        Windows.CollectionChanged += (sender, args) => {
            var selected_window = SelectedWindow;
            if (selected_window == null) {
                return;
            }
            SelectedWindow = Windows.Contains(selected_window) ? selected_window : null;
        };

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


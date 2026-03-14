using Microsoft.UI.Xaml;
using RecordingWindow.ViewModels;
using WindowEnumerator;

namespace RecordingWindow.Views;

public sealed partial class RecordingWindow : Window {

    public readonly RecordingWindowViewModel vm;

    public RecordingWindow(WindowInfo targetWindow) {
        InitializeComponent();

        Title = $"Recording - {targetWindow.Title}";
        vm = new RecordingWindowViewModel(targetWindow);
    }
}

using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using RecordingWindow.ViewModels;
using WindowEnumerator;

namespace RecordingWindow.Views;

public sealed partial class RecordingWindow : Window {

    public readonly RecordingWindowViewModel vm;

    public RecordingWindow(WindowInfo targetWindow) {
        InitializeComponent();

        Title = $"Recording - {targetWindow.ProcessName}";
        vm = new RecordingWindowViewModel(targetWindow, CanvasDevice.GetSharedDevice());
        Canvas.SizeChanged += (s, e) => { vm.SwapChain?.ResizeBuffers(e.NewSize); };
        Closed += (s, e) => vm.Dispose();
    }
}

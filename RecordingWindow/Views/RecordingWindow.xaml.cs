using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using RecordingWindow.ViewModels;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowEnumeratorNative;

namespace RecordingWindow.Views;

public sealed partial class RecordingWindow : Window
{

    public readonly RecordingWindowViewModel vm;

    public RecordingWindow(WindowInfo targetWindow, bool recOnOpen)
    {
        InitializeComponent();

        Title = $"Preview - {targetWindow.ProcessName}";
        vm = new RecordingWindowViewModel(targetWindow, recOnOpen, CanvasDevice.GetSharedDevice(), DispatcherQueue);
        Canvas.SizeChanged += (s, e) => { vm.SwapChain?.ResizeBuffers(e.NewSize); };
        vm.CloseRequested += () => DispatcherQueue.TryEnqueue(() => Close());
        Closed += (s, e) => vm.Dispose();
    }
}


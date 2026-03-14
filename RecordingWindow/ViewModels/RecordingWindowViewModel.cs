using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using System;
using WindowCapture;
using WindowEnumerator;
using Windows.Graphics.Capture;

namespace RecordingWindow.ViewModels;

public sealed partial class RecordingWindowViewModel : ObservableObject {

    private readonly GraphicsCaptureItem? _captureItem;
    public RecordingWindowViewModel(WindowInfo targetWindow) {
        _captureItem = CaptureHelper.CreateItemForWindow(targetWindow.Handle);
    }
}

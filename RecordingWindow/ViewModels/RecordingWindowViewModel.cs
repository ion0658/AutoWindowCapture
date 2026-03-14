using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading;
using System.Threading.Tasks;
using WindowEnumerator;
using Windows.Graphics.Capture;
using Windows.Graphics.Imaging;

namespace RecordingWindow.ViewModels;

public sealed partial class RecordingWindowViewModel : ObservableObject {

    public RecordingWindowViewModel(WindowInfo targetWindow) {

    }
}

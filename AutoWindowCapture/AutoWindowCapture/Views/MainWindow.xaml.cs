using AutoWindowCapture.Services;
using AutoWindowCapture.ViewModels;
using Microsoft.UI.Xaml;

namespace AutoWindowCapture.View;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{

    public MainWindowViewModel vm { get; }

    public MainWindow()
    {
        InitializeComponent();

        NavigationService nav = new() { Frame = ContentFrame };
        RecordingWindowLauncher recordingWindowLauncher = new();
        vm = new MainWindowViewModel(nav, recordingWindowLauncher);
    }
}


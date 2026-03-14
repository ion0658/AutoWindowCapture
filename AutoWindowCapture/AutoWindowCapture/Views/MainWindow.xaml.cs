using AutoWindowCapture.Services;
using AutoWindowCapture.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AutoWindowCapture.View;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window {

    public MainWindowViewModel vm { get; }

    public MainWindow() {
        InitializeComponent();

        var nav = new NavigationService { Frame = this.ContentFrame };
        vm = new MainWindowViewModel(nav);
    }
}

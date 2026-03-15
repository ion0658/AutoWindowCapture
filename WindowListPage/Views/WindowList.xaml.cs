using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WindowListPage.Services;
using WindowListPage.ViewModels;

namespace WindowListPage.Views;

public sealed partial class WindowList : Page {
    public readonly WindowListViewModel vm;
    public WindowList() {
        InitializeComponent();

        vm = new WindowListViewModel(DispatcherQueue);
        Unloaded += (sender, args) => vm.Dispose();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e) {
        base.OnNavigatedTo(e);

        if (e.Parameter is IRecordingWindowLauncher launcher) {
            vm.SetRecordingWindowLauncher(launcher);
        }
    }
}


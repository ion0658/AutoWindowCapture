using Microsoft.UI.Xaml.Controls;
using WindowListPage.ViewModels;

namespace WindowListPage.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class WindowList : Page {

    public readonly WindowListViewModel vm = new();
    public WindowList() {
        InitializeComponent();
    }
}


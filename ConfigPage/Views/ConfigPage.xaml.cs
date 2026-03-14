using ConfigPage.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace ConfigPage.Views;

public sealed partial class ConfigPage : Page {

    public readonly ConfigPageViewModel vm = new();

    public ConfigPage() {
        InitializeComponent();
    }
}

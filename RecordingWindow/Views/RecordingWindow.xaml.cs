using Microsoft.UI.Xaml;
using RecordingWindow.ViewModels;

namespace RecordingWindow.Views {
    public sealed partial class RecordingWindow : Window {

        public readonly RecordingWindowViewModel vm = new();
        public RecordingWindow() {
            InitializeComponent();
        }
    }
}

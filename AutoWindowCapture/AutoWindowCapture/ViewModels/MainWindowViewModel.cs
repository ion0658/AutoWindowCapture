using AutoWindowCapture.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using System;
using WindowListPage.Services;

namespace AutoWindowCapture.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject {
    private readonly INavigationService _navigationService;
    private readonly IRecordingWindowLauncher _recordingWindowLauncher;

    [ObservableProperty]
    private NavigationViewItem? _selectedItem;

    public MainWindowViewModel(INavigationService nav, IRecordingWindowLauncher recordingWindowLauncher) {
        _navigationService = nav;
        _recordingWindowLauncher = recordingWindowLauncher;
    }

    partial void OnSelectedItemChanged(NavigationViewItem? value) {
        if (value == null || !value.IsSelected || value.Tag is not string pageTypeName) return;
        if (pageTypeName == "WindowList") {
            var page_type = Type.GetType("WindowListPage.Views.WindowList, WindowListPage");
            if (page_type != null) {
                _navigationService.NavigateTo(page_type, _recordingWindowLauncher);
            }
        }
        if (pageTypeName == "Settings") {
            _navigationService.NavigateTo(typeof(ConfigPage.Views.ConfigPage));
        }
    }
}

using ConfigPage.ViewModels;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace ConfigPage.Views;

public sealed partial class ConfigPage : Page
{

    public readonly ConfigPageViewModel vm = new();

    public ConfigPage()
    {
        InitializeComponent();
        Loaded += (_, _) => vm.LoadConfig();
    }

    [DllImport("user32.dll")]
    private static extern nint GetActiveWindow();

    private async void SelectRecordingDirectory_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");

        nint hwnd = GetActiveWindow();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        vm.SetRecordingSaveDirectory(folder.Path);
    }

    private async void AddExecutableFiles_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".exe");

        nint hwnd = GetActiveWindow();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        IReadOnlyList<StorageFile>? files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
        {
            return;
        }

        vm.AddExecutableNames(files.Select(x => x.Name));
    }
}


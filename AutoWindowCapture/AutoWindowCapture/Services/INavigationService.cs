using Microsoft.UI.Xaml.Controls;
using System;

namespace AutoWindowCapture.Services {
    public interface INavigationService {
        void NavigateTo(Type pageType, object? parameter = null);
    }

    public class NavigationService : INavigationService {
        public Frame? Frame { get; set; } = null;

        public void NavigateTo(Type pageType, object? parameter = null) {
            _ = (Frame?.Navigate(pageType, parameter));
        }
    }
}


#pragma once
#include "WindowEnumerator.WindowMonitor.g.h"

namespace winrt::WindowEnumerator::implementation {
struct WindowMonitor : WindowMonitorT<WindowMonitor> {
    WindowMonitor() = default;

    winrt::Windows::Foundation::Collections::IVectorView<
        winrt::WindowEnumerator::WindowInfo>
    EnumerateWindows();
    void Stop();
    winrt::event_token WindowAdded(
        winrt::WindowEnumerator::WindowInfoChangedHandler const& handler);
    void WindowAdded(winrt::event_token const& token) noexcept;
    winrt::event_token WindowRemoved(
        winrt::WindowEnumerator::WindowInfoChangedHandler const& handler);
    void WindowRemoved(winrt::event_token const& token) noexcept;
};
}  // namespace winrt::WindowEnumerator::implementation
namespace winrt::WindowEnumerator::factory_implementation {
struct WindowMonitor
    : WindowMonitorT<WindowMonitor, implementation::WindowMonitor> {};
}  // namespace winrt::WindowEnumerator::factory_implementation

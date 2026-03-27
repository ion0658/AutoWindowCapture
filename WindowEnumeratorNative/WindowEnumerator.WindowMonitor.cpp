#include "WindowEnumerator.WindowMonitor.h"
#include "WindowEnumerator.WindowMonitor.g.cpp"
#include "pch.h"

namespace winrt::WindowEnumerator::implementation {
winrt::Windows::Foundation::Collections::IVectorView<
    winrt::WindowEnumerator::WindowInfo>
WindowMonitor::EnumerateWindows() {
    throw hresult_not_implemented();
}
void WindowMonitor::Stop() {
    throw hresult_not_implemented();
}
winrt::event_token WindowMonitor::WindowAdded(
    winrt::WindowEnumerator::WindowInfoChangedHandler const& handler) {
    throw hresult_not_implemented();
}
void WindowMonitor::WindowAdded(winrt::event_token const& token) noexcept {
    throw hresult_not_implemented();
}
winrt::event_token WindowMonitor::WindowRemoved(
    winrt::WindowEnumerator::WindowInfoChangedHandler const& handler) {
    throw hresult_not_implemented();
}
void WindowMonitor::WindowRemoved(winrt::event_token const& token) noexcept {
    throw hresult_not_implemented();
}
}  // namespace winrt::WindowEnumerator::implementation

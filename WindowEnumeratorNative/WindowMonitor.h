#pragma once
#include "WindowMonitor.g.h"

#include <mutex>
#include <optional>
#include <string>
#include <unordered_map>
#include <vector>

namespace winrt::WindowEnumeratorNative::implementation {
struct WindowMonitor : WindowMonitorT<WindowMonitor> {
    WindowMonitor();
    ~WindowMonitor();

    winrt::Windows::Foundation::Collections::IVectorView<
        winrt::WindowEnumeratorNative::WindowInfo>
    EnumerateWindows();
    void Stop();
    winrt::event_token WindowAdded(
        winrt::WindowEnumeratorNative::WindowInfoChangedHandler const& handler);
    void WindowAdded(winrt::event_token const& token) noexcept;
    winrt::event_token WindowRemoved(
        winrt::WindowEnumeratorNative::WindowInfoChangedHandler const& handler);
    void WindowRemoved(winrt::event_token const& token) noexcept;

private:
    static void CALLBACK OnWinEventStatic(HWINEVENTHOOK hook,
                                          DWORD eventType,
                                          HWND hwnd,
                                          LONG idObject,
                                          LONG idChild,
                                          DWORD idEventThread,
                                          DWORD eventTime);
    void OnWinEvent(DWORD eventType, HWND hwnd, LONG idObject, LONG idChild);
    HWINEVENTHOOK RegisterHook(DWORD eventId);
    std::optional<winrt::WindowEnumeratorNative::WindowInfo> TryCreateWindowInfo(
        HWND hwnd) const;
    static std::wstring GetProcessName(DWORD processId);

    DWORD m_currentProcessId{::GetCurrentProcessId()};
    std::vector<HWINEVENTHOOK> m_hooks;
    std::mutex m_hooksMutex;

    winrt::event<winrt::WindowEnumeratorNative::WindowInfoChangedHandler>
        m_windowAdded;
    winrt::event<winrt::WindowEnumeratorNative::WindowInfoChangedHandler>
        m_windowRemoved;

    static std::mutex s_hookMapMutex;
    static std::unordered_map<HWINEVENTHOOK, WindowMonitor*> s_hookMap;
};
}  // namespace winrt::WindowEnumeratorNative::implementation
namespace winrt::WindowEnumeratorNative::factory_implementation {
struct WindowMonitor
    : WindowMonitorT<WindowMonitor, implementation::WindowMonitor> {};
}  // namespace winrt::WindowEnumeratorNative::factory_implementation

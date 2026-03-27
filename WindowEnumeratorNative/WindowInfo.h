#pragma once
#include "WindowInfo.g.h"

namespace winrt::WindowEnumeratorNative::implementation {
struct WindowInfo : WindowInfoT<WindowInfo> {
    WindowInfo() = default;

    WindowInfo(int64_t handle,
               hstring const& title,
               uint32_t processId,
               hstring const& processName);
    int64_t Handle();
    hstring Title();
    uint32_t ProcessId();
    hstring ProcessName();

   private:
    int64_t m_handle{};
    hstring m_title;
    uint32_t m_processId{};
    hstring m_processName;
};
}  // namespace winrt::WindowEnumeratorNative::implementation
namespace winrt::WindowEnumeratorNative::factory_implementation {
struct WindowInfo : WindowInfoT<WindowInfo, implementation::WindowInfo> {};
}  // namespace winrt::WindowEnumeratorNative::factory_implementation

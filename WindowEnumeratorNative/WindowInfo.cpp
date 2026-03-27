#include "pch.h"

#include "WindowInfo.h"

#include "WindowInfo.g.cpp"

namespace winrt::WindowEnumeratorNative::implementation {
WindowInfo::WindowInfo(int64_t handle,
                       hstring const& title,
                       uint32_t processId,
                       hstring const& processName)
    : m_handle(handle),
      m_title(title),
      m_processId(processId),
      m_processName(processName) {}

int64_t WindowInfo::Handle() {
    return m_handle;
}

hstring WindowInfo::Title() {
    return m_title;
}

uint32_t WindowInfo::ProcessId() {
    return m_processId;
}

hstring WindowInfo::ProcessName() {
    return m_processName;
}
}  // namespace winrt::WindowEnumeratorNative::implementation

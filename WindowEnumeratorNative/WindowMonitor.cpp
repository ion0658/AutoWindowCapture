#include "pch.h"

#include "WindowMonitor.h"

#include "WindowMonitor.g.cpp"

#include <algorithm>
#include <cwctype>
#include <string>
#include <string_view>

namespace {
std::wstring Trim(std::wstring value) {
    auto isNotSpace = [](wchar_t c) { return std::iswspace(c) == 0; };

    auto begin = std::find_if(value.begin(), value.end(), isNotSpace);
    if (begin == value.end()) {
        return L"";
    }

    auto end = std::find_if(value.rbegin(), value.rend(), isNotSpace).base();
    return std::wstring(begin, end);
}

int CompareCaseInsensitive(std::wstring const& left, std::wstring const& right) {
    return ::CompareStringOrdinal(left.c_str(),
                                  static_cast<int>(left.size()),
                                  right.c_str(),
                                  static_cast<int>(right.size()),
                                  TRUE) -
           CSTR_EQUAL;
}
}  // namespace

namespace winrt::WindowEnumeratorNative::implementation {
std::mutex WindowMonitor::s_hookMapMutex;
std::unordered_map<HWINEVENTHOOK, WindowMonitor*> WindowMonitor::s_hookMap;

WindowMonitor::WindowMonitor() {
    for (DWORD eventId : {EVENT_OBJECT_SHOW,
                          EVENT_OBJECT_HIDE,
                          EVENT_OBJECT_NAMECHANGE}) {
        m_hooks.push_back(RegisterHook(eventId));
    }
}

WindowMonitor::~WindowMonitor() {
    Stop();
}

winrt::Windows::Foundation::Collections::IVectorView<
    winrt::WindowEnumeratorNative::WindowInfo>
WindowMonitor::EnumerateWindows() {
    std::vector<winrt::WindowEnumeratorNative::WindowInfo> windows;

    struct EnumContext {
        WindowMonitor* self;
        std::vector<winrt::WindowEnumeratorNative::WindowInfo>* collection;
    } context{this, &windows};

    ::EnumWindows(
        [](HWND windowHandle, LPARAM param) -> BOOL {
            auto* ctx = reinterpret_cast<EnumContext*>(param);
            auto windowInfo = ctx->self->TryCreateWindowInfo(windowHandle);
            if (windowInfo.has_value()) {
                ctx->collection->push_back(windowInfo.value());
            }
            return TRUE;
        },
        reinterpret_cast<LPARAM>(&context));

    std::sort(windows.begin(),
              windows.end(),
              [](auto const& left, auto const& right) {
                  std::wstring leftProcessName{left.ProcessName().c_str()};
                  std::wstring rightProcessName{right.ProcessName().c_str()};

                  int byName = CompareCaseInsensitive(leftProcessName, rightProcessName);
                  if (byName != 0) {
                      return byName < 0;
                  }

                  if (left.ProcessId() != right.ProcessId()) {
                      return left.ProcessId() < right.ProcessId();
                  }

                  std::wstring leftTitle{left.Title().c_str()};
                  std::wstring rightTitle{right.Title().c_str()};

                  return CompareCaseInsensitive(leftTitle, rightTitle) < 0;
              });

    auto result =
        winrt::single_threaded_vector<winrt::WindowEnumeratorNative::WindowInfo>();
    for (auto const& window : windows) {
        result.Append(window);
    }

    return result.GetView();
}

void WindowMonitor::Stop() {
    std::vector<HWINEVENTHOOK> hooks;
    {
        std::scoped_lock lock(m_hooksMutex);
        hooks.swap(m_hooks);
    }

    {
        std::scoped_lock lock(s_hookMapMutex);
        for (auto hook : hooks) {
            s_hookMap.erase(hook);
        }
    }

    for (auto hook : hooks) {
        ::UnhookWinEvent(hook);
    }
}

winrt::event_token WindowMonitor::WindowAdded(
    winrt::WindowEnumeratorNative::WindowInfoChangedHandler const& handler) {
    return m_windowAdded.add(handler);
}

void WindowMonitor::WindowAdded(winrt::event_token const& token) noexcept {
    m_windowAdded.remove(token);
}

winrt::event_token WindowMonitor::WindowRemoved(
    winrt::WindowEnumeratorNative::WindowInfoChangedHandler const& handler) {
    return m_windowRemoved.add(handler);
}

void WindowMonitor::WindowRemoved(winrt::event_token const& token) noexcept {
    m_windowRemoved.remove(token);
}

void CALLBACK WindowMonitor::OnWinEventStatic(HWINEVENTHOOK hook,
                                              DWORD eventType,
                                              HWND hwnd,
                                              LONG idObject,
                                              LONG idChild,
                                              DWORD,
                                              DWORD) {
    WindowMonitor* monitor = nullptr;
    {
        std::scoped_lock lock(s_hookMapMutex);
        auto it = s_hookMap.find(hook);
        if (it != s_hookMap.end()) {
            monitor = it->second;
        }
    }

    if (monitor == nullptr) {
        return;
    }

    monitor->OnWinEvent(eventType, hwnd, idObject, idChild);
}

void WindowMonitor::OnWinEvent(DWORD eventType,
                               HWND hwnd,
                               LONG idObject,
                               LONG idChild) {
    if (hwnd == nullptr) {
        return;
    }

    if (idObject != OBJID_WINDOW || idChild != CHILDID_SELF) {
        return;
    }

    if (eventType == EVENT_OBJECT_CREATE || eventType == EVENT_OBJECT_SHOW ||
        eventType == EVENT_OBJECT_NAMECHANGE) {
        auto windowInfo = TryCreateWindowInfo(hwnd);
        if (windowInfo.has_value()) {
            m_windowAdded(windowInfo.value());
        }
    } else if (eventType == EVENT_OBJECT_DESTROY || eventType == EVENT_OBJECT_HIDE) {
        m_windowRemoved(winrt::WindowEnumeratorNative::WindowInfo(
            reinterpret_cast<intptr_t>(hwnd), L"", 0, L""));
    }
}

HWINEVENTHOOK WindowMonitor::RegisterHook(DWORD eventId) {
    auto hook = ::SetWinEventHook(eventId,
                                  eventId,
                                  nullptr,
                                  &WindowMonitor::OnWinEventStatic,
                                  0,
                                  0,
                                  WINEVENT_OUTOFCONTEXT);

    if (hook != nullptr) {
        std::scoped_lock lock(s_hookMapMutex);
        s_hookMap[hook] = this;
    }

    return hook;
}

std::optional<winrt::WindowEnumeratorNative::WindowInfo>
WindowMonitor::TryCreateWindowInfo(HWND hwnd) const {
    if (hwnd == nullptr) {
        return std::nullopt;
    }

    DWORD processId = 0;
    ::GetWindowThreadProcessId(hwnd, &processId);
    if (processId == 0 || processId == m_currentProcessId) {
        return std::nullopt;
    }

    if (!::IsWindowVisible(hwnd)) {
        return std::nullopt;
    }

    if (::GetParent(hwnd) != nullptr) {
        return std::nullopt;
    }

    if (::GetAncestor(hwnd, GA_ROOT) != hwnd) {
        return std::nullopt;
    }

    if (::GetWindow(hwnd, GW_OWNER) != nullptr) {
        return std::nullopt;
    }

    LONG_PTR style = ::GetWindowLongPtrW(hwnd, GWL_STYLE);
    if ((style & WS_CHILD) == WS_CHILD) {
        return std::nullopt;
    }

    int titleLength = ::GetWindowTextLengthW(hwnd);
    if (titleLength <= 0) {
        return std::nullopt;
    }

    std::wstring buffer(static_cast<size_t>(titleLength) + 1, L'\0');
    int copied = ::GetWindowTextW(hwnd, buffer.data(), titleLength + 1);
    if (copied <= 0) {
        return std::nullopt;
    }

    buffer.resize(static_cast<size_t>(copied));
    std::wstring title = Trim(std::move(buffer));
    if (title.empty()) {
        return std::nullopt;
    }

    std::wstring processName = GetProcessName(processId);

    return winrt::WindowEnumeratorNative::WindowInfo(
        reinterpret_cast<intptr_t>(hwnd),
        winrt::hstring(title),
        processId,
        winrt::hstring(processName));
}

std::wstring WindowMonitor::GetProcessName(DWORD processId) {
    HANDLE processHandle =
        ::OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
    if (processHandle == nullptr) {
        return L"";
    }

    wchar_t imagePath[MAX_PATH]{};
    DWORD size = ARRAYSIZE(imagePath);
    const bool succeeded =
        ::QueryFullProcessImageNameW(processHandle, 0, imagePath, &size) != FALSE;

    ::CloseHandle(processHandle);

    if (!succeeded || size == 0) {
        return L"";
    }

    std::wstring fullPath(imagePath, size);
    auto separator = fullPath.find_last_of(L"\\/");
    std::wstring fileName =
        separator == std::wstring::npos ? fullPath : fullPath.substr(separator + 1);

    constexpr std::wstring_view exeSuffix = L".exe";
    if (fileName.size() >= exeSuffix.size() &&
        _wcsicmp(fileName.c_str() + fileName.size() - exeSuffix.size(),
                 exeSuffix.data()) == 0) {
        fileName.resize(fileName.size() - exeSuffix.size());
    }

    return fileName;
}
}  // namespace winrt::WindowEnumeratorNative::implementation

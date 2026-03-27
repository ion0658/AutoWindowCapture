#include "pch.h"

#include "WindowCapture.h"

#include "WindowCapture.g.cpp"

#include <windows.graphics.capture.interop.h>

namespace winrt::WindowGraphicCaptureNative::implementation {
using winrt::Windows::Foundation::IInspectable;
using winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool;
using winrt::Windows::Graphics::Capture::GraphicsCaptureItem;
using winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice;

WindowCapture::WindowCapture(IDirect3DDevice const& device,
                             GraphicsCaptureItem const& item)
    : m_device(device), m_captureItem(item) {
    if (!m_device || !m_captureItem) {
        throw winrt::hresult_invalid_argument();
    }

    m_itemClosedToken = m_captureItem.Closed([this](auto const&, auto const&) {
        StopCapture();
        m_captureStopped();
    });

    m_framePool = Direct3D11CaptureFramePool::Create(
        m_device, PIX_FORMAT, NUM_OF_BUFFERS, m_captureItem.Size());
    if (!m_framePool) {
        throw winrt::hresult_error(E_FAIL, L"Failed to create frame pool.");
    }

    m_frameArrivedToken =
        m_framePool.FrameArrived({this, &WindowCapture::OnFrameArrived});

    m_session = m_framePool.CreateCaptureSession(m_captureItem);
    if (!m_session) {
        throw winrt::hresult_error(E_FAIL,
                                   L"Failed to create capture session.");
    }

    m_session.IsBorderRequired(false);
    m_session.StartCapture();
}

GraphicsCaptureItem WindowCapture::CreateItemForWindow(int64_t handle) {
    auto hwnd = reinterpret_cast<HWND>(handle);
    if (hwnd == nullptr || !::IsWindow(hwnd)) {
        throw winrt::hresult_invalid_argument(L"Invalid window handle.");
    }

    auto interop = winrt::get_activation_factory<GraphicsCaptureItem,
                                                 IGraphicsCaptureItemInterop>();

    winrt::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem>
        item_abi;

    winrt::check_hresult(interop->CreateForWindow(
        hwnd,
        winrt::guid_of<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem>(),
        item_abi.put_void()));

    GraphicsCaptureItem item{nullptr};
    winrt::copy_from_abi(item, item_abi.get());

    return item;
}

winrt::event_token WindowCapture::FrameArrived(
    winrt::WindowGraphicCaptureNative::FrameArrivedHandler const& handler) {
    return m_frameArrived.add(handler);
}

void WindowCapture::FrameArrived(winrt::event_token const& token) noexcept {
    m_frameArrived.remove(token);
}

winrt::event_token WindowCapture::CaptureStopped(
    winrt::WindowGraphicCaptureNative::CaptureStoppedHandler const& handler) {
    return m_captureStopped.add(handler);
}

void WindowCapture::CaptureStopped(winrt::event_token const& token) noexcept {
    m_captureStopped.remove(token);
}

void WindowCapture::Close() {
    StopCapture();
}

void WindowCapture::StopCapture() {
    if (m_session) {
        m_session.Close();
        m_session = nullptr;
    }

    if (m_framePool) {
        if (m_frameArrivedToken.value != 0) {
            m_framePool.FrameArrived(m_frameArrivedToken);
            m_frameArrivedToken = {};
        }

        m_framePool.Close();
        m_framePool = nullptr;
    }

    if (m_captureItem && m_itemClosedToken.value != 0) {
        m_captureItem.Closed(m_itemClosedToken);
        m_itemClosedToken = {};
    }
}

void WindowCapture::OnFrameArrived(Direct3D11CaptureFramePool const& sender,
                                   IInspectable const&) {
    auto frame = sender.TryGetNextFrame();
    if (!frame) {
        return;
    }

    if (frame.ContentSize() != m_lastFrameSize) {
        m_lastFrameSize = frame.ContentSize();
        if (m_framePool) {
            m_framePool.Recreate(m_device, PIX_FORMAT, NUM_OF_BUFFERS,
                                 m_lastFrameSize);
        }
        return;
    }

    m_frameArrived(frame);
    frame.Close();
}
}  // namespace winrt::WindowGraphicCaptureNative::implementation

#pragma once
#include "WindowCapture.g.h"

namespace winrt::WindowGraphicCaptureNative::implementation {
struct WindowCapture : WindowCaptureT<WindowCapture> {
    WindowCapture(winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice const& device,
                  winrt::Windows::Graphics::Capture::GraphicsCaptureItem const& item);

    static winrt::Windows::Graphics::Capture::GraphicsCaptureItem CreateItemForWindow(int64_t handle);

    winrt::event_token FrameArrived(
        winrt::WindowGraphicCaptureNative::FrameArrivedHandler const& handler);
    void FrameArrived(winrt::event_token const& token) noexcept;
    winrt::event_token CaptureStopped(
        winrt::WindowGraphicCaptureNative::CaptureStoppedHandler const& handler);
    void CaptureStopped(winrt::event_token const& token) noexcept;
    void Close();

private:
    void StopCapture();
    void OnFrameArrived(
        winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool const& sender,
        winrt::Windows::Foundation::IInspectable const& args);

    static constexpr winrt::Windows::Graphics::DirectX::DirectXPixelFormat PIX_FORMAT{
        winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized};
    static constexpr int32_t NUM_OF_BUFFERS{2};

    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice m_device{nullptr};
    winrt::Windows::Graphics::Capture::GraphicsCaptureItem m_captureItem{nullptr};
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool m_framePool{nullptr};
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession m_session{nullptr};
    winrt::Windows::Graphics::SizeInt32 m_lastFrameSize{};

    winrt::event_token m_itemClosedToken{};
    winrt::event_token m_frameArrivedToken{};

    winrt::event<winrt::WindowGraphicCaptureNative::FrameArrivedHandler> m_frameArrived;
    winrt::event<winrt::WindowGraphicCaptureNative::CaptureStoppedHandler> m_captureStopped;
};
}  // namespace winrt::WindowGraphicCaptureNative::implementation

namespace winrt::WindowGraphicCaptureNative::factory_implementation {
struct WindowCapture
    : WindowCaptureT<WindowCapture, implementation::WindowCapture> {};
}  // namespace winrt::WindowGraphicCaptureNative::factory_implementation

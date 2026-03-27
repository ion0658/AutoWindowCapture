#pragma once
#include "WindowAudioCaptureNative.WindowAudioCapture.g.h"

#include <wrl/implements.h>
#include <functional>
#include <mutex>

namespace winrt::WindowAudioCaptureNative::implementation
{
    // Internal WRL COM class - not a WinRT runtime class.
    // Forward-declared here so WindowAudioCapture can hold a ComPtr to it.
    class CLoopbackCapture;

    struct WindowAudioCapture : WindowAudioCaptureT<WindowAudioCapture>
    {
        WindowAudioCapture() = default;

        void StartCapture(uint32_t processId);
        void StopCapture();
        winrt::WindowAudioCaptureNative::AudioFormat CaptureFormat();

        winrt::event_token AudioDataReceived(
            winrt::WindowAudioCaptureNative::AudioDataReceivedHandler const& handler);
        void AudioDataReceived(winrt::event_token const& token) noexcept;

        void Close();

    private:
        ::Microsoft::WRL::ComPtr<CLoopbackCapture> m_capture;
        winrt::event<winrt::WindowAudioCaptureNative::AudioDataReceivedHandler> m_audioDataReceived;
        std::mutex m_mutex;
    };
}

namespace winrt::WindowAudioCaptureNative::factory_implementation
{
    struct WindowAudioCapture : WindowAudioCaptureT<WindowAudioCapture, implementation::WindowAudioCapture>
    {
    };
}

#include "pch.h"

#include "LoopbackCapture.h"
#include "LoopbackCaptureBridge.h"

LoopbackCaptureWrapper::LoopbackCaptureWrapper() {
    // WRL RuntimeClass objects must be created with Make<>, not operator new.
    auto ptr = Microsoft::WRL::Make<CLoopbackCapture>();
    m_impl = ptr.Detach();  // Transfer ownership; ref count stays at 1.
}

LoopbackCaptureWrapper::~LoopbackCaptureWrapper() {
    if (m_impl) {
        m_impl->Release();
        m_impl = nullptr;
    }
}

HRESULT LoopbackCaptureWrapper::StartCapture(DWORD processId,
                                             PcmDataCallback callback) {
    return m_impl->StartCaptureAsync(processId, std::move(callback));
}

HRESULT LoopbackCaptureWrapper::StopCapture() {
    return m_impl->StopCaptureAsync();
}

LoopbackCaptureFormatInfo LoopbackCaptureWrapper::GetFormat() const {
    const WAVEFORMATEX& wfx = m_impl->GetCaptureFormat();
    return {static_cast<int>(wfx.nSamplesPerSec),
            static_cast<int>(wfx.wBitsPerSample),
            static_cast<int>(wfx.nChannels)};
}

#include "pch.h"

// Include only the bridge header – no WRL/WIL headers, safe to use with /clr.
#pragma managed(push, off)
#include <memory>
#include "LoopbackCaptureBridge.h"
#pragma managed(pop)

#include <msclr/gcroot.h>
#include "AudioCapture.h"

using namespace System;
using namespace System::Runtime::InteropServices;

// ---------------------------------------------------------------------------
// Native helper types – at file scope to avoid C3923 (no local class
// definitions permitted inside managed class member functions).
// ---------------------------------------------------------------------------

struct PcmCallbackHolder {
    msclr::gcroot<Action<array<Byte> ^, AudioCapture::AudioFormat> ^>
        delegate_ref;
    int sampleRate = 0;
    int bitsPerSample = 0;
    int channels = 0;

    // Default constructor: no managed-type argument, so std::make_shared<> can
    // instantiate this in native template context without hitting C3642/C3821.
    PcmCallbackHolder() = default;

    explicit PcmCallbackHolder(
        Action<array<Byte> ^, AudioCapture::AudioFormat> ^ del)
        : delegate_ref(del) {}

    void Invoke(const BYTE* data, UINT32 byteCount) {
        array<Byte> ^ buf = gcnew array<Byte>(static_cast<int>(byteCount));
        pin_ptr<Byte> dst = &buf[0];
        memcpy(dst, data, byteCount);

        AudioCapture::AudioFormat fmt;
        fmt.SampleRate = sampleRate;
        fmt.BitsPerSample = bitsPerSample;
        fmt.Channels = channels;

        delegate_ref->Invoke(buf, fmt);
    }
};

// Copyable functor wrapping the holder in a shared_ptr.
// Avoids lambdas (which are local class definitions) in managed methods.
struct PcmCallbackInvoker {
    std::shared_ptr<PcmCallbackHolder> holder;

    explicit PcmCallbackInvoker(std::shared_ptr<PcmCallbackHolder> h)
        : holder(std::move(h)) {}

    void operator()(const BYTE* data, UINT32 byteCount) const {
        holder->Invoke(data, byteCount);
    }
};

// ---------------------------------------------------------------------------
// Managed wrapper implementation
// ---------------------------------------------------------------------------

namespace AudioCapture {

LoopbackAudioCapture::LoopbackAudioCapture()
    : m_capture(new LoopbackCaptureWrapper()), m_disposed(false) {}

LoopbackAudioCapture::~LoopbackAudioCapture() {
    if (!m_disposed) {
        this->!LoopbackAudioCapture();
        m_disposed = true;
    }
}

LoopbackAudioCapture::!LoopbackAudioCapture() {
    if (m_capture != nullptr) {
        delete m_capture;
        m_capture = nullptr;
    }
}

void LoopbackAudioCapture::StartCapture(int processId,
                                        Action<array<Byte> ^, AudioFormat> ^
                                            callback) {
    if (m_capture == nullptr)
        throw gcnew ObjectDisposedException("LoopbackAudioCapture");

    auto holder = std::make_shared<PcmCallbackHolder>();
    // Assign the managed delegate separately to avoid passing a managed type
    // as a template argument to std::make_shared (would trigger C3642/C3821).
    holder->delegate_ref = callback;

    // Use a named functor instead of a lambda to satisfy C3923.
    LoopbackCaptureWrapper::PcmDataCallback fn = PcmCallbackInvoker(holder);

    HRESULT hr =
        m_capture->StartCapture(static_cast<DWORD>(processId), std::move(fn));

    if (FAILED(hr))
        throw gcnew Exception(
            String::Format("StartCapture failed: 0x{0:X8}", hr));

    // ActivateCompleted has run synchronously inside StartCapture, so the
    // format is already populated. Fill the holder before audio data arrives.
    LoopbackCaptureFormatInfo fi = m_capture->GetFormat();
    holder->sampleRate = fi.sampleRate;
    holder->bitsPerSample = fi.bitsPerSample;
    holder->channels = fi.channels;
}

void LoopbackAudioCapture::StopCapture() {
    if (m_capture != nullptr)
        m_capture->StopCapture();
}

AudioFormat LoopbackAudioCapture::CaptureFormat::get() {
    AudioFormat fmt;
    if (m_capture != nullptr) {
        LoopbackCaptureFormatInfo fi = m_capture->GetFormat();
        fmt.SampleRate = fi.sampleRate;
        fmt.BitsPerSample = fi.bitsPerSample;
        fmt.Channels = fi.channels;
    }
    return fmt;
}

}  // namespace AudioCapture

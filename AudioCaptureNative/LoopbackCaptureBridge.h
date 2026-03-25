#pragma once
#include <windows.h>
#include <functional>

// POD struct for format info – no WRL/WIL types exposed.
struct LoopbackCaptureFormatInfo {
    int sampleRate;
    int bitsPerSample;
    int channels;
};

// Forward-declare to keep WRL/WIL out of this header.
class CLoopbackCapture;

// Thin non-copyable wrapper around CLoopbackCapture that exposes only
// the types compatible with /clr compilation units.
class LoopbackCaptureWrapper {
   public:
    using PcmDataCallback = std::function<void(const BYTE*, UINT32)>;

    LoopbackCaptureWrapper();
    ~LoopbackCaptureWrapper();

    HRESULT StartCapture(DWORD processId, PcmDataCallback callback);
    HRESULT StopCapture();
    LoopbackCaptureFormatInfo GetFormat() const;

   private:
    CLoopbackCapture* m_impl;

    LoopbackCaptureWrapper(const LoopbackCaptureWrapper&) = delete;
    LoopbackCaptureWrapper& operator=(const LoopbackCaptureWrapper&) = delete;
};

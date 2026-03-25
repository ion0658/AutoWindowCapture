#pragma once

// Forward-declare the native class without pulling in native headers
#pragma managed(push, off)
class LoopbackCaptureWrapper;
#pragma managed(pop)

using namespace System;

namespace AudioCapture {

public
value class AudioFormat {
   public:
    property int SampleRate;
    property int BitsPerSample;
    property int Channels;
};

public
ref class LoopbackAudioCapture : IDisposable {
   public:
    LoopbackAudioCapture();
    ~LoopbackAudioCapture();
    !LoopbackAudioCapture();

    // Start loopback capture for the given process.
    // callback is invoked on an MF worker thread for each PCM chunk.
    void StartCapture(int processId,
                      Action<array<Byte> ^, AudioFormat> ^ callback);
    void StopCapture();

    property AudioFormat CaptureFormat { AudioFormat get(); }

   private:
    LoopbackCaptureWrapper* m_capture;
    bool m_disposed;
};

}  // namespace AudioCapture

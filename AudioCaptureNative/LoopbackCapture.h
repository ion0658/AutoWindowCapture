#pragma once

#include "pch.h"

#include "Common.h"

using namespace Microsoft::WRL;

class CLoopbackCapture
    : public RuntimeClass<RuntimeClassFlags<ClassicCom>,
                          FtmBase,
                          IActivateAudioInterfaceCompletionHandler> {
   public:
    using PcmDataCallback =
        std::function<void(const BYTE* data, UINT32 byteCount)>;

    CLoopbackCapture() = default;
    ~CLoopbackCapture();

    HRESULT StartCaptureAsync(DWORD processId, PcmDataCallback callback);
    HRESULT StopCaptureAsync();

    const WAVEFORMATEX& GetCaptureFormat() const { return m_CaptureFormat; }

    METHODASYNCCALLBACK(CLoopbackCapture, StartCapture, OnStartCapture);
    METHODASYNCCALLBACK(CLoopbackCapture, StopCapture, OnStopCapture);
    METHODASYNCCALLBACK(CLoopbackCapture, SampleReady, OnSampleReady);
    METHODASYNCCALLBACK(CLoopbackCapture, FinishCapture, OnFinishCapture);

    // IActivateAudioInterfaceCompletionHandler
    STDMETHOD(ActivateCompleted)(
        IActivateAudioInterfaceAsyncOperation* operation);

   private:
    enum class DeviceState {
        Uninitialized,
        Error,
        Initialized,
        Starting,
        Capturing,
        Stopping,
        Stopped,
    };

    HRESULT OnStartCapture(IMFAsyncResult* pResult);
    HRESULT OnStopCapture(IMFAsyncResult* pResult);
    HRESULT OnFinishCapture(IMFAsyncResult* pResult);
    HRESULT OnSampleReady(IMFAsyncResult* pResult);

    HRESULT InitializeLoopbackCapture();
    HRESULT OnAudioSampleRequested();

    HRESULT ActivateAudioInterface(DWORD processId);
    HRESULT FinishCaptureAsync();

    HRESULT SetDeviceStateErrorIfFailed(HRESULT hr);

    wil::com_ptr_nothrow<IAudioClient> m_AudioClient;
    WAVEFORMATEX m_CaptureFormat{};
    UINT32 m_BufferFrames = 0;
    wil::com_ptr_nothrow<IAudioCaptureClient> m_AudioCaptureClient;
    wil::com_ptr_nothrow<IMFAsyncResult> m_SampleReadyAsyncResult;

    wil::unique_event_nothrow m_SampleReadyEvent;
    MFWORKITEM_KEY m_SampleReadyKey = 0;
    wil::critical_section m_CritSec;
    DWORD m_dwQueueID = 0;

    PcmDataCallback m_dataCallback;

    HRESULT m_activateResult = E_UNEXPECTED;

    DeviceState m_DeviceState{DeviceState::Uninitialized};
    wil::unique_event_nothrow m_hActivateCompleted;
    wil::unique_event_nothrow m_hCaptureStopped;
};

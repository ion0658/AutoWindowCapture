#include "pch.h"

#include "WindowAudioCaptureNative.AudioFormat.h"
#include "WindowAudioCaptureNative.WindowAudioCapture.h"

#include "WindowAudioCaptureNative.WindowAudioCapture.g.cpp"

// ---------------------------------------------------------------------------
// METHODASYNCCALLBACK macro
// Generates a nested IMFAsyncCallback implementation that delegates back to
// the parent's method.  Identical in semantics to the Windows SDK sample.
// ---------------------------------------------------------------------------
#ifndef METHODASYNCCALLBACK
#define METHODASYNCCALLBACK(Parent, AsyncCallback, pfnCallback)              \
    class Callback##AsyncCallback : public IMFAsyncCallback {                \
    public:                                                                  \
        Callback##AsyncCallback()                                            \
            : _parent(((Parent*)((BYTE*)this -                               \
                       offsetof(Parent, m_x##AsyncCallback)))),              \
              _dwQueueID(MFASYNC_CALLBACK_QUEUE_MULTITHREADED) {}            \
        STDMETHOD_(ULONG, AddRef)() { return _parent->AddRef(); }            \
        STDMETHOD_(ULONG, Release)() { return _parent->Release(); }          \
        STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) {           \
            if (riid == IID_IMFAsyncCallback || riid == IID_IUnknown) {      \
                *ppvObject = this; AddRef(); return S_OK;                    \
            }                                                                \
            *ppvObject = NULL; return E_NOINTERFACE;                         \
        }                                                                    \
        STDMETHOD(GetParameters)(__RPC__out DWORD* pdwFlags,                 \
                                 __RPC__out DWORD* pdwQueue) {               \
            *pdwFlags = 0; *pdwQueue = _dwQueueID; return S_OK;              \
        }                                                                    \
        STDMETHOD(Invoke)(__RPC__out IMFAsyncResult* pResult) {              \
            _parent->pfnCallback(pResult); return S_OK;                      \
        }                                                                    \
        void SetQueueID(DWORD dwQueueID) { _dwQueueID = dwQueueID; }         \
    protected:                                                               \
        Parent* _parent;                                                     \
        DWORD _dwQueueID;                                                    \
    } m_x##AsyncCallback
#endif

namespace winrt::WindowAudioCaptureNative::implementation
{
    constexpr int BITS_PER_BYTE = 8;

    // -----------------------------------------------------------------------
    // CLoopbackCapture
    // Pure WRL COM class (not a WinRT runtime class).  Implements process-
    // loopback audio capture via WASAPI and Media Foundation work queues.
    // -----------------------------------------------------------------------
    class CLoopbackCapture
        : public ::Microsoft::WRL::RuntimeClass<
              ::Microsoft::WRL::RuntimeClassFlags<::Microsoft::WRL::ClassicCom>,
              ::Microsoft::WRL::FtmBase,
              IActivateAudioInterfaceCompletionHandler>
    {
    public:
        using PcmDataCallback = std::function<void(const BYTE* data, UINT32 byteCount)>;

        CLoopbackCapture() = default;
        ~CLoopbackCapture();

        HRESULT StartCaptureAsync(DWORD processId, PcmDataCallback callback);
        HRESULT StopCaptureAsync();

        const WAVEFORMATEX& GetCaptureFormat() const { return m_CaptureFormat; }

        METHODASYNCCALLBACK(CLoopbackCapture, StartCapture, OnStartCapture);
        METHODASYNCCALLBACK(CLoopbackCapture, StopCapture,  OnStopCapture);
        METHODASYNCCALLBACK(CLoopbackCapture, SampleReady,  OnSampleReady);
        METHODASYNCCALLBACK(CLoopbackCapture, FinishCapture, OnFinishCapture);

        // IActivateAudioInterfaceCompletionHandler
        STDMETHOD(ActivateCompleted)(IActivateAudioInterfaceAsyncOperation* operation);

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

        wil::com_ptr_nothrow<IAudioClient>        m_AudioClient;
        WAVEFORMATEX                               m_CaptureFormat{};
        UINT32                                     m_BufferFrames = 0;
        wil::com_ptr_nothrow<IAudioCaptureClient>  m_AudioCaptureClient;
        wil::com_ptr_nothrow<IMFAsyncResult>       m_SampleReadyAsyncResult;
        wil::unique_event_nothrow                  m_SampleReadyEvent;
        MFWORKITEM_KEY                             m_SampleReadyKey = 0;
        wil::critical_section                      m_CritSec;
        DWORD                                      m_dwQueueID = 0;
        PcmDataCallback                            m_dataCallback;
        HRESULT                                    m_activateResult = E_UNEXPECTED;
        DeviceState                                m_DeviceState{ DeviceState::Uninitialized };
        wil::unique_event_nothrow                  m_hActivateCompleted;
        wil::unique_event_nothrow                  m_hCaptureStopped;
    };

    // -----------------------------------------------------------------------
    // CLoopbackCapture implementation
    // -----------------------------------------------------------------------

    HRESULT CLoopbackCapture::SetDeviceStateErrorIfFailed(HRESULT hr)
    {
        if (FAILED(hr)) {
            m_DeviceState = DeviceState::Error;
        }
        return hr;
    }

    HRESULT CLoopbackCapture::InitializeLoopbackCapture()
    {
        RETURN_IF_FAILED(m_SampleReadyEvent.create(wil::EventOptions::None));
        RETURN_IF_FAILED(MFStartup(MF_VERSION, MFSTARTUP_LITE));

        DWORD dwTaskID = 0;
        RETURN_IF_FAILED(MFLockSharedWorkQueue(L"Capture", 0, &dwTaskID, &m_dwQueueID));

        m_xSampleReady.SetQueueID(m_dwQueueID);

        RETURN_IF_FAILED(m_hActivateCompleted.create(wil::EventOptions::None));
        RETURN_IF_FAILED(m_hCaptureStopped.create(wil::EventOptions::None));

        return S_OK;
    }

    CLoopbackCapture::~CLoopbackCapture()
    {
        if (m_dwQueueID != 0) {
            MFUnlockWorkQueue(m_dwQueueID);
        }
    }

    HRESULT CLoopbackCapture::ActivateAudioInterface(DWORD processId)
    {
        return SetDeviceStateErrorIfFailed([&]() -> HRESULT {
            AUDIOCLIENT_ACTIVATION_PARAMS params = {};
            params.ActivationType = AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK;
            params.ProcessLoopbackParams.ProcessLoopbackMode =
                PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE;
            params.ProcessLoopbackParams.TargetProcessId = processId;

            PROPVARIANT activateParams = {};
            activateParams.vt = VT_BLOB;
            activateParams.blob.cbSize = sizeof(params);
            activateParams.blob.pBlobData = reinterpret_cast<BYTE*>(&params);

            wil::com_ptr_nothrow<IActivateAudioInterfaceAsyncOperation> asyncOp;
            RETURN_IF_FAILED(ActivateAudioInterfaceAsync(
                VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK,
                __uuidof(IAudioClient),
                &activateParams,
                this,
                &asyncOp));

            m_hActivateCompleted.wait();
            return m_activateResult;
        }());
    }

    HRESULT CLoopbackCapture::ActivateCompleted(
        IActivateAudioInterfaceAsyncOperation* operation)
    {
        m_activateResult = SetDeviceStateErrorIfFailed([&]() -> HRESULT {
            HRESULT hrActivateResult = E_UNEXPECTED;
            wil::com_ptr_nothrow<IUnknown> punkAudioInterface;
            RETURN_IF_FAILED(operation->GetActivateResult(&hrActivateResult,
                                                          &punkAudioInterface));
            RETURN_IF_FAILED(hrActivateResult);

            RETURN_IF_FAILED(punkAudioInterface.copy_to(&m_AudioClient));

            m_CaptureFormat.wFormatTag      = WAVE_FORMAT_PCM;
            m_CaptureFormat.nChannels       = 2;
            m_CaptureFormat.nSamplesPerSec  = 44100;
            m_CaptureFormat.wBitsPerSample  = 16;
            m_CaptureFormat.nBlockAlign     =
                m_CaptureFormat.nChannels * m_CaptureFormat.wBitsPerSample / BITS_PER_BYTE;
            m_CaptureFormat.nAvgBytesPerSec =
                m_CaptureFormat.nSamplesPerSec * m_CaptureFormat.nBlockAlign;

            RETURN_IF_FAILED(m_AudioClient->Initialize(
                AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_LOOPBACK |
                AUDCLNT_STREAMFLAGS_EVENTCALLBACK |
                AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM |
                AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY,
                0, 0, &m_CaptureFormat, nullptr));

            RETURN_IF_FAILED(m_AudioClient->GetBufferSize(&m_BufferFrames));
            RETURN_IF_FAILED(
                m_AudioClient->GetService(IID_PPV_ARGS(&m_AudioCaptureClient)));

            RETURN_IF_FAILED(MFCreateAsyncResult(nullptr, &m_xSampleReady, nullptr,
                                                  &m_SampleReadyAsyncResult));

            RETURN_IF_FAILED(m_AudioClient->SetEventHandle(m_SampleReadyEvent.get()));

            m_DeviceState = DeviceState::Initialized;
            return S_OK;
        }());

        m_hActivateCompleted.SetEvent();
        return S_OK;
    }

    HRESULT CLoopbackCapture::StartCaptureAsync(DWORD processId, PcmDataCallback callback)
    {
        m_dataCallback = std::move(callback);

        RETURN_IF_FAILED(InitializeLoopbackCapture());
        RETURN_IF_FAILED(ActivateAudioInterface(processId));

        if (m_DeviceState == DeviceState::Initialized) {
            m_DeviceState = DeviceState::Starting;
            return MFPutWorkItem2(MFASYNC_CALLBACK_QUEUE_MULTITHREADED, 0,
                                  &m_xStartCapture, nullptr);
        }
        return S_OK;
    }

    HRESULT CLoopbackCapture::OnStartCapture(IMFAsyncResult*)
    {
        return SetDeviceStateErrorIfFailed([&]() -> HRESULT {
            RETURN_IF_FAILED(m_AudioClient->Start());
            m_DeviceState = DeviceState::Capturing;
            MFPutWaitingWorkItem(m_SampleReadyEvent.get(), 0,
                                 m_SampleReadyAsyncResult.get(), &m_SampleReadyKey);
            return S_OK;
        }());
    }

    HRESULT CLoopbackCapture::StopCaptureAsync()
    {
        RETURN_HR_IF(E_NOT_VALID_STATE,
                     (m_DeviceState != DeviceState::Capturing) &&
                     (m_DeviceState != DeviceState::Error));

        m_DeviceState = DeviceState::Stopping;

        RETURN_IF_FAILED(MFPutWorkItem2(MFASYNC_CALLBACK_QUEUE_MULTITHREADED, 0,
                                        &m_xStopCapture, nullptr));

        m_hCaptureStopped.wait();
        return S_OK;
    }

    HRESULT CLoopbackCapture::OnStopCapture(IMFAsyncResult*)
    {
        if (m_SampleReadyKey != 0) {
            MFCancelWorkItem(m_SampleReadyKey);
            m_SampleReadyKey = 0;
        }
        m_AudioClient->Stop();
        m_SampleReadyAsyncResult.reset();
        return FinishCaptureAsync();
    }

    HRESULT CLoopbackCapture::FinishCaptureAsync()
    {
        return MFPutWorkItem2(MFASYNC_CALLBACK_QUEUE_MULTITHREADED, 0,
                              &m_xFinishCapture, nullptr);
    }

    HRESULT CLoopbackCapture::OnFinishCapture(IMFAsyncResult*)
    {
        // Clear the callback first to break the potential ref-cycle
        // (the callback may hold a ComPtr back to this object).
        m_dataCallback = {};
        m_DeviceState  = DeviceState::Stopped;
        m_hCaptureStopped.SetEvent();
        return S_OK;
    }

    HRESULT CLoopbackCapture::OnSampleReady(IMFAsyncResult*)
    {
        if (SUCCEEDED(OnAudioSampleRequested())) {
            if (m_DeviceState == DeviceState::Capturing) {
                return MFPutWaitingWorkItem(m_SampleReadyEvent.get(), 0,
                                            m_SampleReadyAsyncResult.get(),
                                            &m_SampleReadyKey);
            }
        } else {
            m_DeviceState = DeviceState::Error;
        }
        return S_OK;
    }

    HRESULT CLoopbackCapture::OnAudioSampleRequested()
    {
        UINT32 framesAvailable = 0;
        BYTE*  data            = nullptr;
        DWORD  captureFlags    = 0;
        UINT64 devicePos       = 0;
        UINT64 qpcPos          = 0;

        auto lock = m_CritSec.lock();

        if (m_DeviceState == DeviceState::Stopping) {
            return S_OK;
        }

        while (SUCCEEDED(m_AudioCaptureClient->GetNextPacketSize(&framesAvailable)) &&
               framesAvailable > 0)
        {
            DWORD cbBytes = framesAvailable * m_CaptureFormat.nBlockAlign;

            RETURN_IF_FAILED(m_AudioCaptureClient->GetBuffer(
                &data, &framesAvailable, &captureFlags, &devicePos, &qpcPos));

            if (m_DeviceState != DeviceState::Stopping && m_dataCallback) {
                m_dataCallback(data, cbBytes);
            }

            m_AudioCaptureClient->ReleaseBuffer(framesAvailable);
        }
        return S_OK;
    }

    // -----------------------------------------------------------------------
    // WindowAudioCapture implementation
    // -----------------------------------------------------------------------

    void WindowAudioCapture::StartCapture(uint32_t processId)
    {
        ::Microsoft::WRL::ComPtr<CLoopbackCapture> capture;
        {
            std::scoped_lock lock(m_mutex);
            if (m_capture) {
                m_capture->StopCaptureAsync();
            }
            capture   = ::Microsoft::WRL::Make<CLoopbackCapture>();
            m_capture = capture;
        }

        CLoopbackCapture::PcmDataCallback cb =
            [weakThis = get_weak(),
             captureRef = ::Microsoft::WRL::ComPtr<CLoopbackCapture>(capture)](
                const BYTE* data, UINT32 byteCount)
        {
            auto strongThis = weakThis.get();
            if (!strongThis) return;
            try {
                const WAVEFORMATEX& fmt = captureRef->GetCaptureFormat();
                auto audioFormat = winrt::make<implementation::AudioFormat>(
                    static_cast<int32_t>(fmt.nSamplesPerSec),
                    static_cast<int32_t>(fmt.wBitsPerSample),
                    static_cast<int32_t>(fmt.nChannels));

                winrt::array_view<uint8_t const> dataView(data, data + byteCount);
                strongThis->m_audioDataReceived(dataView, audioFormat);
            }
            catch (...) {}
        };

        winrt::check_hresult(
            capture->StartCaptureAsync(static_cast<DWORD>(processId), std::move(cb)));
    }

    void WindowAudioCapture::StopCapture()
    {
        std::scoped_lock lock(m_mutex);
        if (m_capture) {
            m_capture->StopCaptureAsync();
        }
    }

    winrt::WindowAudioCaptureNative::AudioFormat WindowAudioCapture::CaptureFormat()
    {
        std::scoped_lock lock(m_mutex);
        if (!m_capture) {
            return winrt::make<implementation::AudioFormat>(0, 0, 0);
        }
        const WAVEFORMATEX& fmt = m_capture->GetCaptureFormat();
        return winrt::make<implementation::AudioFormat>(
            static_cast<int32_t>(fmt.nSamplesPerSec),
            static_cast<int32_t>(fmt.wBitsPerSample),
            static_cast<int32_t>(fmt.nChannels));
    }

    winrt::event_token WindowAudioCapture::AudioDataReceived(
        winrt::WindowAudioCaptureNative::AudioDataReceivedHandler const& handler)
    {
        return m_audioDataReceived.add(handler);
    }

    void WindowAudioCapture::AudioDataReceived(winrt::event_token const& token) noexcept
    {
        m_audioDataReceived.remove(token);
    }

    void WindowAudioCapture::Close()
    {
        StopCapture();
    }
}

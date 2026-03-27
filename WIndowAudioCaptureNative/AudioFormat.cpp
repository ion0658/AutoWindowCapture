#include "pch.h"
#include "WindowAudioCaptureNative.AudioFormat.h"
#include "WindowAudioCaptureNative.AudioFormat.g.cpp"

namespace winrt::WindowAudioCaptureNative::implementation
{
    AudioFormat::AudioFormat(int32_t SampleRate, int32_t BitsPerSample, int32_t Channels)
        : m_sampleRate(SampleRate), m_bitsPerSample(BitsPerSample), m_channels(Channels)
    {
    }
    int32_t AudioFormat::SampleRate()
    {
        return m_sampleRate;
    }
    int32_t AudioFormat::BitsPerSample()
    {
        return m_bitsPerSample;
    }
    int32_t AudioFormat::Channels()
    {
        return m_channels;
    }
}

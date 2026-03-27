#pragma once
#include "WindowAudioCaptureNative.AudioFormat.g.h"

namespace winrt::WindowAudioCaptureNative::implementation
{
    struct AudioFormat : AudioFormatT<AudioFormat>
    {
        AudioFormat() = default;

        AudioFormat(int32_t SampleRate, int32_t BitsPerSample, int32_t Channels);
        int32_t SampleRate();
        int32_t BitsPerSample();
        int32_t Channels();

    private:
        int32_t m_sampleRate{};
        int32_t m_bitsPerSample{};
        int32_t m_channels{};
    };
}
namespace winrt::WindowAudioCaptureNative::factory_implementation
{
    struct AudioFormat : AudioFormatT<AudioFormat, implementation::AudioFormat>
    {
    };
}

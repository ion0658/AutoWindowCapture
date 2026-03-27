using System;
using System.Collections.Generic;
using WindowEnumeratorNative;
using WindowListPage.Services;

namespace AutoWindowCapture.Services;

public sealed class RecordingWindowLauncher : IRecordingWindowLauncher
{
    private readonly Dictionary<long, RecordingWindow.Views.RecordingWindow> _windowsByHandle = [];

    public void OpenOrActivate(WindowInfo windowInfo, bool recOnOpen)
    {
        RecordingWindow.Views.RecordingWindow recordingWindow = GetOrCreateWindow(windowInfo, recOnOpen);
        recordingWindow.Activate();
    }

    private RecordingWindow.Views.RecordingWindow GetOrCreateWindow(WindowInfo windowInfo, bool recOnOpen)
    {
        if (_windowsByHandle.TryGetValue(windowInfo.Handle, out RecordingWindow.Views.RecordingWindow? existingWindow))
        {
            return existingWindow;
        }

        RecordingWindow.Views.RecordingWindow recordingWindow = new(windowInfo, recOnOpen);
        _windowsByHandle[windowInfo.Handle] = recordingWindow;
        recordingWindow.Closed += (_, _) => _windowsByHandle.Remove(windowInfo.Handle);
        return recordingWindow;
    }
}


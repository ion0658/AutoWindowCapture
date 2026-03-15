using System;
using System.Collections.Generic;
using WindowEnumerator;
using WindowListPage.Services;

namespace AutoWindowCapture.Services;

public sealed class RecordingWindowLauncher : IRecordingWindowLauncher {
    private readonly Dictionary<IntPtr, RecordingWindow.Views.RecordingWindow> _windowsByHandle = [];

    public void OpenOrActivate(WindowInfo windowInfo) {
        if (_windowsByHandle.TryGetValue(windowInfo.Handle, out RecordingWindow.Views.RecordingWindow? existingWindow)) {
            existingWindow.Activate();
            return;
        }

        RecordingWindow.Views.RecordingWindow recordingWindow = new(windowInfo);

        _windowsByHandle[windowInfo.Handle] = recordingWindow;
        recordingWindow.Closed += (_, _) => _windowsByHandle.Remove(windowInfo.Handle);
        recordingWindow.Activate();
    }
}


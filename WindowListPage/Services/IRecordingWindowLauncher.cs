using WindowEnumeratorNative;

namespace WindowListPage.Services;

public interface IRecordingWindowLauncher
{
    void OpenOrActivate(WindowInfo windowInfo, bool recOnOpen);
}


using WindowEnumerator;

namespace WindowListPage.Services;

public interface IRecordingWindowLauncher
{
    void OpenOrActivate(WindowInfo windowInfo);
}


using System.Threading.Tasks;
using WindowEnumerator;

namespace WindowListPage.Services;

public interface IRecordingWindowLauncher
{
    void OpenOrActivate(WindowInfo windowInfo, bool recOnOpen);
}


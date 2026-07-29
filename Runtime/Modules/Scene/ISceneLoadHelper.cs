using UnityEngine.SceneManagement;
using XFramework.Tasks;

namespace XFramework
{
    internal interface ISceneLoadHelper
    {
        XAwaitableTask<bool> LoadSceneAsync(string scenePath);
        XAwaitableTask<bool> UnloadSceneAsync(Scene scene);
    }
}

using UnityEngine.SceneManagement;
using XFramework.Tasks;

namespace XFramework
{
    internal interface ISceneLoadHelper
    {
        bool LoadScene(string scenePath, LoadSceneMode mode);
        bool LoadScene(int buildIndex, LoadSceneMode mode);
        XAwaitableTask<bool> LoadSceneAsync(string scenePath, LoadSceneMode mode);
        XAwaitableTask<bool> LoadSceneAsync(int buildIndex, LoadSceneMode mode);
        XAwaitableTask<bool> UnloadSceneAsync(string sceneName);
        XAwaitableTask<bool> UnloadSceneAsync(int buildIndex);
        XAwaitableTask<bool> UnloadSceneAsync(Scene scene);
    }
}

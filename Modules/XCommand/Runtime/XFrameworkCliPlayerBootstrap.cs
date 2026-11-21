#if !UNITY_EDITOR && DEVELOPMENT_BUILD
using System.IO;
using UnityEngine;

namespace XFramework.Command
{
    internal sealed class XFrameworkCliPlayerBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            var gameObject = new GameObject("XFramework CLI Server") {
                hideFlags = HideFlags.HideAndDontSave,
            };
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<XFrameworkCliPlayerBootstrap>();
        }

        private void Awake()
        {
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            try
            {
                XCommand.Enable(XFrameworkCliProcessType.DevelopmentPlayer, projectPath);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void Update()
        {
            XCommand.Pump();
        }

        private void OnApplicationQuit()
        {
            XCommand.Disable();
        }

        private void OnDestroy()
        {
            XCommand.Disable();
        }
    }
}
#endif

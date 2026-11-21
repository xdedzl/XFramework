using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    [InitializeOnLoad]
    internal static class XFrameworkCliServerBootstrap
    {
        static XFrameworkCliServerBootstrap()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            try
            {
                Start();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        internal static void Start()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            XCommand.Enable(XFrameworkCliProcessType.Editor, projectPath);
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;
        }

        internal static void Stop()
        {
            EditorApplication.update -= Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;
            XCommand.Disable();
        }

        private static void Update()
        {
            XCommand.Pump();
        }

        private static void Shutdown()
        {
            Stop();
        }
    }
}

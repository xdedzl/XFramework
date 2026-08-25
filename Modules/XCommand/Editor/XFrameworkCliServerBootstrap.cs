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

            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
            try
            {
                string projectPath = Directory.GetParent(Application.dataPath).FullName;
                XFrameworkCliServer.Start(XFrameworkCliProcessType.Editor, projectPath);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Update()
        {
            XFrameworkCliServer.Pump();
        }

        private static void Shutdown()
        {
            EditorApplication.update -= Update;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            EditorApplication.quitting -= Shutdown;
            XFrameworkCliServer.Stop();
        }
    }
}

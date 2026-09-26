using System;
using UnityEditor;
using XFramework.Command;

namespace XFramework.Editor
{
    [InitializeOnLoad]
    public sealed class XCommandPlayModeCommands : XCommandGroup
    {
        private const string Category = "Editor";

        static XCommandPlayModeCommands()
        {
            XCommandPipeline.SetEditorStopHandler(StopPlayMode);
            XCommandUtility.Logs.InitializeCapture();
        }

        [XCommandEntry("play-start", name = "开始运行", order = 0, mode = XCommandMode.Editor, category = Category, description = "让 Unity Editor 进入 Play Mode。", usage = "play-start", displayType = XCommandDisplayType.Hidden)]
        private static object StartPlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Unity is already entering Play Mode.");
            }
            EditorApplication.isPlaying = true;
            return "Entering Play Mode.";
        }

        private static void StopPlayMode()
        {
            EditorApplication.isPlaying = false;
        }
    }
}

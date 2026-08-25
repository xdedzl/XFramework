using UnityEditor;
using XFramework.AutoTest;
using XFramework.Command;

namespace XFramework.Editor
{
    [InitializeOnLoad]
    public sealed class AutoTestPipelineEditorCommands : XCommandGroup
    {
        private const string Category = "AutoTest";

        static AutoTestPipelineEditorCommands()
        {
            AutoTestPipeline.SetEditorStopHandler(StopPlayMode);
            AutoTestPipeline.InitializeLogCapture();
        }

        [XCommandEntry("play-start", name = "开始运行", order = 0, mode = XCommandMode.Editor, category = Category, description = "让 Unity Editor 进入 Play Mode。", usage = "play-start", displayType = XCommandDisplayType.Hidden)]
        private static object StartPlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new System.InvalidOperationException("Unity is already entering Play Mode.");
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

using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    public sealed class XCommandEditorCommands : XCommandGroup
    {
        private const double RecompileDelaySeconds = 0.5d;
        private static bool s_RecompileScheduled;
        private static double s_RecompileTime;

        [XCommandEntry("editor_selection", name = "查看当前选择", order = 0, mode = XCommandMode.Editor, category = "Editor", description = "返回 Unity Editor 当前选择对象的名称、类型和资源路径。", usage = "editor_selection")]
        private static object GetCurrentSelection()
        {
            Object selectedObject = Selection.activeObject;
            if (selectedObject == null)
            {
                return "当前没有选择对象。";
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            string pathText = string.IsNullOrEmpty(assetPath) ? "<Scene Object>" : assetPath;
            return $"Name: {selectedObject.name}\nType: {selectedObject.GetType().FullName}\nPath: {pathText}";
        }

        [XCommandEntry("recompile", name = "重新编译脚本", order = 10, mode = XCommandMode.Editor, category = "Editor", description = "刷新 AssetDatabase 并请求 Unity 重新编译脚本；通过 CLI 执行时会先返回响应，再开始编译。", usage = "recompile", displayType = XCommandDisplayType.Hidden)]
        private static object Recompile()
        {
            if (EditorApplication.isCompiling)
            {
                return "Unity 当前正在编译脚本。";
            }
            if (s_RecompileScheduled)
            {
                return "重新编译请求已在队列中。";
            }

            s_RecompileScheduled = true;
            s_RecompileTime = EditorApplication.timeSinceStartup + RecompileDelaySeconds;
            EditorApplication.update += RequestRecompile;
            return "已提交重新编译请求，Unity 将在短暂延迟后开始编译。";
        }

        private static void RequestRecompile()
        {
            if (EditorApplication.timeSinceStartup < s_RecompileTime)
            {
                return;
            }

            EditorApplication.update -= RequestRecompile;
            s_RecompileScheduled = false;
            if (EditorApplication.isCompiling)
            {
                Debug.Log("[XCommand] Unity 已开始编译，无需重复提交编译请求。");
                return;
            }

            Debug.Log("[XCommand] 刷新 AssetDatabase 并请求重新编译脚本。");
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            if (!EditorApplication.isCompiling)
            {
                CompilationPipeline.RequestScriptCompilation();
            }
        }
    }
}

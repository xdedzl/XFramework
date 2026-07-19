using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace XFramework.Editor
{
    /// <summary>
    /// 全局快捷键：Ctrl+Shift+D，强制执行一次 Domain Reload。
    /// </summary>
    internal static class ReloadDomainShortcut
    {
        [Shortcut("XFramework/Reload Domain", KeyCode.D, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        private static void ReloadDomain()
        {
            if (EditorApplication.isCompiling)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Debug.LogWarning("[ReloadDomainShortcut] 运行模式下无法触发 Domain Reload。");
                return;
            }

            Debug.Log("[ReloadDomainShortcut] 触发 Domain Reload。");
            EditorApplication.delayCall += () => CompilationPipeline.RequestScriptCompilation();
        }
    }
}


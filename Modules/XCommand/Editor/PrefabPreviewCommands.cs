using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using XFramework.AutoTest;
using XFramework.Command;

namespace XFramework.Editor
{
    public sealed class PrefabPreviewCommands : XCommandGroup
    {
        [Serializable]
        private sealed class PreviewReceipt
        {
            public bool success;
            public string prefabPath;
            public string path;
            public string mode;
            public int width;
            public int height;
        }

        [XCommandEntry("preview-prefab", name = "预览Prefab", order = 20, mode = XCommandMode.Editor, category = "Editor", description = "在临时场景中离屏渲染 UI 或模型 Prefab 并导出 PNG，完成后恢复活动场景。", usage = "preview-prefab [--width W --height H] [--background #RRGGBB[AA]] PREFAB_PATH PNG_PATH")]
        private static object PreviewPrefab(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "width", "height", "background" }, Array.Empty<string>());
            args.RequireValueCount(2, "usage: preview-prefab [--width W --height H] [--background #RRGGBB[AA]] PREFAB_PATH PNG_PATH");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请等待 Unity 完成编译和资源导入，并退出 Play Mode 后再预览 Prefab。");

            int width = args.GetInt("width", 1920);
            int height = args.GetInt("height", 1080);
            int maxSize = Mathf.Min(8192, SystemInfo.maxTextureSize);
            if (width <= 0 || height <= 0 || width > maxSize || height > maxSize)
                throw new ArgumentException($"预览宽高必须在 1 到 {maxSize} 之间。");
            string backgroundText = args.GetString("background", "#202622FF");
            if (!ColorUtility.TryParseHtmlString(backgroundText, out Color background))
                throw new ArgumentException($"无效背景色：{backgroundText}，请使用 #RRGGBB 或 #RRGGBBAA。");

            string prefabPath = args.Values[0].Replace('\\', '/');
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                throw new ArgumentException($"找不到 Prefab 资源：{prefabPath}，请使用 Assets/ 或 Packages/ 开头的资源路径。");
            string outputPath = AutoTestCapture.ResolvePath(args.Values[1]);
            if (!string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("预览输出路径必须以 .png 结尾。");

            bool isUI = prefab.GetComponent<RectTransform>() != null;
            PrefabPreviewUtility.Render(prefab, outputPath, width, height, background, isUI);
            return JsonUtility.ToJson(new PreviewReceipt {
                success = true,
                prefabPath = prefabPath,
                path = outputPath.Replace('\\', '/'),
                mode = isUI ? "ui" : "model",
                width = width,
                height = height,
            });
        }

    }
}

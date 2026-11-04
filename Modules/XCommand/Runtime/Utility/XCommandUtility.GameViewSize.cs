#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace XFramework.Command
{
    public static partial class XCommandUtility
    {
        /// <summary>
        /// GameView 尺寸控制（编辑器专用，通过反射访问 UnityEditor 内部 API）。
        /// 可最大化 / 还原 GameView 窗口，或切换为指定固定分辨率，
        /// 便于离屏录制（record-offscreen）得到足够大的画面。
        /// </summary>
        internal static class GameViewSize
        {
            internal const string Usage = "gameview-size [--width W --height H] [--maximize] [--restore]";

            private const string GameViewTypeName = "UnityEditor.GameView";
            private const string GameViewSizesTypeName = "UnityEditor.GameViewSizes";
            private const string GameViewSizeTypeName = "UnityEditor.GameViewSize";
            private const string GameViewSizeTypeEnumName = "UnityEditor.GameViewSizeType";
            private const string GameViewSizeGroupTypeName = "UnityEditor.GameViewSizeGroup";

            internal static object Execute(string argument)
            {
                XCommandArguments args = XCommandArguments.Parse(
                    argument,
                    new[] { "width", "height" },
                    new[] { "maximize", "restore" });
                args.RequireValueCount(0, $"usage: {Usage}");

                int width = args.GetInt("width", 0);
                int height = args.GetInt("height", 0);
                if (width > 0 != height > 0)
                {
                    throw new ArgumentException("--width 与 --height 必须同时指定。");
                }

                EditorWindow gameView = FindGameView();
                if (gameView == null)
                {
                    throw new InvalidOperationException("未找到打开的 GameView 窗口，请先在 Unity 中打开 Game 视图。");
                }

                bool maximize = args.HasFlag("maximize");
                bool restore = args.HasFlag("restore");
                string action;

                if (maximize || restore)
                {
                    gameView.maximized = maximize && !restore;
                    gameView.Repaint();
                    action = gameView.maximized ? "已最大化 GameView" : "已还原 GameView";
                }
                else if (width > 0)
                {
                    int index = EnsureFixedResolutionSize(width, height);
                    SelectSize(gameView, index);
                    action = $"已把 GameView 切换为固定分辨率 {width}x{height}";
                }
                else
                {
                    action = "未指定操作（--maximize / --restore / --width --height），仅返回当前状态。";
                }

                // GameView 尺寸变化后 Screen.width/height 需要下一帧才会刷新，
                // 这里同时返回窗口矩形尺寸作为即时参考。
                return $"{action}\n" +
                       $"GameView 窗口矩形：{(int)gameView.position.width}x{(int)gameView.position.height}，maximized={gameView.maximized}\n" +
                       $"当前 Screen：{Screen.width}x{Screen.height}（下一帧刷新后可再次用 app-state 确认）";
            }

            private static EditorWindow FindGameView()
            {
                Type gameViewType = typeof(Editor).Assembly.GetType(GameViewTypeName);
                if (gameViewType == null)
                {
                    return null;
                }

                UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(gameViewType);
                for (int i = 0; i < windows.Length; i++)
                {
                    if (windows[i] is EditorWindow window)
                    {
                        return window;
                    }
                }

                return null;
            }

            private static void SelectSize(EditorWindow gameView, int index)
            {
                Type gameViewType = typeof(Editor).Assembly.GetType(GameViewTypeName);
                MethodInfo callback = gameViewType.GetMethod(
                    "SizeSelectionCallback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int), typeof(object) },
                    null);
                if (callback == null)
                {
                    throw new InvalidOperationException("未找到 GameView.SizeSelectionCallback，Unity 版本可能不兼容。");
                }

                callback.Invoke(gameView, new object[] { index, null });
                gameView.Repaint();
            }

            /// <summary>确保当前 GameView 尺寸分组中存在指定固定分辨率，返回其索引。</summary>
            private static int EnsureFixedResolutionSize(int width, int height)
            {
                Assembly editorAssembly = typeof(Editor).Assembly;
                object sizesInstance = GetGameViewSizesInstance(editorAssembly);
                object group = GetCurrentSizeGroup(sizesInstance, editorAssembly, sizesType: editorAssembly.GetType(GameViewSizesTypeName));
                Type groupType = editorAssembly.GetType(GameViewSizeGroupTypeName);
                Type sizeType = editorAssembly.GetType(GameViewSizeTypeName);
                Type sizeEnumType = editorAssembly.GetType(GameViewSizeTypeEnumName);

                MethodInfo getTotalCount = groupType.GetMethod("GetTotalCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo getGameViewSize = groupType.GetMethod(
                    "GetGameViewSize",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(int) },
                    null);
                MethodInfo addCustomSize = groupType.GetMethod(
                    "AddCustomSize",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { sizeType },
                    null);

                int total = (int)getTotalCount.Invoke(group, null);
                for (int i = 0; i < total; i++)
                {
                    object size = getGameViewSize.Invoke(group, new object[] { i });
                    string sizeTypeName = GetMember(sizeType, size, "sizeType").ToString();
                    int sizeWidth = (int)GetMember(sizeType, size, "width");
                    int sizeHeight = (int)GetMember(sizeType, size, "height");
                    if (sizeTypeName == "FixedResolution" && sizeWidth == width && sizeHeight == height)
                    {
                        return i;
                    }
                }

                object enumValue = Enum.Parse(sizeEnumType, "FixedResolution");
                object newSize = sizeType
                    .GetConstructor(new[] { sizeEnumType, typeof(int), typeof(int), typeof(string) })
                    .Invoke(new object[] { enumValue, width, height, $"XCommand {width}x{height}" });
                addCustomSize.Invoke(group, new[] { newSize });
                return (int)getTotalCount.Invoke(group, null) - 1;
            }

            private static object GetGameViewSizesInstance(Assembly editorAssembly)
            {
                Type sizesType = editorAssembly.GetType(GameViewSizesTypeName);
                Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                PropertyInfo instanceProperty = singletonType.GetProperty(
                    "instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return instanceProperty.GetValue(null);
            }

            private static object GetCurrentSizeGroup(object sizesInstance, Assembly editorAssembly, Type sizesType)
            {
                MethodInfo getCurrentGroup = sizesType.GetMethod("GetCurrentGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object group = getCurrentGroup?.Invoke(sizesInstance, null);
                if (group != null)
                {
                    return group;
                }

                Type groupEnumType = editorAssembly.GetType("UnityEditor.GameViewSizeGroupType");
                MethodInfo getGroup = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return getGroup.Invoke(sizesInstance, new[] { Enum.Parse(groupEnumType, "Standalone") });
            }

            private static object GetMember(Type type, object instance, string name)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    return property.GetValue(instance);
                }

                throw new MissingMemberException(type.FullName, name);
            }
        }
    }
}
#endif
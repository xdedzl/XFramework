using System;
using System.IO;
using System.Runtime.InteropServices;   // 新增：window-focus 的 DllImport 需要
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using XFramework.Command;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace XFramework.Editor
{
    /// <summary>XCommand 编辑器命令集合（XFramework CLI）。按 #region 分类组织编辑器选择/重编译、Play 模式、
    /// TMP 字体图集烘焙、Prefab 预览、屏幕录制和 GameView 截图等命令；复杂命令只保留入口，
    /// 具体实现位于 Runtime 侧的 XCommandUtility.* partial。</summary>
    [InitializeOnLoad]
    public sealed class XCommandEditorCommands : XCommandGroup
    {
        #region 编辑器

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

        #endregion

        #region Play 模式

        private const string Category = "Editor";

        static XCommandEditorCommands()
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

        #endregion

        #region 字体烘焙

        [XCommandEntry("font_bake",
            name = "烘焙 TMP 字体图集",
            order = 20,
            mode = XCommandMode.Editor,
            category = "Font",
            description = "扫描项目中 TMP 文本用到的字符，把字体图集里缺失的字补进去。默认只报告（dry-run），加 --apply 才会写入字体资产；字体默认取 TMP Settings 的默认字体。字符很多时请配合 CLI 的 --timeout（例如 --timeout 900）。",
            usage = XCommandUtility.FontBake.Usage)]
        private static object BakeTmpFont(string argument)
        {
            return XCommandUtility.FontBake.Execute(argument);
        }

        /// <summary>
        /// 无头模式入口（不依赖打开着的 Editor）。用法：
        ///   Unity.exe -batchmode -quit -projectPath &lt;项目路径&gt;
        ///            -executeMethod XFramework.Editor.XCommandEditorCommands.BakeFontAtlasInBatchMode
        /// 等价于执行一次 <c>font_bake --apply</c>。
        /// </summary>
        public static void BakeFontAtlasInBatchMode()
        {
            XCommandUtility.FontBake.BakeInBatchMode();
        }

        #endregion

        #region Prefab 预览

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
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "width", "height", "background" }, Array.Empty<string>());
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
            string outputPath = XCommandUtility.Path.ResolvePath(args.Values[1]);
            if (!string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("预览输出路径必须以 .png 结尾。");

            bool isUI = prefab.GetComponent<RectTransform>() != null;
            XCommandUtility.PrefabPreview.Render(prefab, outputPath, width, height, background, isUI);
            return JsonUtility.ToJson(new PreviewReceipt {
                success = true,
                prefabPath = prefabPath,
                path = outputPath.Replace('\\', '/'),
                mode = isUI ? "ui" : "model",
                width = width,
                height = height,
            });
        }

        #endregion

        #region 录屏

        [XCommandEntry("record", name = "录屏（固定时长）", order = 40, mode = XCommandMode.Both, category = "Editor",
            description = "调用 ffmpeg 录制屏幕指定秒数后自动停止并输出 MP4；需要系统已安装 ffmpeg。默认录制 GameView 区域，可用 --region 或 --full 覆盖；录制时需保持 Unity 窗口可见。",
            usage = XCommandUtility.ScreenRecord.Usage)]
        private static object Record(string argument)
        {
            return XCommandUtility.ScreenRecord.Execute(argument);
        }

        [XCommandEntry("record-start", name = "开始录屏", order = 41, mode = XCommandMode.Both, category = "Editor",
            description = "开始录屏，需手动执行 record-stop 结束；输出 MP4，需要系统已安装 ffmpeg。默认录制 GameView 区域，可用 --region 或 --full 覆盖。",
            usage = XCommandUtility.ScreenRecord.StartUsage)]
        private static object RecordStart(string argument)
        {
            return XCommandUtility.ScreenRecord.ExecuteStart(argument);
        }

        [XCommandEntry("record-stop", name = "停止录屏", order = 42, mode = XCommandMode.Both, category = "Editor",
            description = "停止当前进行中的录屏，等待 ffmpeg 写完文件后返回输出路径与文件大小。",
            usage = XCommandUtility.ScreenRecord.StopUsage)]
        private static object RecordStop(string argument)
        {
            return XCommandUtility.ScreenRecord.ExecuteStop(argument);
        }

        [XCommandEntry("record-offscreen", name = "离屏录屏", order = 43, mode = XCommandMode.Both, category = "Editor",
            description = "使用 Unity 内置 MediaEncoder 离屏逐帧录制 GameView 并输出 MP4，不落 PNG、不依赖物理桌面是否可见；需要 Play 模式。默认实时录制（默认 30fps、10 秒），加 --frame-step 改用确定性帧步进录制。MediaEncoder 不可用时自动回退到 ffmpeg rawvideo。启动后立即返回，不占用操作队列，因此录制期间可用 ui-input 注入输入；用 record-offscreen-status 查询结果。",
            usage = XCommandUtility.OffscreenRecord.Usage)]
        private static object RecordOffscreen(string argument)
        {
            return XCommandUtility.OffscreenRecord.Execute(argument);
        }

        [XCommandEntry("record-offscreen-stop", name = "停止离屏录屏", order = 44, mode = XCommandMode.Both, category = "Editor",
            description = "请求提前结束当前离屏录制；编码器会在下一帧收尾并写出可播放的分片 MP4。",
            usage = XCommandUtility.OffscreenRecord.StopUsage)]
        private static object RecordOffscreenStop(string argument)
        {
            return XCommandUtility.OffscreenRecord.ExecuteStop(argument);
        }

        [XCommandEntry("record-offscreen-status", name = "查询离屏录屏状态", order = 45, mode = XCommandMode.Both, category = "Editor",
            description = "返回最近一次 / 当前离屏录制的状态、已录帧数、编码器与最终文件信息。",
            usage = XCommandUtility.OffscreenRecord.StatusUsage)]
        private static object RecordOffscreenStatus(string argument)
        {
            return XCommandUtility.OffscreenRecord.ExecuteStatus(argument);
        }

        #endregion

        #region GameView 截图

        [XCommandEntry("gameview-screenshot", name = "截取 GameView", order = 30, mode = XCommandMode.Editor, category = "Editor",
            description = "把当前场景的视图相机（GameView 画面）离屏渲染成 PNG，不需要进入 Play 模式。支持 temp:/ data:/ persistent:/ 路径前缀；Screen Space Overlay 的 UI 不在相机渲染内。",
            usage = XCommandUtility.GameViewCapture.Usage)]
        private static object Capture(string argument)
        {
            return XCommandUtility.GameViewCapture.Execute(argument);
        }

        #endregion

        #region GameView 尺寸

        [XCommandEntry("gameview-size", name = "调整 GameView 尺寸", order = 31, mode = XCommandMode.Editor, category = "Editor",
            description = "调整 GameView 渲染尺寸：--maximize 最大化窗口、--restore 还原，或 --width/--height 切换为指定固定分辨率。用于让离屏录制的画面足够大；不带参数时仅返回当前尺寸状态。",
            usage = XCommandUtility.GameViewSize.Usage)]
        private static object ResizeGameView(string argument)
        {
            return XCommandUtility.GameViewSize.Execute(argument);
        }

        #endregion

        #region 前置窗口

        private const int SW_RESTORE = 9;
        private const byte VK_MENU = 0x12;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        /// <summary>
        /// 把 Unity Editor 主窗口恢复到前台并聚焦 GameView。模拟鼠标/键盘（ui-input）之前调用，
        /// 可显著降低「GameView 不在前台导致点击被吞」的概率。Windows 前台锁可能拒绝抢占，
        /// 因此返回 broughtToFront 供调用方判定；isFocused 可能滞后一帧，可稍后用 app-state 复核。
        /// </summary>
        [XCommandEntry("window-focus", name = "前置 Unity/Game 窗口", order = 12, mode = XCommandMode.Both, displayType = XCommandDisplayType.Hidden, category = Category,
            description = "把 Unity Editor 主窗口恢复到前台并聚焦 GameView，供 ui-input 注入真实鼠标/键盘前调用；Windows 前台锁可能阻止抢占，返回 broughtToFront 供调用方判定。",
            usage = "window-focus")]
        private static object FocusWindow()
        {
            bool gameViewFocused = TryFocusGameView(out string gameViewDetail);

            IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (hwnd == IntPtr.Zero)
            {
                return $"前置窗口失败：未取得 Unity 主窗口句柄。{gameViewDetail}";
            }

            ShowWindow(hwnd, SW_RESTORE);

            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = foreground == IntPtr.Zero ? 0u : GetWindowThreadProcessId(foreground, out _);
            uint currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0 && foregroundThread != currentThread
                && AttachThreadInput(foregroundThread, currentThread, true);

            bool broughtToFront;
            try
            {
                // 模拟一次 ALT 按键，满足 Windows「最近有输入」条件，降低前台锁拒绝 SetForegroundWindow 的概率。
                keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
                keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                broughtToFront = SetForegroundWindow(hwnd);
                if (!broughtToFront)
                {
                    broughtToFront = BringWindowToTop(hwnd);
                }
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(foregroundThread, currentThread, false);
                }
            }

            return $"前置窗口请求完成：broughtToFront={broughtToFront}, gameViewFocused={gameViewFocused}, isFocused={Application.isFocused}。{gameViewDetail}";
        }

        /// <summary>
        /// 反射定位（必要时创建）GameView 窗口并聚焦；停靠时切到该标签页，浮动时激活其窗口。
        /// </summary>
        private static bool TryFocusGameView(out string detail)
        {
            detail = string.Empty;
            try
            {
                Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null)
                {
                    detail = "未找到 UnityEditor.GameView 类型。";
                    return false;
                }

                EditorWindow gameView = null;
                Object[] found = Resources.FindObjectsOfTypeAll(gameViewType);
                if (found != null && found.Length > 0)
                {
                    gameView = found[0] as EditorWindow;
                }

                if (gameView == null)
                {
                    gameView = EditorWindow.GetWindow(gameViewType);
                }

                if (gameView == null)
                {
                    detail = "未能获取 GameView 窗口。";
                    return false;
                }

                gameView.Show();
                gameView.Focus();
                detail = "GameView 已聚焦。";
                return true;
            }
            catch (Exception exception)
            {
                detail = "聚焦 GameView 失败：" + exception.Message;
                return false;
            }
        }

        #endregion
    }
}

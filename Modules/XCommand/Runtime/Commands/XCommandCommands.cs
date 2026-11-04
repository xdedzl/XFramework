using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace XFramework.Command
{
    /// <summary>
    /// Runtime 程序集中的非核心命令入口：内置命令、运行时与自动化能力。
    /// 核心 operation 管理命令由 XCommandCoreCommands 提供。
    /// </summary>
    public sealed class XCommandCommands : XCommandGroup
    {
        private const string Category = "XCommand";
        private const string StateCategory = "State";
        private const string PerfCategory = "Perf";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterCommandGroup()
        {
            XCommandRegistry.RegisterGroup(typeof(XCommandCommands));
        }

        #region 内置命令

        [XCommandEntry("clear", name = "清空 XCommand", order = 0, mode = XCommandMode.Both, displayType = XCommandDisplayType.Hidden, category = Category, description = "清空当前 XCommand 终端的显示内容，不删除 Hub 历史。", usage = "clear", recordExecution = false)]
        private static void ClearConsole()
        {
            XCommandHub.RequestDisplayClear();
        }

        [XCommandEntry("load_history", name = "加载 XCommand 历史", order = 1, mode = XCommandMode.Both, displayType = XCommandDisplayType.Hidden, category = Category, description = "在当前 XCommand 终端重新加载 Hub 历史。", usage = "load_history", recordExecution = false)]
        private static void LoadHistory()
        {
            XCommandHub.RequestDisplayHistory();
        }

        [XCommandEntry("help", name = "查看 XCommand 帮助", order = 2, mode = XCommandMode.Both, displayType = XCommandDisplayType.Hidden, category = Category, description = "查看全部命令分类，或查看指定分类下的全部命令。", usage = "help [--category CATEGORY] [--all]", recordExecution = false)]
        private static string GetHelp(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "category" }, new[] { "all" });
            args.RequireValueCount(0, "help 不接受位置参数。usage: help [--category CATEGORY] [--all]");

            string requestedCategory = args.GetString("category");
            bool includeCommands = args.HasFlag("all");
            if (requestedCategory != null && string.IsNullOrWhiteSpace(requestedCategory))
            {
                throw new ArgumentException("--category 的分类不能为空。");
            }
            if (requestedCategory != null && includeCommands)
            {
                throw new ArgumentException("--category 和 --all 不能同时使用。");
            }

            IReadOnlyList<XCommandDescriptor> commands = XCommandRegistry.Commands;
            string category = FindHelpCategory(commands, requestedCategory);
            var result = new XCommandHelpResult {
                category = category ?? string.Empty,
            };
            AddHelpCategories(result, commands);

            if (requestedCategory != null)
            {
                AddHelpCommands(result, commands, category);
            }
            else if (includeCommands)
            {
                AddHelpCommands(result, commands, null);
            }
            return JsonUtility.ToJson(result);
        }

        private static string FindHelpCategory(IReadOnlyList<XCommandDescriptor> commands, string requestedCategory)
        {
            if (requestedCategory == null)
            {
                return null;
            }

            string normalizedCategory = requestedCategory.Trim();
            for (int i = 0; i < commands.Count; i++)
            {
                if (string.Equals(commands[i].Category, normalizedCategory, StringComparison.OrdinalIgnoreCase))
                {
                    return commands[i].Category;
                }
            }
            throw new ArgumentException($"未找到命令分类：{requestedCategory}");
        }

        private static void AddHelpCategories(XCommandHelpResult result, IReadOnlyList<XCommandDescriptor> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                string category = commands[i].Category;
                bool exists = false;
                for (int j = 0; j < result.categories.Count; j++)
                {
                    if (string.Equals(result.categories[j], category, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    result.categories.Add(category);
                }
            }
        }

        private static void AddHelpCommands(XCommandHelpResult result, IReadOnlyList<XCommandDescriptor> commands, string category)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                XCommandDescriptor command = commands[i];
                if (category != null && !string.Equals(command.Category, category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                result.commands.Add(new XCommandHelpCommand {
                    command = command.Command,
                    name = command.Name,
                    mode = command.Mode.ToString(),
                    usage = command.Usage,
                    description = command.Description,
                });
            }
        }

        [Serializable]
        private sealed class XCommandHelpResult
        {
            public List<string> categories = new List<string>();
            public string category;
            public List<XCommandHelpCommand> commands = new List<XCommandHelpCommand>();
        }

        [Serializable]
        private sealed class XCommandHelpCommand
        {
            public string command;
            public string name;
            public string mode;
            public string usage;
            public string description;
        }

        [XCommandEntry("log", name = "输出 Unity 日志", order = 30, mode = XCommandMode.Both, displayType = XCommandDisplayType.Hidden, category = Category, description = "向 Unity Console 输出一条日志。", usage = "log <content>")]
        private static void UnityLog(string content)
        {
            Debug.Log(content);
        }

        #endregion

        #region 运行控制

        [XCommandEntry("play-stop", name = "结束运行", order = 10, mode = XCommandMode.Runtime, category = Category, description = "Editor 中异步退出 Play Mode 并等待 EnteredEditMode；Development Player 中返回退出请求回执。", usage = "play-stop")]
        private static object StopRunning()
        {
            return XCommandPipeline.StopRunning();
        }

        #endregion

        #region GameObject 查询

        [XCommandEntry("go-list", name = "列出 GameObject", order = 16, mode = XCommandMode.Both, category = Category, description = "列出真实已加载场景中的 GameObject；name/path 使用不区分大小写的包含匹配，支持组件、Tag、Layer 和激活状态筛选。", usage = "go-list [--name TEXT] [--path TEXT] [--component TYPE] [--tag TAG] [--layer NAME|INDEX] [--active any|active|inactive] [--max-results N] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object ListGameObjects(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "name", "path", "component", "tag", "layer", "active", "max-results" }, new[] { "compact" });
            args.RequireValueCount(0, "go-list 不接受位置参数。");
            return XCommandUtility.GameObjects.List(new XCommandGameObjectListQuery {
                name = args.GetString("name"),
                path = args.GetString("path"),
                component = args.GetString("component"),
                tag = args.GetString("tag"),
                layer = args.GetString("layer"),
                active = args.GetString("active", "any"),
                maxResults = args.GetInt("max-results", 100),
                compact = args.HasFlag("compact"),
            });
        }

        [XCommandEntry("go-state", name = "查询 GameObject 状态", order = 20, mode = XCommandMode.Both, category = Category, description = "按实例 ID、名称、层级路径或组件类型查询 GameObject 状态；使用 --brief 时只返回身份、激活状态和 Transform。默认要求唯一匹配。", usage = "go-state [--instance-id ID|--name NAME|--path PATH|--indexed-path PATH|--component TYPE] [--include-inactive] [--all --max-results N] [--brief] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetGameObjectState(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "instance-id", "name", "path", "indexed-path", "component", "max-results" }, new[] { "include-inactive", "all", "brief", "compact" });
            args.RequireValueCount(0, "go-state 不接受位置参数。");
            return XCommandUtility.GameObjects.GetState(new XCommandGameObjectQuery {
                instanceId = args.GetInt("instance-id", 0),
                name = args.GetString("name"),
                path = args.GetString("path"),
                indexedPath = args.GetString("indexed-path"),
                component = args.GetString("component"),
                includeInactive = args.HasFlag("include-inactive"),
                all = args.HasFlag("all"),
                maxResults = args.GetInt("max-results", 100),
                brief = args.HasFlag("brief"),
                compact = args.HasFlag("compact"),
            });
        }

        #endregion

        #region UI 查询与操作

        [XCommandEntry("ui-list", name = "列出可操作 UGUI", order = 20, mode = XCommandMode.Runtime, category = Category, description = "列出射线可达且存在事件处理器的 UGUI 元素，支持选择器、采样和增量快照。", usage = "ui-list [--sample-grid N] [--name NAME] [--path PATH] [--indexed-path PATH] [--text TEXT] [--action ACTION] [--text-limit N] [--max-results N] [--changed-since ID] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object ListUI(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "sample-grid", "name", "path", "indexed-path", "text", "action", "text-limit", "max-results", "changed-since" }, new[] { "compact" });
            args.RequireValueCount(0, "ui-list 不接受位置参数。");
            var query = new XCommandUIQuery {
                selector = CreateSelector(args),
                sampleGrid = args.GetInt("sample-grid", 5),
                textLimit = args.GetInt("text-limit", 512),
                maxResults = args.GetInt("max-results", 0),
                changedSince = args.GetLong("changed-since", 0),
                compact = args.HasFlag("compact"),
            };
            return XCommandUI.List(query);
        }

        [XCommandEntry("ui-act", name = "操作 UGUI", order = 30, mode = XCommandMode.Runtime, category = Category, description = "重新定位唯一且稳定的 UGUI 元素，通过 Input System 注入 click、down、up 或 scroll。返回 operation ID。", usage = "ui-act [--name NAME|--path PATH|--indexed-path PATH|--text TEXT] [--button BUTTON] [--scroll-x X] [--scroll-y Y] [--sample-grid N] [--stable-frames N] [--timeout S] click|down|up|scroll", displayType = XCommandDisplayType.Hidden)]
        private static object ActOnUI(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "name", "path", "indexed-path", "text", "button", "scroll-x", "scroll-y", "sample-grid", "stable-frames", "timeout" }, Array.Empty<string>());
            args.RequireValueCount(1, "usage: ui-act [OPTIONS] click|down|up|scroll");
            var request = new XCommandUIActionRequest {
                pointerAction = args.Values[0],
                selector = CreateSelector(args),
                button = args.GetString("button", "left"),
                scrollX = args.GetFloat("scroll-x", 0f),
                scrollY = args.GetFloat("scroll-y", 0f),
                sampleGrid = args.GetInt("sample-grid", 5),
                stableFrames = args.GetInt("stable-frames", 2),
                timeoutSeconds = args.GetFloat("timeout", 10f),
            };
            return XCommandPipeline.RunOperation(new UIActOperation(request));
        }

        #endregion

        #region 等待

        [XCommandEntry("wait-for-ui", name = "等待 UGUI", order = 40, mode = XCommandMode.Runtime, category = Category, description = "等待 UGUI 元素出现、消失或内容稳定；返回 operation ID。", usage = "wait-for-ui [--state visible|hidden|stable] [--interactable] [--name NAME|--path PATH|--indexed-path PATH|--text TEXT|--action ACTION] [--sample-grid N] [--stable-frames N] [--timeout S] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object WaitForUI(string argument)
        {
            const string usage = "usage: wait-for-ui [--state visible|hidden|stable] [--interactable] [--name NAME|--path PATH|--indexed-path PATH|--text TEXT|--action ACTION] [--sample-grid N] [--stable-frames N] [--timeout S] [--compact]";
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "state", "name", "path", "indexed-path", "text", "action", "sample-grid", "stable-frames", "timeout" }, new[] { "interactable", "compact" });
            args.RequireValueCount(0, usage);
            return XCommandPipeline.RunOperation(new WaitOperation("wait-for-ui", new XCommandUIWaitRequest {
                state = args.GetString("state", "visible"),
                interactable = args.HasFlag("interactable"),
                selector = CreateSelector(args),
                sampleGrid = args.GetInt("sample-grid", 5),
                stableFrames = args.GetInt("stable-frames", 2),
                timeoutSeconds = args.GetFloat("timeout", 30f),
                compact = args.HasFlag("compact"),
            }));
        }

        [XCommandEntry("wait-for-go", name = "等待 GameObject", order = 41, mode = XCommandMode.Runtime, category = Category, description = "等待 GameObject 出现、消失、激活、失活或变换稳定；返回 operation ID。", usage = "wait-for-go [--state exists|missing|active|inactive|stable] [--instance-id ID|--name NAME|--path PATH|--indexed-path PATH|--component TYPE] [--stable-frames N] [--position-epsilon N] [--rotation-epsilon N] [--timeout S] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object WaitForGameObject(string argument)
        {
            const string usage = "usage: wait-for-go [--state exists|missing|active|inactive|stable] [--instance-id ID|--name NAME|--path PATH|--indexed-path PATH|--component TYPE] [--stable-frames N] [--position-epsilon N] [--rotation-epsilon N] [--timeout S] [--compact]";
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "state", "instance-id", "name", "path", "indexed-path", "component", "stable-frames", "position-epsilon", "rotation-epsilon", "timeout" }, new[] { "compact" });
            args.RequireValueCount(0, usage);
            return XCommandPipeline.RunOperation(new WaitOperation("wait-for-go", new XCommandGameObjectWaitRequest {
                state = args.GetString("state", "exists"),
                query = new XCommandGameObjectQuery {
                    instanceId = args.GetInt("instance-id", 0),
                    name = args.GetString("name"),
                    path = args.GetString("path"),
                    indexedPath = args.GetString("indexed-path"),
                    component = args.GetString("component"),
                    includeInactive = true,
                },
                stableFrames = args.GetInt("stable-frames", 2),
                positionEpsilon = args.GetFloat("position-epsilon", 0.001f),
                rotationEpsilon = args.GetFloat("rotation-epsilon", 0.1f),
                timeoutSeconds = args.GetFloat("timeout", 30f),
                compact = args.HasFlag("compact"),
            }));
        }

        [XCommandEntry("wait-for-log", name = "等待日志", order = 42, mode = XCommandMode.Runtime, category = Category, description = "等待指定级别与正文的日志达到条数，默认只等待命令开始后产生的新日志；返回 operation ID。", usage = "wait-for-log [--since CURSOR] [--level all|log|warning|error] [--contains TEXT] [--count N] [--message-limit N] [--stack-limit N] [--stack] [--timeout S] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object WaitForLog(string argument)
        {
            const string usage = "usage: wait-for-log [--since CURSOR] [--level all|log|warning|error] [--contains TEXT] [--count N] [--message-limit N] [--stack-limit N] [--stack] [--timeout S] [--compact]";
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "since", "level", "contains", "count", "message-limit", "stack-limit", "timeout" }, new[] { "stack", "compact" });
            args.RequireValueCount(0, usage);
            return XCommandPipeline.RunOperation(new WaitOperation("wait-for-log", new XCommandLogWaitRequest {
                since = args.GetLong("since", -1),
                level = args.GetString("level", "all"),
                contains = args.GetString("contains"),
                count = args.GetInt("count", 1),
                messageLimit = args.GetInt("message-limit", 2048),
                stackLimit = args.GetInt("stack-limit", 4096),
                includeStack = args.HasFlag("stack"),
                timeoutSeconds = args.GetFloat("timeout", 30f),
                compact = args.HasFlag("compact"),
            }));
        }

        [XCommandEntry("wait", name = "独立等待", order = 45, mode = XCommandMode.Runtime, category = Category, description = "按真实时间、受 timeScale 影响的游戏时间或帧数等待，不依赖 ui-input。返回 operation ID。", usage = "wait (--real-seconds S|--game-seconds S|--frames N) [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object Wait(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "real-seconds", "game-seconds", "frames" }, new[] { "compact" });
            args.RequireValueCount(0, "wait 不接受位置参数。");
            return XCommandPipeline.RunOperation(new WaitOperation("wait", new XCommandWaitRequest {
                realSeconds = args.GetFloat("real-seconds", 0f),
                gameSeconds = args.GetFloat("game-seconds", 0f),
                frames = args.GetInt("frames", 0),
                compact = args.HasFlag("compact"),
            }));
        }

        #endregion

        #region 日志

        [XCommandEntry("logs", name = "查询日志", order = 50, mode = XCommandMode.Both, category = Category, description = "按游标查询 50MB 环形缓冲区中的 Unity 日志，支持级别、正文、条数和堆栈筛选。", usage = "logs [--since CURSOR] [--level all|log|warning|error] [--contains TEXT] [--limit N] [--message-limit N] [--stack-limit N] [--stack] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetLogs(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "since", "level", "contains", "limit", "message-limit", "stack-limit" }, new[] { "stack", "compact" });
            args.RequireValueCount(0, "logs 不接受位置参数。");
            return XCommandUtility.Logs.Query(new XCommandLogQuery {
                since = args.GetLong("since", 0),
                level = args.GetString("level", "all"),
                contains = args.GetString("contains"),
                limit = args.GetInt("limit", 200),
                messageLimit = args.GetInt("message-limit", 2048),
                stackLimit = args.GetInt("stack-limit", 4096),
                includeStack = args.HasFlag("stack"),
                compact = args.HasFlag("compact"),
            });
        }

        #endregion

        #region 输入注入

        [XCommandEntry("input-state", name = "查询自动化输入状态", order = 55, mode = XCommandMode.Both, category = Category, description = "返回自动化键盘、鼠标、触摸保持状态，以及注入设备和当前设备信息。", usage = "input-state [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetInputState(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(0, "input-state 不接受位置参数。");
            return XCommandUtility.Input.GetState(args.HasFlag("compact"));
        }

        [XCommandEntry("input-reset", name = "重置自动化输入", order = 56, mode = XCommandMode.Runtime, category = Category, description = "立即释放自动化键盘、鼠标和触摸的所有保持状态，并返回重置后的输入状态。", usage = "input-reset", displayType = XCommandDisplayType.Hidden)]
        private static object ResetInput()
        {
            XCommandUtility.Input.ResetInterruptedInput();
            return XCommandUtility.Input.GetState(true);
        }

        [XCommandEntry("ui-input", name = "注入输入序列", order = 60, mode = XCommandMode.Runtime, category = Category, description = "通过 Unity Input System 注入键盘、鼠标和多点触控 JSON 序列。返回 operation ID。", usage = "ui-input {\"steps\":[{\"type\":\"mouse|key|touch|wait|reset\",...}],\"resetAfter\":false}", displayType = XCommandDisplayType.Hidden)]
        private static object InjectInput(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                throw new ArgumentException("输入序列 JSON 不能为空。", nameof(argument));
            XCommandInputSequence sequence = JsonUtility.FromJson<XCommandInputSequence>(argument);
            return XCommandPipeline.RunOperation(new UIInputOperation(sequence));
        }

        #endregion

        #region 截图

        [XCommandEntry("screenshot", name = "运行时截图", order = 70, mode = XCommandMode.Runtime, category = Category, description = "在帧末截图，支持输出尺寸、屏幕区域和仅 UI 模式。返回 operation ID。", usage = "screenshot [--width W --height H] [--region X,Y,W,H] [--ui-only] [--timeout S] PATH", displayType = XCommandDisplayType.Hidden)]
        private static object CaptureScreenshot(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "width", "height", "region", "timeout" }, new[] { "ui-only" });
            args.RequireValueCount(1, "usage: screenshot [OPTIONS] PATH");
            var request = new XCommandScreenshotRequest {
                path = args.Values[0],
                width = args.GetInt("width", 0),
                height = args.GetInt("height", 0),
                region = ParseRegion(args.GetString("region")),
                uiOnly = args.HasFlag("ui-only"),
                timeoutSeconds = args.GetFloat("timeout", 10f),
            };
            return XCommandPipeline.RunOperation(new ScreenshotOperation(request));
        }

        #endregion

        #region 性能采样

        [XCommandEntry("perf-start", name = "开始帧采样", order = 200, mode = XCommandMode.Runtime, category = PerfCategory,
            description = "开始逐帧采样帧间隔、GC、FrameTiming 与 Profiler 标记；可指定采样时长、自定义标记与位置追踪目标。",
            usage = XCommandUtility.Performance.StartUsage, displayType = XCommandDisplayType.Hidden)]
        private static object StartPerformanceSampling(string argument)
        {
            return XCommandUtility.Performance.ExecuteStart(argument);
        }

        [XCommandEntry("perf-status", name = "查询帧采样状态", order = 201, mode = XCommandMode.Runtime, category = PerfCategory,
            description = "返回当前采样会话的样本数量、已采样时长与超阈值帧数。",
            usage = XCommandUtility.Performance.StatusUsage, displayType = XCommandDisplayType.Hidden)]
        private static object GetPerformanceSamplingStatus(string argument)
        {
            return XCommandUtility.Performance.ExecuteStatus(argument);
        }

        [XCommandEntry("perf-stop", name = "结束帧采样", order = 202, mode = XCommandMode.Runtime, category = PerfCategory,
            description = "结束采样并返回统计报告（单行 JSON，包含帧耗时分位、直方图、尖峰、GC、FrameTiming、Profiler 标记与最差帧）。",
            usage = XCommandUtility.Performance.StopUsage, displayType = XCommandDisplayType.Hidden)]
        private static object StopPerformanceSampling(string argument)
        {
            return XCommandUtility.Performance.ExecuteStop(argument);
        }

        #endregion

        #region 状态查询

        [XCommandEntry("scene-state", name = "查询场景状态", order = 15, mode = XCommandMode.Both, category = StateCategory, description = "返回当前已加载场景、活动场景、Dirty 状态以及根 GameObject 摘要。", usage = "scene-state [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetSceneState(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(0, "scene-state 不接受位置参数。");
            return XCommandUtility.State.GetScenes(args.HasFlag("compact"));
        }

        [XCommandEntry("app-state", name = "查询应用状态", order = 17, mode = XCommandMode.Both, category = StateCategory, description = "返回应用、时间、屏幕、设备和常用路径状态。", usage = "app-state [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetApplicationState(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(0, "app-state 不接受位置参数。");
            return XCommandUtility.State.GetApplication(args.HasFlag("compact"));
        }

        #endregion

        private static XCommandUISelector CreateSelector(XCommandArguments args)
        {
            return new XCommandUISelector {
                name = args.GetString("name"),
                path = args.GetString("path"),
                indexedPath = args.GetString("indexed-path"),
                text = args.GetString("text"),
                action = args.GetString("action"),
            };
        }

        private static RectInt ParseRegion(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new RectInt();
            string[] parts = value.Split(',');
            if (parts.Length != 4)
                throw new ArgumentException("region 必须是 X,Y,W,H。");
            var region = new RectInt(ParseInt(parts[0], "region x"), ParseInt(parts[1], "region y"), ParseInt(parts[2], "region width"), ParseInt(parts[3], "region height"));
            if (region.width <= 0 || region.height <= 0)
                throw new ArgumentException("region 宽高必须大于 0。");
            return region;
        }

        private static int ParseInt(string value, string name)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new ArgumentException($"{name} 不是有效整数：{value}");
            return result;
        }

    }
}

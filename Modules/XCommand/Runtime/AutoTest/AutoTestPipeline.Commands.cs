using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using XFramework.Command;

namespace XFramework.AutoTest
{
    public sealed class AutoTestPipelineCommands : XCommandGroup
    {
        private const string Category = "AutoTest";

        [XCommandEntry("play-stop", name = "结束运行", order = 10, mode = XCommandMode.Runtime, category = Category, description = "Editor 中退出 Play Mode；Development Player 中请求退出应用。", usage = "play-stop", displayType = XCommandDisplayType.Hidden)]
        private static object StopRunning()
        {
            AutoTestPipeline.StopRunning();
            return "Stop requested.";
        }

        [XCommandEntry("scene-state", name = "查询场景状态", order = 15, mode = XCommandMode.Both, category = Category, description = "返回当前已加载场景、活动场景、Dirty 状态以及根 GameObject 摘要。", usage = "scene-state [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetSceneState(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(0, "scene-state 不接受位置参数。");
            return AutoTestPipeline.GetSceneState(args.HasFlag("compact"));
        }

        [XCommandEntry("go-list", name = "列出 GameObject", order = 16, mode = XCommandMode.Both, category = Category, description = "列出真实已加载场景中的 GameObject；name/path 使用不区分大小写的包含匹配，支持组件、Tag、Layer 和激活状态筛选。", usage = "go-list [--name TEXT] [--path TEXT] [--component TYPE] [--tag TAG] [--layer NAME|INDEX] [--active any|active|inactive] [--max-results N] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object ListGameObjects(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "name", "path", "component", "tag", "layer", "active", "max-results" }, new[] { "compact" });
            args.RequireValueCount(0, "go-list 不接受位置参数。");
            return AutoTestPipeline.ListGameObjects(new AutoTestGameObjectListQuery {
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

        [XCommandEntry("app-state", name = "查询应用状态", order = 17, mode = XCommandMode.Both, category = Category, description = "返回应用、时间、屏幕、设备和常用路径状态。", usage = "app-state [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetApplicationState(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(0, "app-state 不接受位置参数。");
            return AutoTestPipeline.GetApplicationState(args.HasFlag("compact"));
        }

        [XCommandEntry("go-state", name = "查询 GameObject 状态", order = 20, mode = XCommandMode.Both, category = Category, description = "按实例 ID、名称、层级路径或组件类型查询 GameObject 状态；使用 --brief 时只返回身份、激活状态和 Transform。默认要求唯一匹配。", usage = "go-state [--instance-id ID|--name NAME|--path PATH|--indexed-path PATH|--component TYPE] [--include-inactive] [--all --max-results N] [--brief] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetGameObjectState(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "instance-id", "name", "path", "indexed-path", "component", "max-results" }, new[] { "include-inactive", "all", "brief", "compact" });
            args.RequireValueCount(0, "go-state 不接受位置参数。");
            return AutoTestPipeline.GetGameObjectState(new AutoTestGameObjectQuery {
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

        [XCommandEntry("ui-list", name = "列出可操作 UGUI", order = 20, mode = XCommandMode.Runtime, category = Category, description = "列出射线可达且存在事件处理器的 UGUI 元素，支持选择器、采样和增量快照。", usage = "ui-list [--sample-grid N] [--name NAME] [--path PATH] [--indexed-path PATH] [--text TEXT] [--action ACTION] [--text-limit N] [--max-results N] [--changed-since ID] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object ListUI(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "sample-grid", "name", "path", "indexed-path", "text", "action", "text-limit", "max-results", "changed-since" }, new[] { "compact" });
            args.RequireValueCount(0, "ui-list 不接受位置参数。");
            var query = new AutoTestUIQuery {
                selector = CreateSelector(args),
                sampleGrid = args.GetInt("sample-grid", 5),
                textLimit = args.GetInt("text-limit", 512),
                maxResults = args.GetInt("max-results", 0),
                changedSince = args.GetLong("changed-since", 0),
                compact = args.HasFlag("compact"),
            };
            return AutoTestPipeline.ListUI(query);
        }

        [XCommandEntry("ui-act", name = "操作 UGUI", order = 30, mode = XCommandMode.Runtime, category = Category, description = "重新定位唯一且稳定的 UGUI 元素，通过 Input System 注入 click、down、up 或 scroll。返回 operation ID。", usage = "ui-act [--name NAME|--path PATH|--indexed-path PATH|--text TEXT] [--button BUTTON] [--scroll-x X] [--scroll-y Y] [--sample-grid N] [--stable-frames N] [--timeout S] click|down|up|scroll", displayType = XCommandDisplayType.Hidden)]
        private static object ActOnUI(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "name", "path", "indexed-path", "text", "button", "scroll-x", "scroll-y", "sample-grid", "stable-frames", "timeout" }, Array.Empty<string>());
            args.RequireValueCount(1, "usage: ui-act [OPTIONS] click|down|up|scroll");
            var request = new AutoTestUIActionRequest {
                pointerAction = args.Values[0],
                selector = CreateSelector(args),
                button = args.GetString("button", "left"),
                scrollX = args.GetFloat("scroll-x", 0f),
                scrollY = args.GetFloat("scroll-y", 0f),
                sampleGrid = args.GetInt("sample-grid", 5),
                stableFrames = args.GetInt("stable-frames", 2),
                timeoutSeconds = args.GetFloat("timeout", 10f),
            };
            long operationId = AutoTestPipeline.ActOnUI(request);
            AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo operation);
            return AutoTestPipeline.SerializeOperation(operation);
        }

        [XCommandEntry("wait-for", name = "等待状态", order = 40, mode = XCommandMode.Runtime, category = Category, description = "等待 UGUI、GameObject 或日志条件满足。默认只等待命令开始后产生的新日志；所有等待均返回 operation ID。", usage = "wait-for [UI_OPTIONS] ui | wait-for [--state exists|missing|active|inactive|stable] [--instance-id ID|--name NAME|--path PATH|--indexed-path PATH|--component TYPE] [--stable-frames N] [--position-epsilon N] [--rotation-epsilon N] [--timeout S] [--compact] go | wait-for [--since CURSOR] [--level all|log|warning|error] [--contains TEXT] [--count N] [--stack] [--timeout S] [--compact] log", displayType = XCommandDisplayType.Hidden)]
        private static object WaitFor(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "state", "name", "path", "indexed-path", "text", "action", "sample-grid", "stable-frames", "timeout", "instance-id", "component", "position-epsilon", "rotation-epsilon", "since", "level", "contains", "count", "message-limit", "stack-limit" }, new[] { "interactable", "stack", "compact" });
            args.RequireValueCount(1, "usage: wait-for [OPTIONS] ui|go|log");
            string target = args.Values[0].ToLowerInvariant();
            long operationId;
            if (target == "ui")
            {
                operationId = AutoTestPipeline.WaitForUI(new AutoTestUIWaitRequest {
                    state = args.GetString("state", "visible"),
                    interactable = args.HasFlag("interactable"),
                    selector = CreateSelector(args),
                    sampleGrid = args.GetInt("sample-grid", 5),
                    stableFrames = args.GetInt("stable-frames", 2),
                    timeoutSeconds = args.GetFloat("timeout", 30f),
                });
            }
            else if (target == "go")
            {
                operationId = AutoTestPipeline.WaitForGameObject(new AutoTestGameObjectWaitRequest {
                    state = args.GetString("state", "exists"),
                    query = new AutoTestGameObjectQuery {
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
                });
            }
            else if (target == "log")
            {
                operationId = AutoTestPipeline.WaitForLog(new AutoTestLogWaitRequest {
                    since = args.GetLong("since", -1),
                    level = args.GetString("level", "all"),
                    contains = args.GetString("contains"),
                    count = args.GetInt("count", 1),
                    messageLimit = args.GetInt("message-limit", 2048),
                    stackLimit = args.GetInt("stack-limit", 4096),
                    includeStack = args.HasFlag("stack"),
                    timeoutSeconds = args.GetFloat("timeout", 30f),
                    compact = args.HasFlag("compact"),
                });
            }
            else
                throw new ArgumentException($"wait-for 不支持目标：{args.Values[0]}");
            AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo operation);
            return AutoTestPipeline.SerializeOperation(operation);
        }

        [XCommandEntry("wait", name = "独立等待", order = 45, mode = XCommandMode.Runtime, category = Category, description = "按真实时间、受 timeScale 影响的游戏时间或帧数等待，不依赖 ui-input。返回 operation ID。", usage = "wait (--real-seconds S|--game-seconds S|--frames N) [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object Wait(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "real-seconds", "game-seconds", "frames" }, new[] { "compact" });
            args.RequireValueCount(0, "wait 不接受位置参数。");
            long operationId = AutoTestPipeline.Wait(new AutoTestWaitRequest {
                realSeconds = args.GetFloat("real-seconds", 0f),
                gameSeconds = args.GetFloat("game-seconds", 0f),
                frames = args.GetInt("frames", 0),
                compact = args.HasFlag("compact"),
            });
            AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo operation);
            return AutoTestPipeline.SerializeOperation(operation);
        }

        [XCommandEntry("logs", name = "查询日志", order = 50, mode = XCommandMode.Both, category = Category, description = "按游标查询 50MB 环形缓冲区中的 Unity 日志，支持级别、正文、条数和堆栈筛选。", usage = "logs [--since CURSOR] [--level all|log|warning|error] [--contains TEXT] [--limit N] [--message-limit N] [--stack-limit N] [--stack] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetLogs(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "since", "level", "contains", "limit", "message-limit", "stack-limit" }, new[] { "stack", "compact" });
            args.RequireValueCount(0, "logs 不接受位置参数。");
            return AutoTestPipeline.GetLogs(new AutoTestLogQuery {
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

        [XCommandEntry("input-state", name = "查询自动化输入状态", order = 55, mode = XCommandMode.Both, category = Category, description = "返回自动化键盘、鼠标、触摸保持状态，以及注入设备和当前设备信息。", usage = "input-state [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object GetInputState(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(0, "input-state 不接受位置参数。");
            return AutoTestPipeline.GetInputState(args.HasFlag("compact"));
        }

        [XCommandEntry("input-reset", name = "重置自动化输入", order = 56, mode = XCommandMode.Runtime, category = Category, description = "立即释放自动化键盘、鼠标和触摸的所有保持状态，并返回重置后的输入状态。", usage = "input-reset", displayType = XCommandDisplayType.Hidden)]
        private static object ResetInput()
        {
            return AutoTestPipeline.ResetInput(true);
        }

        [XCommandEntry("ui-input", name = "注入输入序列", order = 60, mode = XCommandMode.Runtime, category = Category, description = "通过 Unity Input System 注入键盘、鼠标和多点触控 JSON 序列。返回 operation ID。", usage = "ui-input {\"steps\":[{\"type\":\"mouse|key|touch|wait|reset\",...}],\"resetAfter\":false}", displayType = XCommandDisplayType.Hidden)]
        private static object InjectInput(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                throw new ArgumentException("输入序列 JSON 不能为空。", nameof(argument));
            AutoTestInputSequence sequence = JsonUtility.FromJson<AutoTestInputSequence>(argument);
            long operationId = AutoTestPipeline.RunInput(sequence);
            AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo operation);
            return AutoTestPipeline.SerializeOperation(operation);
        }

        [XCommandEntry("screenshot", name = "运行时截图", order = 70, mode = XCommandMode.Runtime, category = Category, description = "在帧末截图，支持输出尺寸、屏幕区域和仅 UI 模式。返回 operation ID。", usage = "screenshot [--width W --height H] [--region X,Y,W,H] [--ui-only] [--timeout S] PATH", displayType = XCommandDisplayType.Hidden)]
        private static object CaptureScreenshot(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "width", "height", "region", "timeout" }, new[] { "ui-only" });
            args.RequireValueCount(1, "usage: screenshot [OPTIONS] PATH");
            var request = new AutoTestScreenshotRequest {
                path = args.Values[0],
                width = args.GetInt("width", 0),
                height = args.GetInt("height", 0),
                region = ParseRegion(args.GetString("region")),
                uiOnly = args.HasFlag("ui-only"),
                timeoutSeconds = args.GetFloat("timeout", 10f),
            };
            long operationId = AutoTestPipeline.CaptureScreenshot(request);
            AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo operation);
            return AutoTestPipeline.SerializeOperation(operation);
        }

        [XCommandEntry("operation-list", name = "列出自动化操作", order = 80, mode = XCommandMode.Both, category = Category, description = "列出最近的 AutoTest 异步操作及其状态。", usage = "operation-list [--limit N] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object ListOperations(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, new[] { "limit" }, new[] { "compact" });
            args.RequireValueCount(0, "operation-list 不接受位置参数。");
            return AutoTestPipeline.SerializeOperations(args.GetInt("limit", 20), !args.HasFlag("compact"));
        }

        [XCommandEntry("operation-status", name = "查询自动化操作", order = 90, mode = XCommandMode.Both, category = Category, description = "查询指定 AutoTest 操作的状态、输出和错误。", usage = "operation-status [--compact] ID", displayType = XCommandDisplayType.Hidden)]
        private static object GetOperationStatus(string argument)
        {
            AutoTestCommandArguments args = AutoTestCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(1, "usage: operation-status [--compact] ID");
            long operationId = ParseLong(args.Values[0], "operation ID");
            if (!AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo operation))
                throw new ArgumentException($"找不到 AutoTest 操作：{operationId}");
            return AutoTestPipeline.SerializeOperation(operation, !args.HasFlag("compact"));
        }

        [XCommandEntry("operation-cancel", name = "取消自动化操作", order = 100, mode = XCommandMode.Runtime, category = Category, description = "取消指定 AutoTest 操作，或使用 all 取消全部等待和排队操作。", usage = "operation-cancel ID|all", displayType = XCommandDisplayType.Hidden)]
        private static object CancelOperation(string argument)
        {
            string value = argument?.Trim() ?? string.Empty;
            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
                return $"Cancelled operations: {AutoTestPipeline.CancelAllOperations()}";
            long operationId = ParseLong(value, "operation ID");
            if (!AutoTestPipeline.CancelOperation(operationId))
                throw new InvalidOperationException($"操作 {operationId} 不存在或已经结束。");
            AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo operation);
            return AutoTestPipeline.SerializeOperation(operation);
        }

        private static AutoTestUISelector CreateSelector(AutoTestCommandArguments args)
        {
            return new AutoTestUISelector {
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

        private static long ParseLong(string value, string name)
        {
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
                throw new ArgumentException($"{name} 不是有效整数：{value}");
            return result;
        }
    }

    internal sealed class AutoTestCommandArguments
    {
        private readonly Dictionary<string, string> m_Options = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> m_Flags = new HashSet<string>(StringComparer.Ordinal);

        public List<string> Values { get; } = new List<string>();

        public static AutoTestCommandArguments Parse(string commandLine, string[] valueOptions, string[] flagOptions)
        {
            var result = new AutoTestCommandArguments();
            var valueOptionSet = new HashSet<string>(valueOptions, StringComparer.Ordinal);
            var flagOptionSet = new HashSet<string>(flagOptions, StringComparer.Ordinal);
            List<string> tokens = Tokenize(commandLine ?? string.Empty);
            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (!token.StartsWith("--", StringComparison.Ordinal))
                {
                    result.Values.Add(token);
                    continue;
                }
                string option = token.Substring(2);
                int equalsIndex = option.IndexOf('=');
                string name = equalsIndex >= 0 ? option.Substring(0, equalsIndex) : option;
                if (flagOptionSet.Contains(name))
                {
                    if (equalsIndex >= 0)
                        throw new ArgumentException($"标记 --{name} 不接受值。");
                    result.m_Flags.Add(name);
                    continue;
                }
                if (!valueOptionSet.Contains(name))
                    throw new ArgumentException($"未知选项：--{name}");
                string value;
                if (equalsIndex >= 0)
                    value = option.Substring(equalsIndex + 1);
                else if (++i < tokens.Count && !tokens[i].StartsWith("--", StringComparison.Ordinal))
                    value = tokens[i];
                else
                    throw new ArgumentException($"选项 --{name} 缺少值。");
                result.m_Options[name] = value;
            }
            return result;
        }

        public bool HasFlag(string name)
        {
            return m_Flags.Contains(name);
        }

        public string GetString(string name, string defaultValue = null)
        {
            return m_Options.TryGetValue(name, out string value) ? value : defaultValue;
        }

        public int GetInt(string name, int defaultValue)
        {
            string value = GetString(name);
            if (value == null)
                return defaultValue;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                throw new ArgumentException($"--{name} 不是有效整数：{value}");
            return result;
        }

        public long GetLong(string name, long defaultValue)
        {
            string value = GetString(name);
            if (value == null)
                return defaultValue;
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
                throw new ArgumentException($"--{name} 不是有效整数：{value}");
            return result;
        }

        public float GetFloat(string name, float defaultValue)
        {
            string value = GetString(name);
            if (value == null)
                return defaultValue;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                throw new ArgumentException($"--{name} 不是有效数字：{value}");
            return result;
        }

        public void RequireValueCount(int count, string error)
        {
            if (Values.Count != count)
                throw new ArgumentException(error);
        }

        private static List<string> Tokenize(string value)
        {
            var tokens = new List<string>();
            int index = 0;
            while (index < value.Length)
            {
                while (index < value.Length && char.IsWhiteSpace(value[index]))
                    index++;
                if (index >= value.Length)
                    break;
                var builder = new StringBuilder();
                char quote = '\0';
                while (index < value.Length)
                {
                    char character = value[index];
                    if (quote == '\0' && char.IsWhiteSpace(character))
                        break;
                    if (character == '\'' || character == '"')
                    {
                        if (quote == '\0')
                        {
                            quote = character;
                            index++;
                            continue;
                        }
                        if (quote == character)
                        {
                            quote = '\0';
                            index++;
                            continue;
                        }
                    }
                    if (character == '\\' && index + 1 < value.Length && quote != '\0' && (value[index + 1] == quote || value[index + 1] == '\\'))
                    {
                        builder.Append(value[index + 1]);
                        index += 2;
                        continue;
                    }
                    builder.Append(character);
                    index++;
                }
                if (quote != '\0')
                    throw new ArgumentException("命令参数包含未闭合的引号。");
                tokens.Add(builder.ToString());
            }
            return tokens;
        }
    }
}

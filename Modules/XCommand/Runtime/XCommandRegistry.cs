using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XFramework.Command
{
    public enum XCommandMode
    {
        Runtime,
        Editor,
        Both
    }

    public enum XCommandDisplayType
    {
        Visible,
        Hidden
    }

    public enum XCommandExecutionStatus
    {
        Succeeded,
        NotFound,
        Unavailable,
        Failed
    }

    public sealed class XCommandDescriptor
    {
        private readonly Func<string, object> m_Handler;

        internal XCommandDescriptor(string command, string name, int order, XCommandMode mode, XCommandDisplayType displayType, string category, string description, string usage, bool requireConfirmation, bool recordExecution, bool hasArgument, string declaringTypeName, string methodName, string assemblyName, Func<string, object> handler)
        {
            Command = command;
            Name = name;
            Order = order;
            Mode = mode;
            DisplayType = displayType;
            Category = category;
            Description = description;
            Usage = usage;
            RequireConfirmation = requireConfirmation;
            RecordExecution = recordExecution;
            HasArgument = hasArgument;
            DeclaringTypeName = declaringTypeName;
            MethodName = methodName;
            AssemblyName = assemblyName;
            m_Handler = handler;
        }

        public string Command { get; }
        public string Name { get; }
        public int Order { get; }
        public XCommandMode Mode { get; }
        public XCommandDisplayType DisplayType { get; }
        public string Category { get; }
        public string Description { get; }
        public string Usage { get; }
        public bool RequireConfirmation { get; }
        public bool RecordExecution { get; }
        public bool HasArgument { get; }
        public string DeclaringTypeName { get; }
        public string MethodName { get; }
        public string AssemblyName { get; }

        internal object Invoke(string argument)
        {
            return m_Handler.Invoke(argument);
        }
    }

    public sealed class XCommandExecutionResult
    {
        internal XCommandExecutionResult(XCommandExecutionStatus status, string commandLine, XCommandDescriptor command, object value, Exception exception, double durationMilliseconds, string message)
        {
            Status = status;
            CommandLine = commandLine;
            Command = command;
            Value = value;
            Exception = exception;
            DurationMilliseconds = durationMilliseconds;
            Message = message;
        }

        public XCommandExecutionStatus Status { get; }
        public string CommandLine { get; }
        public XCommandDescriptor Command { get; }
        public object Value { get; }
        public Exception Exception { get; }
        public double DurationMilliseconds { get; }
        public string Message { get; }
        public bool Succeeded => Status == XCommandExecutionStatus.Succeeded;
    }

    public sealed class XCommandDiagnostic
    {
        internal XCommandDiagnostic(string command, string message, string declaringTypeName, string methodName)
        {
            Command = command;
            Message = message;
            DeclaringTypeName = declaringTypeName;
            MethodName = methodName;
        }

        public string Command { get; }
        public string Message { get; }
        public string DeclaringTypeName { get; }
        public string MethodName { get; }
    }

    public abstract class XCommandGroup
    {
    }

    public static class XCommandRegistry
    {
        private static readonly Dictionary<string, XCommandDescriptor> s_AttributedCommands = new Dictionary<string, XCommandDescriptor>(StringComparer.Ordinal);
        private static readonly Dictionary<string, XCommandDescriptor> s_DynamicCommands = new Dictionary<string, XCommandDescriptor>(StringComparer.Ordinal);
        private static readonly Dictionary<string, XCommandDescriptor> s_Commands = new Dictionary<string, XCommandDescriptor>(StringComparer.Ordinal);
        private static readonly List<XCommandDescriptor> s_CommandList = new List<XCommandDescriptor>();
        private static readonly List<XCommandDescriptor> s_VisibleCommandList = new List<XCommandDescriptor>();
        private static readonly List<XCommandDiagnostic> s_Diagnostics = new List<XCommandDiagnostic>();

        static XCommandRegistry()
        {
            Refresh();
        }

        public static IReadOnlyList<XCommandDescriptor> Commands => s_CommandList;
        public static IReadOnlyList<XCommandDescriptor> VisibleCommands => s_VisibleCommandList;
        public static IReadOnlyList<XCommandDiagnostic> Diagnostics => s_Diagnostics;

        public static void Refresh()
        {
            s_AttributedCommands.Clear();
            s_Diagnostics.Clear();

            foreach (var type in GetCommandTypes())
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<XCommandEntryAttribute>();
                    if (attr == null)
                    {
                        continue;
                    }

                    string command = attr.cmd ?? method.Name;
                    var parameters = method.GetParameters();
                    if (!method.IsStatic || method.ContainsGenericParameters || parameters.Length > 1 || parameters.Length == 1 && parameters[0].ParameterType != typeof(string))
                    {
                        AddDiagnostic(command, $"XCommand 命令必须是非泛型静态方法，并且只允许无参数或单个 string 参数：{type.FullName}.{method.Name}", type.FullName, method.Name);
                        continue;
                    }

                    bool hasArgument = parameters.Length == 1;
                    Func<string, object> handler;
                    if (hasArgument)
                    {
                        handler = argument => method.Invoke(null, new object[] { argument });
                    }
                    else
                    {
                        handler = _ => method.Invoke(null, null);
                    }
                    string name = string.IsNullOrEmpty(attr.name) ? command : attr.name;
                    string category = string.IsNullOrEmpty(attr.category) ? type.Name : attr.category;
                    string usage = string.IsNullOrEmpty(attr.usage) ? command + (hasArgument ? " <args>" : string.Empty) : attr.usage;
                    var descriptor = new XCommandDescriptor(command, name, attr.order, attr.mode, attr.displayType, category, attr.description ?? string.Empty, usage, attr.requireConfirmation, attr.recordExecution, hasArgument, type.FullName, method.Name, type.Assembly.GetName().Name, handler);
                    RegisterAttributedCommand(descriptor);
                }
            }

            RebuildCommandRegistry();
        }

        public static bool CanExecute(XCommandDescriptor command)
        {
            if (command == null)
            {
                return false;
            }

            switch (command.Mode)
            {
                case XCommandMode.Runtime:
                    return Application.isPlaying;
                case XCommandMode.Editor:
                    return !Application.isPlaying && Application.isEditor;
                default:
                    return true;
            }
        }

        public static bool TryGetCommand(string commandLine, out XCommandDescriptor command)
        {
            command = null;
            if (!TryParseCommandLine(commandLine, out string commandName, out _))
            {
                return false;
            }

            return s_Commands.TryGetValue(commandName, out command);
        }

        public static XCommandExecutionResult ExecuteDetailed(string commandLine)
        {
            return XCommandHub.Execute(commandLine);
        }

        internal static XCommandExecutionResult ExecuteCore(string commandLine)
        {
            string normalizedCommandLine = commandLine?.Trim() ?? string.Empty;
            if (!TryParseCommandLine(normalizedCommandLine, out string commandName, out string commandArgument))
            {
                return new XCommandExecutionResult(XCommandExecutionStatus.NotFound, normalizedCommandLine, null, null, null, 0d, "命令不能为空。");
            }

            if (!s_Commands.TryGetValue(commandName, out XCommandDescriptor command))
            {
                return new XCommandExecutionResult(XCommandExecutionStatus.NotFound, normalizedCommandLine, null, null, null, 0d, $"未找到命令：{commandName}");
            }

            if (!CanExecute(command))
            {
                string environment = Application.isPlaying ? "Runtime" : "Editor";
                return new XCommandExecutionResult(XCommandExecutionStatus.Unavailable, normalizedCommandLine, command, null, null, 0d, $"命令 {command.Command} 不能在 {environment} 环境执行。");
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                object value = command.Invoke(commandArgument);
                stopwatch.Stop();
                return new XCommandExecutionResult(XCommandExecutionStatus.Succeeded, normalizedCommandLine, command, value, null, stopwatch.Elapsed.TotalMilliseconds, "执行成功。");
            }
            catch (TargetInvocationException exception)
            {
                stopwatch.Stop();
                Exception commandException = exception.InnerException ?? exception;
                return new XCommandExecutionResult(XCommandExecutionStatus.Failed, normalizedCommandLine, command, null, commandException, stopwatch.Elapsed.TotalMilliseconds, commandException.Message);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                return new XCommandExecutionResult(XCommandExecutionStatus.Failed, normalizedCommandLine, command, null, exception, stopwatch.Elapsed.TotalMilliseconds, exception.Message);
            }
        }

        public static bool Execute(string cmd, out object result)
        {
            XCommandExecutionResult executionResult = ExecuteDetailed(cmd);
            result = executionResult.Value;
            if (executionResult.Status == XCommandExecutionStatus.Failed)
            {
                Debug.LogException(executionResult.Exception);
            }
            return executionResult.Succeeded;
        }

        public static void AddCommand(string cmd, Func<string, object> fun, XCommandMode mode = XCommandMode.Runtime, XCommandDisplayType displayType = XCommandDisplayType.Visible)
        {
            if (s_Commands.ContainsKey(cmd))
            {
                string message = $"XCommand 命令重复：{cmd}";
                AddDiagnostic(cmd, message, fun.Method.DeclaringType?.FullName ?? "<Dynamic>", fun.Method.Name);
                return;
            }

            Type declaringType = fun.Method.DeclaringType;
            string declaringTypeName = declaringType?.FullName ?? "<Dynamic>";
            string assemblyName = declaringType?.Assembly.GetName().Name ?? "<Dynamic>";
            var descriptor = new XCommandDescriptor(cmd, cmd, -1, mode, displayType, "Dynamic", string.Empty, $"{cmd} <args>", false, true, true, declaringTypeName, fun.Method.Name, assemblyName, fun);
            s_DynamicCommands.Add(cmd, descriptor);
            RebuildCommandRegistry();
        }

        private static IEnumerable<Type> GetCommandTypes()
        {
            var typeBase = typeof(XCommandGroup);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeBase) && !type.IsAbstract)
                    {
                        yield return type;
                    }
                }
            }
        }

        private static void RegisterAttributedCommand(XCommandDescriptor descriptor)
        {
            if (s_AttributedCommands.TryGetValue(descriptor.Command, out XCommandDescriptor existing))
            {
                AddDiagnostic(descriptor.Command, $"XCommand 命令重复，保留 {existing.DeclaringTypeName}.{existing.MethodName}，忽略 {descriptor.DeclaringTypeName}.{descriptor.MethodName}。", descriptor.DeclaringTypeName, descriptor.MethodName);
                return;
            }

            s_AttributedCommands.Add(descriptor.Command, descriptor);
        }

        private static void RebuildCommandRegistry()
        {
            s_Commands.Clear();
            foreach (var pair in s_AttributedCommands)
            {
                s_Commands.Add(pair.Key, pair.Value);
            }

            foreach (var pair in s_DynamicCommands)
            {
                if (s_Commands.TryGetValue(pair.Key, out XCommandDescriptor existing))
                {
                    AddDiagnostic(pair.Key, $"动态 XCommand 命令与 Attribute 命令重复，保留 {existing.DeclaringTypeName}.{existing.MethodName}。", pair.Value.DeclaringTypeName, pair.Value.MethodName);
                    continue;
                }
                s_Commands.Add(pair.Key, pair.Value);
            }

            s_CommandList.Clear();
            s_CommandList.AddRange(s_Commands.Values);
            s_CommandList.Sort(CompareCommands);

            s_VisibleCommandList.Clear();
            for (int i = 0; i < s_CommandList.Count; i++)
            {
                if (s_CommandList[i].DisplayType == XCommandDisplayType.Visible)
                {
                    s_VisibleCommandList.Add(s_CommandList[i]);
                }
            }
        }

        private static int CompareCommands(XCommandDescriptor left, XCommandDescriptor right)
        {
            int result = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            int leftOrder = left.Order < 0 ? int.MaxValue : left.Order;
            int rightOrder = right.Order < 0 ? int.MaxValue : right.Order;
            result = leftOrder.CompareTo(rightOrder);
            return result != 0 ? result : string.Compare(left.Command, right.Command, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseCommandLine(string commandLine, out string commandName, out string commandArgument)
        {
            commandName = string.Empty;
            commandArgument = string.Empty;
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return false;
            }

            string normalized = commandLine.Trim();
            int separatorIndex = normalized.IndexOf(' ');
            if (separatorIndex < 0)
            {
                commandName = normalized;
                return true;
            }

            commandName = normalized.Substring(0, separatorIndex);
            commandArgument = normalized.Substring(separatorIndex + 1).Trim();
            return true;
        }

        private static void AddDiagnostic(string command, string message, string declaringTypeName, string methodName)
        {
            s_Diagnostics.Add(new XCommandDiagnostic(command, message, declaringTypeName, methodName));
            Debug.LogWarning(message);
        }
    }

    public sealed class XCommandBuiltInCommands : XCommandGroup
    {
        [XCommandEntry("clear", name = "清空 XCommand", order = 0, mode = XCommandMode.Both, category = "XCommand", description = "清空当前 XCommand 终端的显示内容，不删除 Hub 历史。", usage = "clear", recordExecution = false)]
        public static void ClearConsole()
        {
            XCommandHub.RequestDisplayClear();
        }

        [XCommandEntry("load_history", name = "加载 XCommand 历史", order = 1, mode = XCommandMode.Both, category = "XCommand", description = "在当前 XCommand 终端重新加载 Hub 历史。", usage = "load_history", recordExecution = false)]
        public static void LoadHistory()
        {
            XCommandHub.RequestDisplayHistory();
        }

        [XCommandEntry("enable_hunter", name = "连接 Hunter", order = 10, mode = XCommandMode.Runtime, category = "XCommand", description = "连接远程 Hunter 调试端。", usage = "enable_hunter")]
        public static void StartHunter()
        {
            XCommand.Log("已成功连接 Hunter");
            XCommand.ConnetHunter();
        }

        [XCommandEntry("disable_hunter", name = "断开 Hunter", order = 20, mode = XCommandMode.Runtime, category = "XCommand", description = "断开远程 Hunter 调试端。", usage = "disable_hunter")]
        public static void StopHunter()
        {
            XCommand.Log("已成功断开 Hunter");
            XCommand.DisConnetHunter();
        }

        [XCommandEntry("log", name = "输出 Unity 日志", order = 30, mode = XCommandMode.Both, category = "XCommand", description = "向 Unity Console 输出一条日志。", usage = "log <content>")]
        public static void UnityLog(string content)
        {
            Debug.Log(content);
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class XCommandEntryAttribute : Attribute
    {
        public readonly string cmd;
        public string name;
        public int order;
        public XCommandMode mode;
        public string category;
        public string description;
        public string usage;
        public bool requireConfirmation;
        public bool recordExecution = true;
        public XCommandDisplayType displayType;

        public XCommandEntryAttribute(string cmd = null, string name = null, int order = -1, XCommandMode mode = XCommandMode.Runtime, string category = null, string description = null, string usage = null, bool requireConfirmation = false, XCommandDisplayType displayType = XCommandDisplayType.Visible)
        {
            this.cmd = cmd;
            this.name = name;
            this.order = order;
            this.mode = mode;
            this.category = category;
            this.description = description;
            this.usage = usage;
            this.requireConfirmation = requireConfirmation;
            this.displayType = displayType;
        }
    }

}

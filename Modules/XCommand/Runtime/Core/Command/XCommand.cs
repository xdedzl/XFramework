using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Linq;

namespace XFramework.Command
{
    public delegate bool CommandDelegate(string cmd, out object result);
    
    public static partial class XCommand
    {
        private static Action<Message> LogMessageReceived;

        private static IConsole console = new UGUIConsole();
        
        private static readonly Dictionary<string, CommandDelegate> m_ExecuteFunctions = new ();
        private static readonly List<CommandDelegate> m_AutoFunctions = new ();
        private static readonly CommandDelegate s_CommandRegistryDelegate = ExecuteRegisteredCommand;
        private static string m_CurrentExecuteKey = "";
            
        private static bool m_isOpen;
        private static bool m_isInit;
        private static CancellationTokenRegistration s_ExitRegistration;
        private static XCommandSource s_CurrentCommandSource = XCommandSource.Api;
        private static int s_CommandHistoryIndex = -1;

        private static readonly XFrameworkCliServer server = new XFrameworkCliServer(new XCommandCliOperationProvider());
        private static readonly XCommandRegistry registry = new XCommandRegistry();
        private static readonly XCommandHub hub = new XCommandHub();
        private static readonly XCommandInput input = new XCommandInput();
        private static readonly XCommandUI ui = new XCommandUI();
        private static readonly XCommandPipeline pipeline = new XCommandPipeline();
        private static readonly LogStore logStore = new LogStore();
        private static bool isEnabled = true;
        private static bool subsystemsStarted;

        /// <summary>XCommand CLI Server 的唯一实例。</summary>
        public static XFrameworkCliServer Server => server;
        /// <summary>XCommand 命令发现与注册的唯一实例。</summary>
        public static XCommandRegistry Registry => registry;
        /// <summary>XCommand 命令执行、历史记录与显示事件的唯一实例。</summary>
        public static XCommandHub Hub => hub;
        /// <summary>XCommand 自动化输入状态与注入的唯一实例。</summary>
        internal static XCommandInput Input => input;
        /// <summary>XCommand UGUI 查询与快照管理的唯一实例。</summary>
        public static XCommandUI UI => ui;
        /// <summary>XCommand 异步 operation 队列的唯一实例。</summary>
        public static XCommandPipeline Pipeline => pipeline;
        /// <summary>XCommand 运行时日志采集与查询的唯一实例。</summary>
        public static LogStore Logs => logStore;
        /// <summary>XCommand 当前是否处于启用状态。</summary>
        public static bool IsEnabled => isEnabled;

        /// <summary>XCommand 启用状态变更时触发。</summary>
        public static event Action<bool> EnabledChanged;

        public static bool IsOpen
        {
            get
            {
                return m_isOpen;
            }
            set
            {
                if (m_isOpen != value)
                {
                    m_isOpen = value;

                    if (m_isOpen)
                    {
                        if (!m_isInit)
                        {
                            console.OnInit();
                            OnInit();
                            m_isInit = true;
                        }
                        console.OnOpen();
                    }
                    else
                    {
                        console.OnClose();
                    }
                }
            }
        }
        
        public static string CurrentCommandKey => m_CurrentExecuteKey;
        
        public static IReadOnlyList<string> CommandKeys => m_ExecuteFunctions.Keys.ToList();
        
        static XCommand()
        {
            AddCommand("Auto", ExecuteAutoCommand);
            AddCommand("Command", s_CommandRegistryDelegate, true);
            hub.CommandHistoryChanged += ResetCommandHistoryNavigation;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitializeDefaultEnabled()
        {
            if (isEnabled)
            {
                EnsureSubsystems();
            }
        }

        private static void EnsureSubsystems()
        {
            if (subsystemsStarted)
            {
                return;
            }

            subsystemsStarted = true;
            registry.Start();
            pipeline.Start();
            input.Start();
            ui.Start();
            logStore.Start();
        }

        /// <summary>启用 XCommand：启动命令子系统与 CLI 监听。</summary>
        public static void Enable(XFrameworkCliProcessType processType, string projectPath)
        {
            bool changed = !isEnabled;
            EnsureSubsystems();
            server.Start(processType, projectPath);
            isEnabled = true;
            if (changed)
            {
                EnabledChanged?.Invoke(true);
            }
        }

        /// <summary>停用 XCommand：停止 CLI 监听并撤销命令子系统订阅。</summary>
        public static void Disable()
        {
            if (!isEnabled)
            {
                return;
            }

            isEnabled = false;
            registry.Stop();
            input.Stop();
            ui.Stop();
            logStore.Stop();
            pipeline.Stop();
            server.Stop();
            subsystemsStarted = false;
            EnabledChanged?.Invoke(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPipelineOperations()
        {
            pipeline.ResetOperations();
        }

        /// <summary>在主线程推进 CLI 请求和异步操作完成状态。</summary>
        public static void Pump()
        {
            if (isEnabled)
            {
                server.Pump();
            }
        }

#if UNITY_EDITOR // 编辑器下应对关闭 Reload Domain
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeSwitchListener()
        {
            s_ExitRegistration.Dispose();
            ResetStaticState();
            if (isEnabled)
            {
                logStore.Reset();
                s_ExitRegistration = Application.exitCancellationToken.Register(ResetStaticState);
            }
        }

        private static void ResetStaticState()
        {
            (console as UGUIConsole)?.Dispose();
            LogMessageReceived = null;
            m_CurrentExecuteKey = "Auto";
            m_isOpen = false;
            m_isInit = false;
            s_CurrentCommandSource = XCommandSource.Api;
            s_CommandHistoryIndex = -1;
            console = new UGUIConsole();

            Message.defaultColor = Color.white;
            Message.warningColor = Color.yellow;
            Message.errorColor = Color.red;
            Message.systemColor = Color.green;
            Message.inputColor = Color.green;
            Message.outputColor = Color.cyan;
            Message.unityColor = new Color(0.3882f, 0.7725f, 1f, 1f);
        }
#endif

        private static void OnInit()
        {
            XCommand.LogMessage(Message.System("start XCommand"));
        }

        public static void LogMessage(Message message)
        {
            console.OnLogMessage(message);
            LogMessageReceived?.Invoke(message);
        }

        public static object Log(object message)
        {
            LogMessage(Message.Log(message, ""));
            return message;
        }

        public static object Log(object message, Color col)
        {
            LogMessage(Message.Log(message, "", col));
            return message;
        }

        public static object Log(object message, MessageType messageType)
        {
            LogMessage(Message.Log(message, messageType));
            return message;
        }

        public static object LogWarning(object message)
        {
            LogMessage(Message.Warning(message, ""));
            return message;
        }

        public static object LogError(object message)
        {
            LogMessage(Message.Error(message, ""));
            return message;
        }
        
        public static bool AddCommand(string executeKey, CommandDelegate func, bool registerAuto = false)
        {
            if (string.IsNullOrEmpty(executeKey))
            {
                Debug.LogError("Execute key is null or empty.");
                return false;
            }
            if (m_ExecuteFunctions.ContainsKey(executeKey))
            {
                Debug.LogError($"Execute function {executeKey} already exists.");
                return false;
            }
            
            m_ExecuteFunctions.Add(executeKey, func);
            if (string.IsNullOrEmpty(m_CurrentExecuteKey))
            {
                m_CurrentExecuteKey = executeKey;
            }
            
            if (registerAuto)
            {
                m_AutoFunctions.Add(func);
            }

            OnCommandChange();
            return true;
        }

        public static bool ChangeCommand(string executeKey)
        {
            if (m_ExecuteFunctions.ContainsKey(executeKey))
            {
                m_CurrentExecuteKey = executeKey;
                XCommand.LogMessage(Message.System("change execute commander to " + executeKey));

                OnCommandChange();
                return true;
            }
            else
            {
                Debug.LogError($"Execute function {executeKey} not found.");
                return false;
            }
        }

        private static void OnCommandChange()
        {
            console.OnCommandChange();
        }

        public static bool Execute(string cmd)
        {
            return Execute(cmd, out var result, XCommandSource.Api);
        }

        public static bool Execute(string cmd, out object result)
        {
            return Execute(cmd, out result, XCommandSource.Api);
        }

        internal static bool Execute(string cmd, out object result, XCommandSource source)
        {
            result = null;
            m_ExecuteFunctions.TryGetValue(m_CurrentExecuteKey, out var executeFun);
            if (executeFun == null)
            {
                LogError($"Execute function {m_CurrentExecuteKey} not found.");
                return false;
            }

            XCommandSource previousSource = s_CurrentCommandSource;
            s_CurrentCommandSource = source;
            try
            {
                bool succeeded;
                if (m_CurrentExecuteKey == "Command" || m_CurrentExecuteKey == "Auto")
                {
                    succeeded = executeFun(cmd, out result);
                }
                else
                {
                    succeeded = hub.ExecuteHandler(cmd, source, executeFun, out result);
                }
                console.OnExecuteCmd(cmd, result);
                s_CommandHistoryIndex = -1;
                return succeeded;
            }
            finally
            {
                s_CurrentCommandSource = previousSource;
            }
        }

        private static bool ExecuteRegisteredCommand(string cmd, out object result)
        {
            XCommandExecutionResult executionResult = hub.Execute(cmd, s_CurrentCommandSource);
            result = executionResult.Value;
            if (executionResult.Status == XCommandExecutionStatus.Failed)
            {
                Debug.LogException(executionResult.Exception);
            }
            return executionResult.Succeeded;
        }

        private static bool ExecuteAutoCommand(string cmd, out object result)
        {
            if (registry.TryGetCommand(cmd, out _))
            {
                return ExecuteRegisteredCommand(cmd, out result);
            }

            return hub.ExecuteHandler(cmd, s_CurrentCommandSource, ExecuteRegisteredAutoCommand, out result);
        }

        private static bool ExecuteRegisteredAutoCommand(string cmd, out object result)
        {
            foreach (var func in m_AutoFunctions)
            {
                if (func == s_CommandRegistryDelegate)
                {
                    continue;
                }
                if (func.Invoke(cmd, out result))
                {
                    return true;
                }
            }

            result = null;
            return false;
        }
        
        public static void JumpToPreviousCmd()
        {
            IReadOnlyList<string> commandHistory = hub.CommandHistory;
            if (commandHistory.Count == 0)
            {
                return;
            }

            if (s_CommandHistoryIndex < commandHistory.Count - 1)
            {
                s_CommandHistoryIndex++;
            }
            console.OnCurrentCmdChanged(commandHistory[s_CommandHistoryIndex]);
        }

        public static void JumpToNextCmd()
        {
            if (s_CommandHistoryIndex > 0)
            {
                s_CommandHistoryIndex--;
                console.OnCurrentCmdChanged(hub.CommandHistory[s_CommandHistoryIndex]);
                return;
            }

            s_CommandHistoryIndex = -1;
            console.OnCurrentCmdChanged(string.Empty);
        }

        private static void ResetCommandHistoryNavigation()
        {
            s_CommandHistoryIndex = -1;
        }

        public static void Clear()
        {
            console.OnClear();
        }
    }

    public interface IConsole
    {
        void OnInit();

        void OnOpen();

        void OnClose();

        void OnLogMessage(Message message);

        void OnExecuteCmd(string cmd, object value);

        void OnCommandChange();
        
        void OnClear();
        
        void OnCurrentCmdChanged(string cmd);
    }

    public enum MessageType : int
    {
        NORMAL = 0,
        WARNING = 1,
        ERROR = 2,
        SYSTEM = 3,
        INPUT = 4,
        OUTPUT = 5,
        UNITY = 6,
    }

    public struct Message
    {
        public string text;
        string formatted;
        public string customType;
        public MessageType type;

        public Color color { get; private set; }

        public static Color defaultColor = Color.white;
        public static Color warningColor = Color.yellow;
        public static Color errorColor = Color.red;
        public static Color systemColor = Color.green;
        public static Color inputColor = Color.green;
        public static Color outputColor = Color.cyan;
        public static Color unityColor = new Color(0.3882f, 0.7725f, 1f, 1f);

        public Message(object messageObject, MessageType messageType, Color displayColor, string customType) : this()
        {
            this.Set(messageObject, messageType, displayColor, customType);
        }

        public void Set(object messageObject, MessageType messageType, Color displayColor, string customType)
        {
            this.color = displayColor;

            if (messageObject == null)
                this.text = "<null>";
            else
            {
                if (messageType == MessageType.SYSTEM || messageType == MessageType.OUTPUT || messageType == MessageType.INPUT || messageType == MessageType.UNITY)
                    this.text = messageObject.ToString();
                else
                    this.text = "[" + DateTime.Now.ToLongTimeString() + "] " + messageObject.ToString();
            }

            this.formatted = string.Empty;
            this.type = messageType;
            this.customType = customType;
        }

        public static Message Log(object message, string customType)
        {
            return new Message(message, MessageType.NORMAL, defaultColor, customType);
        }

        public static Message Log(object message, string customType, Color col)
        {
            return new Message(message, MessageType.NORMAL, col, customType);
        }

        public static Message Log(object message, MessageType messageType)
        {
            return new Message(message, messageType, defaultColor, string.Empty);
        }

        public static Message System(object message)
        {
            return new Message(message, MessageType.SYSTEM, systemColor, string.Empty);
        }

        public static Message Warning(object message, string customType)
        {
            return new Message(message, MessageType.WARNING, warningColor, customType);
        }

        public static Message Error(object message, string customType)
        {
            return new Message(message, MessageType.ERROR, errorColor, customType);
        }

        public static Message Output(object message)
        {
            return new Message(message, MessageType.OUTPUT, outputColor, string.Empty);
        }

        public static Message Input(object message)
        {
            return new Message(message, MessageType.INPUT, inputColor, string.Empty);
        }

        public static Message Unity(object message)
        {
            return new Message(message, MessageType.UNITY, unityColor, string.Empty);
        }

        public override string ToString()
        {
            return ToGUIString();
        }

        ///need color
        public string ToGUIString()
        {
            if (!string.IsNullOrEmpty(formatted))
                return formatted;

            var hexColor = ColorToHex(this.color);
            string prefix;
            string logText = text;
            switch (type)
            {
                case MessageType.INPUT:
                    prefix = ">>>";
                    break;
                case MessageType.OUTPUT:
                    var lines = text.Trim('\n').Split('\n');
                    var output = new StringBuilder();

                    for (int i = 0; lines != null && i < lines.Length; i++)
                    {
                        output.AppendLine("= " + lines[i]);
                    }
                    prefix = "";
                    logText = output.ToString();
                    break;
                case MessageType.SYSTEM:
                    prefix = "#";
                    break;
                case MessageType.WARNING:
                    prefix = "*";
                    break;
                case MessageType.ERROR:
                    prefix = "**";
                    break;
                case MessageType.UNITY:
                    prefix = "***";
                    break;
                default:
                    prefix = "";
                    break;
            }

            formatted = $"<color=#{hexColor}>{prefix} {logText}</color>\n";
            return formatted;
        }

        static string ColorToHex(Color32 color)
        {
            string hex = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
            return hex;
        }
    }
}

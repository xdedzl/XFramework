using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using System.Linq;

namespace XFramework.Console
{
    public delegate bool CommandDelegate(string cmd, out object result);
    
    public static partial class XConsole
    {
        private static Action<Message> LogMessageReceived;

        private static readonly Queue<Message> m_messages = new ();
        private static readonly IConsole console = new UGUIConsole();
        
        private static readonly Dictionary<string, CommandDelegate> m_ExecuteFunctions = new ();
        private static readonly List<CommandDelegate> m_AutoFunctions = new ();
        private static string m_CurrentExecuteKey = "";
            
        private static bool m_isOpen;
        private static bool m_isInit;
        
        
        private static readonly LinkedList<string> cmdCache = new ();
        private static LinkedListNode<string> currentCmd;

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
        
        static XConsole()
        {
            AddCommand("Auto", ExecuteAutoCommand);
            AddCommand("GM", ExecuteGMCommand);
        }

        private static void OnInit()
        {
            XConsole.LogMessage(Message.System("start XConsole"));
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
                XConsole.LogMessage(Message.System("change execute commander to " + executeKey));

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
            return Execute(cmd, out var result);
        }
        
        public static bool Execute(string cmd, out object result)
        {
            LogMessage(Message.Input(cmd));
            result = null;
            m_ExecuteFunctions.TryGetValue(m_CurrentExecuteKey, out var executeFun);
            executeFun?.Invoke(cmd, result);

            if (result != null)
            {
                Log(result);
            }
            console.OnExecuteCmd(cmd, result);
            cmdCache.AddLast(cmd);
            currentCmd = null;
            
            return true;
        }

        private static bool ExecuteGMCommand(string cmd, out object result)
        {
            return GMCommand.Execute(cmd, out result);
        }
        
        private static bool ExecuteAutoCommand(string cmd, out object result)
        {
            foreach (var func in m_AutoFunctions)
            {
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
            if (currentCmd == null)
            {
                currentCmd = cmdCache.Last;
            }
            else if (currentCmd.Previous != null)
            {
                currentCmd = currentCmd.Previous;
            }
            else
            {
                return;
            }
            console.OnCurrentCmdChanged(currentCmd?.Value);
        }

        public static void JumpToNextCmd()
        {
            if (currentCmd != null && currentCmd.Next != null)
            {
                currentCmd = currentCmd.Next;
            }
            console.OnCurrentCmdChanged(currentCmd?.Value);
        }
        
        public static void Clear()
        {
            cmdCache.Clear();
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
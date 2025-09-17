using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace XFramework.Console
{
    public static partial class XConsole
    {
        private static Action<Message> LogMessageReceived;

        private static readonly Queue<Message> m_messages = new ();
        private static readonly IConsole console = new UGUIConsole();
        
        private static readonly Dictionary<string, Func<string, object>> m_ExecuteFunctions = new ();
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

        static XConsole()
        {
            
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
        
        public static bool AddExecuteFunction(string executeKey, Func<string, object> func)
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
            return true;
        }

        public static bool ChangeExecuteFunction(string executeKey)
        {
            if (m_ExecuteFunctions.ContainsKey(executeKey))
            {
                m_CurrentExecuteKey = executeKey;
                return true;
            }
            else
            {
                Debug.LogError($"Execute function {executeKey} not found.");
                return false;
            }
        }
        
        public static object Execute(string cmd)
        {
            LogMessage(Message.Input(cmd));
            
            m_ExecuteFunctions.TryGetValue(m_CurrentExecuteKey, out var executeFun);
            object result = executeFun?.Invoke(cmd);

            if (result != null)
            {
                Log(result);
            }
            console.OnExecuteCmd(cmd, result);
            cmdCache.AddLast(cmd);
            currentCmd = null;
            
            return result;
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
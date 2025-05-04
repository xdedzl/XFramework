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

        private static readonly Queue<Message> m_messages = new Queue<Message>();
        private static readonly IConsole console = new UGUIConsole();

        private static bool m_isOpen;
        private static bool m_isInit;

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
            var typeBase = typeof(GMCommandBase);
            var sonTypes = Utility.Reflection.GetTypesInAllAssemblies((type) =>
            {
                if (type.IsSubclassOf(typeBase) && !type.IsAbstract)
                {
                    return true;
                }
                return false;
            });

            foreach (var type in sonTypes)
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<GMCommandAttribute>();
                    if (attr != null)
                    {
                        var cmd = attr.cmd is null ? method.Name : attr.cmd;
                        var parms = method.GetParameters();
                        if (parms.Length == 0)
                        {
                            CSharpInterpreter.Instance.AddCmd(cmd, (parm) =>
                            {
                                return method.Invoke(null, null);
                            });

                        }
                        else if (parms.Length == 1 || parms[0].ParameterType == typeof(string))
                        {
                            CSharpInterpreter.Instance.AddCmd(cmd, (parm) =>
                            {
                                return method.Invoke(null, new object[] { parm });
                            });
                        }
                        else
                        {
                            Debug.LogWarning($"[非法GM指令] {type.Name}.{method.Name}, GM函数只允许传一个string参数或不传参");
                            continue;
                        }
                    }
                }
            }
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

        public static object Excute(string cmd)
        {
            LogMessage(Message.Input(cmd));

            object value = null;
            try
            {
                value = CSharpInterpreter.Instance.Excute(cmd);
            }
            catch (Exception e)
            {
                LogError($"{e.Message}\n{e.StackTrace}");
            }

            if (value != null)
            {
                Log(value);
            }
            console.OnExcuteCmd(cmd, value);
            return value;
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

        void OnExcuteCmd(string cmd, object value);

        void OnClear();
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
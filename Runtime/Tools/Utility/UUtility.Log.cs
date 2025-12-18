using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace XFramework
{
    public partial class UUtility
    {
        private const string DEBUG_LOG = "DEBUG_LOG";

        /// <summary>
        /// 红色
        /// </summary>
        public const string COLOR_RED = "A83131";

        /// <summary>
        /// 橙色 
        /// </summary>
        public const string COLOR_ORANGE = "DE4D08";

        /// <summary>
        /// 黄色
        /// </summary>
        public const string COLOR_YELLOW = "D5CB6C";

        /// <summary>
        /// 绿色
        /// </summary>
        public const string COLOR_GREEN = "33B1B0";

        /// <summary>
        /// 蓝色 
        /// </summary>
        public const string COLOR_BLUE = "2762BD";

        /// <summary>
        /// 紫色
        /// </summary>
        public const string COLOR_PURPLE = "865FC5";

        /// <summary>
        /// 打印信息
        /// </summary>
        /// <param name="message"></param>
        [Conditional(DEBUG_LOG)]
        public static void Log(object message)
        {
            Debug.Log(message);
        }

        /// <summary>
        /// 打印信息
        /// </summary>
        [Conditional(DEBUG_LOG)]
        public static void Log(string format, params object[] args)
        {
            Debug.LogFormat(format, args);
        }

        /// <summary>
        /// 打印彩色信息
        /// </summary>
        /// <param name="color"></param>
        /// <param name="message"></param>
        [Conditional(DEBUG_LOG)]
        public static void LogColor(string color, object message)
        {
            message = $"<color=#{color}>{message}</color>";
            Log(message);
        }

        /// <summary>
        /// 打印彩色信息
        /// </summary>
        /// <param name="color"></param>
        /// <param name="message"></param>
        [Conditional(DEBUG_LOG)]
        public static void LogColor(string color, string format, params object[] args)
        {
            var message = $"<color=#{color}>{string.Format(format, args)}</color>";
            Log(message);
        }


        /// <summary>
        /// 打印警告
        /// </summary>
        [Conditional(DEBUG_LOG)]
        public static void LogWarning(object message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>
        /// 打印警告
        /// </summary>
        [Conditional(DEBUG_LOG)]
        public static void LogWarning(string format, params object[] args)
        {
            Debug.LogWarningFormat(format, args);
        }

        /// <summary>
        /// 打印错误
        /// </summary>
        [Conditional(DEBUG_LOG)]
        public static void LogError(object message)
        {
            Debug.LogError(message);
        }

        /// <summary>
        /// 打印错误
        /// </summary>
        [Conditional(DEBUG_LOG)]
        public static void LogError(string format, params object[] args)
        {
            Debug.LogErrorFormat(format, args);
        }

        [Conditional(DEBUG_LOG)]
        public static void LogColorGUI(string color, object content)
        {
            var message = $"<color=#{color}>{content}</color>";
            LogGUI(message);
        }

        [Conditional(DEBUG_LOG)]
        public static void LogGUI(string color, string format, params object[] args)
        {
            var message = $"<color=#{color}>{string.Format(format, args)}</color>";
            LogGUI(message);
        }

        [Conditional(DEBUG_LOG)]
        public static void LogGUI(string format, params object[] args)
        {
            LogGUI(string.Format(format, args));
        }

        /// <summary>
        /// 在Game窗口显示一条日志消息
        /// </summary>
        /// <param name="content"></param>
        [Conditional(DEBUG_LOG)]
        public static void LogGUI(string content)
        {
            //content = string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss.fff"), content);
            Log(content);
            //GUILog.Show(content);
        }
    }
}
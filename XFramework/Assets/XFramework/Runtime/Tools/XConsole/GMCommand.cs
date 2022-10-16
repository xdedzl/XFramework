using System;
using UnityEngine;

namespace XFramework.Console
{
    public abstract class GMCommandBase
    {
        public abstract int Order { get; }
        public abstract string TabName { get; }
    }

    public class XGMCommand : GMCommandBase
    {
        public override int Order => -1;

        public override string TabName => "";

        [GMCommand("clear")]
        public static void ClearConsole()
        {
            XConsole.Clear();
        }

        [GMCommand("enable_hunter")]
        public static void StartHunter()
        {
            XConsole.Log("已成功打开Hunter");
            XConsole.ConnetHunter();
        }

        [GMCommand("disable_hunter")]
        public static void StopHunter()
        {
            XConsole.Log("已成功关闭Hunter");
            XConsole.DisConnetHunter();
        }

        [GMCommand("log")]
        public static void UnityLog(string content)
        {
            Debug.Log(content);
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class GMCommandAttribute : Attribute
    {
        public string cmd;
        public string name;
        public int order;
        public GMCommandAttribute(string cmd = null, string name = null, int order = -1)
        {
            this.cmd = cmd;
            this.name = name;
            this.order = order;
        }
    }
}
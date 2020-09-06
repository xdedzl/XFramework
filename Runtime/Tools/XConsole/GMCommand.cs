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
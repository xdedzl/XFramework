using System;
using System.Collections.Generic;
using UnityEngine;


namespace XFramework.Console
{
    /// <summary>
    /// c#解释器
    /// </summary>
    public class CSharpInterpreter : Singleton<CSharpInterpreter>
    {
        private List<string> nameSpaces = new List<string>();
        private Dictionary<string, Func<string, object>> cmds = new Dictionary<string, Func<string, object>>();

        private CSharpInterpreter()
        {
            AddCmd("print", Print);
            AddCmd("using", Using);
        }

        public object Excute(string cmd)
        {
            for (int i = 0; i < cmd.Length; i++)
            {
                if (cmd[i] == '.')
                {
                    return ExcuteCSharp(cmd);
                }
                else if (cmd[i] == ' ')
                {
                    string cmdName = cmd.Substring(0, i);
                    string arg = cmd.Substring(i + 1, cmd.Length - (i + 1));
                    return ExcuteCmd(cmdName, arg);
                }
            }

            return ExcuteCmd(cmd, "");
        }

        public void AddCmd(string cmd, Func<string, object> fun)
        {
            if (cmds.ContainsKey(cmd))
            {
                Debug.LogWarning($"[指令重复] cmd name: {cmd}");
                return;
            }
            cmds.Add(cmd, fun);
        }

        #region cmd指令

        private object ExcuteCmd(string cmd, string arg)
        {
            if (cmds.TryGetValue(cmd, out Func<string, object> fun))
            {
                return fun.Invoke(arg);
            }
            return null;
        }

        private object Print(string arg)
        {
            return arg;
        }

        private object Using(string arg)
        {
            nameSpaces.Add(arg);
            return null;
        }

        #endregion

        #region 解析c#

        private object ExcuteCSharp(string cmd)
        {
            var cmds = cmd.Split('.');
            var className = cmds[0];

            return null;
        }

        #endregion
    }
}
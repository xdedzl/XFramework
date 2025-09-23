using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace XFramework.Console
{
    public class GMCommand
    {
        private static readonly Dictionary<string, Func<string, object>> s_Commands = new();
        
        static GMCommand()
        {
            var typeBase = typeof(GMCommand);
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
                        var cmd = attr.cmd ?? method.Name;
                        var parms = method.GetParameters();
                        if (parms.Length == 0)
                        {
                            AddCmd(cmd, (parm) => method.Invoke(null, null));

                        }
                        else if (parms.Length == 1 || parms[0].ParameterType == typeof(string))
                        {
                            AddCmd(cmd, (parm) =>
                            {
                                return method.Invoke(null, new object[] { parm });
                            });
                        }
                        else
                        {
                            Debug.LogWarning($"[非法GM指令] {type.Name}.{method.Name}, GM函数只允许传一个string参数或不传参");
                        }
                    }
                }
            }    
        }
        
        public static bool Execute(string cmd, out object result)
        {
            var args = cmd.Split(' ');
            result = null;
            if (s_Commands.ContainsKey(args[0]))
            {
                try
                {
                    if (args.Length == 1)
                    {
                        return ExecuteCmd(cmd, "", out result);
                    }
                    else
                    {
                        return ExecuteCmd(args[0], args[1], out result);
                    }
                }
                catch (Exception e)
                {
                    // LogError($"{e.Message}\n{e.StackTrace}");
                    return false;
                }
            }

            return false;
        }
        

        /// <summary>
        /// 添加一个指令
        /// </summary>
        /// <param name="cmd">命令关键字</param>
        /// <param name="fun">指令调用内容</param>
        public static void AddCmd(string cmd, Func<string, object> fun)
        {
            if (s_Commands.ContainsKey(cmd))
            {
                Debug.LogWarning($"[指令重复] cmd name: {cmd}");
                return;
            }
            s_Commands.Add(cmd, fun);
        }

        private static bool ExecuteCmd(string cmd, string arg, out object result)
        {
            if (s_Commands.TryGetValue(cmd, out Func<string, object> fun))
            {
                result = fun.Invoke(arg);
                return true;
            }
            result = null;
            return false;
        }
    }
    
    public class XGMCommand : GMCommand
    {
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
        public readonly string cmd;
        public readonly string name;
        public readonly int order;
        public GMCommandAttribute(string cmd = null, string name = null, int order = -1)
        {
            this.cmd = cmd;
            this.name = name;
            this.order = order;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class GMCommandClassAttribute : Attribute
    {
        
    }
}
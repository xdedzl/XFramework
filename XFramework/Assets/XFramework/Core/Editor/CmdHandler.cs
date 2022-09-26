using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace XFramework.Editor
{
    public static class CmdHandler
    {
        private static readonly Dictionary<string, Action<string[]>> cmdDic = new Dictionary<string, Func<string[]>>;

        static CmdHandler()
        {
            var type = typeof(CmdHandler);
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<CmdAttribute>();
                if (attr != null)
                {
                    var cmd = attr.name is null ? method.Name : attr.name;
                    var parms = method.GetParameters();
                    if (parms.Length == 0)
                    {
                        cmdDic.Add(cmd, (args) => { method.Invoke(null, null); });
                    }
                    else if (parms.Length >= 1 || parms[0].ParameterType == typeof(string))
                    {
                        foreach (var parm in parms)
                        {
                            var tyoe = parm.ParameterType;
                            if (type != typeof(string))
                            {
                                Debug.LogWarning($"[非法CMD指令] {type.Name}.{method.Name} -- {parm.Name}, {parm.ParameterType.Name}, cmd函数只允许string数组或不传参");
                                goto done;
                            }
                        }
                        cmdDic.Add(cmd, (args) => { method.Invoke(null, new string[0]); });
                    }
                }

                done: continue;
            }
        }

        [Cmd("test")]
        public static void TestCmd(params string[] args)
        {
            
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class CmdAttribute: Attribute
    {
        public string name;
        public CmdAttribute(string name=null)
        {
            this.name = name;
        }
    }
}

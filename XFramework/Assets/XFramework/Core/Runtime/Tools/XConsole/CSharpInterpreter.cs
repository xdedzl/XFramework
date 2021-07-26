using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace XFramework.Console
{
    /// <summary>
    /// c#解释器
    /// </summary>
    public class CSharpInterpreter : Singleton<CSharpInterpreter>
    {
        private Dictionary<string, Func<string, object>> cmds = new Dictionary<string, Func<string, object>>();
        private CodeGenerater codeGenerater = new CodeGenerater();

        private CSharpInterpreter()
        {
            AddCmd("print", Print);
            AddCmd("using", Using);
        }

        public object Excute(string cmd)
        {
            var strs = cmd.Split(' ');
            if (cmds.ContainsKey(strs[0]))
            {

                if (strs.Length == 1)
                {
                    return ExcuteCmd(cmd, "");
                }
                else
                {
                    return ExcuteCmd(strs[0], strs[1]);
                }
            }

            return ExcuteCSharp(cmd);
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

        private object Using(string name)
        {
            codeGenerater.AddNameSpace(name);
            return null;
        }

        #endregion

        #region 解析c#

        private object ExcuteCSharp(string cmd)
        {
            CSharpCodeProvider codeProvider = new CSharpCodeProvider();
            CompilerParameters compilerParameters= new CompilerParameters();
            Assembly[] assemblys = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var item in assemblys)
            {
                compilerParameters.ReferencedAssemblies.Add(item.Location);
            }
            compilerParameters.GenerateExecutable = false;
            compilerParameters.GenerateInMemory = true;
            compilerParameters.OutputAssembly = "DynamicAssembly";

            Class dynamicClass = new Class("DynamicClass");

            codeGenerater.AddClass(dynamicClass);

            CompilerResults cr = codeProvider.CompileAssemblyFromSource(compilerParameters, codeGenerater.Code);
            if (cr.Errors.HasErrors)
            {
                var msg = string.Join(Environment.NewLine, cr.Errors.Cast<CompilerError>().First().ErrorText);
                Debug.LogError(msg);
            }
            else
            {
                Assembly objAssembly = cr.CompiledAssembly;
                object dyClass = objAssembly.CreateInstance("DynamicClass");
            }

            return null;
        }

        #endregion
    }
}
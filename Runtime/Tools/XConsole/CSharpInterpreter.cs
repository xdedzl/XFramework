using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
        private CSharpCodeProvider codeProvider = new CSharpCodeProvider();
        private CompilerParameters compilerParameters = new CompilerParameters();
        private Dictionary<string, object> dynamicValues = new Dictionary<string, object>();
        private readonly string ExpressionPattern = @"[^!=]=[^=]";

        private CSharpInterpreter()
        {
            AddCmd("print", Print);
            AddCmd("using", Using);


            Assembly[] assemblys = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var item in assemblys)
            {
                compilerParameters.ReferencedAssemblies.Add(item.Location);
            }
            compilerParameters.GenerateExecutable = false;
            compilerParameters.GenerateInMemory = true;
            compilerParameters.OutputAssembly = "DynamicAssembly";
        }

        /// <summary>
        /// 执行一行代码
        /// </summary>
        /// <param name="cmd"></param>
        public object Excute(string cmd)
        {
            var strs = cmd.Split(' ');
            if (dynamicValues.TryGetValue(cmd, out object value))
            {
                return value;
            }
            else if (cmds.ContainsKey(strs[0]))
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

        /// <summary>
        /// 添加一个指令
        /// </summary>
        /// <param name="cmd">命令关键字</param>
        /// <param name="fun">指令调用内容</param>
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
            codeGenerater.ClearClasses();

            Function function = new Function
            {
                name = "DynamicFunction",
            };

            Match match = Regex.Match(cmd, ExpressionPattern);
            if (match.Success)
            {
                string variableName = cmd.Substring(0, match.Index + 1).Trim();
                string variableValue = cmd.Substring(match.Index + 2, cmd.Length - (match.Index + 2)).Trim();
                function.returnType = typeof(object);
                function.contents = new string[]
                {
                    $"var {variableName} = {variableValue}",
                    $"return {variableName}"
                };
            }
            else
            {
                function.returnType = null;
                function.contents = new string[]
                {
                    cmd
                };
            }

            string className = "DynamicClass_" + Utility.Time.GetCurrentTimeStamp();
            Class dynamicClass = new Class(className);
            dynamicClass.AddFunction(function);
            codeGenerater.AddClass(dynamicClass);

            var value = ExcuteCode(codeGenerater.Code, className, "DynamicFunction");

            if (match.Success)
            {
                string variableName = cmd.Substring(0, match.Index + 1).Trim();
                return $"{variableName } = {value}";
            }
            else
            {
                return null;
            }
        }

        public object ExcuteCode(string code, string className, string method)
        {
            CompilerResults cr = codeProvider.CompileAssemblyFromSource(compilerParameters, code);
            if (cr.Errors.HasErrors)
            {
                var msg = string.Join(Environment.NewLine, cr.Errors.Cast<CompilerError>().First().ErrorText);
                Debug.LogError(msg);
                return null;
            }
            else
            {
                Assembly objAssembly = cr.CompiledAssembly;
                object dyClass = objAssembly.CreateInstance(className);
                var value = dyClass.GetType().GetMethod(method).Invoke(dyClass, null);
                return value;
            }
        }

        #endregion
    }
}
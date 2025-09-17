using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

using Microsoft.CodeAnalysis;


namespace XFramework.Console
{
    /// <summary>
    /// c#解释器
    /// </summary>
    public class CSharpInterpreter : Singleton<CSharpInterpreter>
    {
        private readonly Dictionary<string, Func<string, object>> cmds = new();
        private readonly CodeGenerater codeGenerater = new();
        private readonly string ExpressionPattern = @"[^!=]=[^=]";
        private readonly Dictionary<string, object> dynamicValues = new();

        public CSharpInterpreter()
        {
            AddCmd("print", Print);
            AddCmd("using", Using);
            
            
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

        /// <summary>
        /// 执行一行代码
        /// </summary>
        /// <param name="cmd"></param>
        public object Execute(string cmd)
        {
            var strs = cmd.Split(' ');
            if (dynamicValues.TryGetValue(cmd, out object value))
            {
                return value;
            }
            else if (cmds.ContainsKey(strs[0]))
            {
                try
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
                catch (Exception e)
                {
                    // LogError($"{e.Message}\n{e.StackTrace}");
                }
            }

            if (Application.platform == RuntimePlatform.Android)
                throw new XFrameworkException("平台不支持动态生成代码");

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
                dynamicValues[variableName] = value;
                return $"{variableName } = {value}";
            }
            else
            {
                return null;
            }
        }

        public object ExcuteCode(string code, string className, string method)
        {
            //CSharpSyntaxTree
#if QQQ
            CSharpCodeProvider codeProvider = new CSharpCodeProvider();
            CompilerParameters compilerParameters = new CompilerParameters();
            Assembly[] assemblys = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var item in assemblys)
            {
                if (item.Location.Contains("Unity.Plastic.Newtonsoft.Json.dll"))
                    continue;
                compilerParameters.ReferencedAssemblies.Add(item.Location);
            }
            compilerParameters.GenerateExecutable = false;
            compilerParameters.GenerateInMemory = true;
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

                var methodInfo = dyClass.GetType().GetMethod(method);
                

                if (methodInfo.ReturnType != typeof(void))
                {
                    var @delegate = Utility.Reflection.MethodWrapperFunc<object>(dyClass, methodInfo);
                    var value = @delegate.Invoke();
                    return value;
                }
                else
                {
                    var @delegate = Utility.Reflection.MethodWrapperAction(dyClass, methodInfo);
                    @delegate.Invoke();
                    return null;
                }
            }
#endif
            return null;
        }

#endregion
    }
}
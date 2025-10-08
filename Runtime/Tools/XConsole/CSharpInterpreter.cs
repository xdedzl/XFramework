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
    public class CSharpInterpreter
    {
        private readonly CodeGenerator codeGenerator = new();
        private readonly string ExpressionPattern = @"[^!=]=[^=]";
        private readonly Dictionary<string, object> dynamicValues = new();

        /// <summary>
        /// 执行一行代码
        /// </summary>
        public bool Execute(string cmd, out object result)
        {
            result = null;
            if (dynamicValues.TryGetValue(cmd, out object value))
            {
                result = value;
                return true;
            }

            if (Application.platform != RuntimePlatform.WindowsEditor)
                return false;
            
            ExecuteCSharp(cmd);
            return true;
        }

        #region cmd指令
        public void Using(string name)
        {
            codeGenerator.AddNameSpace(name);
        }

        #endregion
        
        private object ExecuteCSharp(string cmd)
        {
            codeGenerator.ClearClasses();

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
            codeGenerator.AddClass(dynamicClass);

            var value = ExecuteCode(codeGenerator.Code, className, "DynamicFunction");
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

        public static object ExecuteCode(string code, string className, string method)
        {
            Assembly objAssembly = null;
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
    }
}
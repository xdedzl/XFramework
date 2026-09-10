using System;
using System.Collections.Generic;
using XFramework.Command;

namespace XFramework
{
    public sealed class ProcedureConsoleCommands : XCommandGroup
    {
        [XCommandEntry("procedure-switch", name = "切换主流程", order = 0, mode = XCommandMode.Runtime, category = "Procedure", description = "按类型名或完整类型名切换到唯一的 MainProcedure，带 Request 的流程使用默认 Request。", usage = "procedure-switch PROCEDURE_TYPE", displayType = XCommandDisplayType.Hidden)]
        private static object SwitchProcedure(string argument)
        {
            string procedureTypeName = RequireProcedureTypeName(argument);
            Type procedureType = ResolveMainProcedureType(procedureTypeName);
            string previousProcedure = ProcedureManager.Instance.CurrentProcedure?.GetType().FullName ?? string.Empty;
            ProcedureManager.Instance.ChangeProcedure(procedureType);
            string currentProcedure = ProcedureManager.Instance.CurrentProcedure?.GetType().FullName ?? string.Empty;
            return new {
                previousProcedure,
                currentProcedure,
                changed = !string.Equals(previousProcedure, currentProcedure, StringComparison.Ordinal),
            };
        }

        private static string RequireProcedureTypeName(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                throw new ArgumentException("usage: procedure-switch PROCEDURE_TYPE");
            return argument.Trim();
        }

        private static Type ResolveMainProcedureType(string procedureTypeName)
        {
            var matches = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsAbstract || !typeof(MainProcedure).IsAssignableFrom(type))
                        continue;
                    if (string.Equals(type.Name, procedureTypeName, StringComparison.Ordinal) || string.Equals(type.FullName, procedureTypeName, StringComparison.Ordinal))
                        matches.Add(type);
                }
            }
            if (matches.Count == 0)
                throw new ArgumentException($"找不到 MainProcedure：{procedureTypeName}");
            if (matches.Count > 1)
                throw new ArgumentException($"MainProcedure 名称不唯一，请使用完整类型名：{procedureTypeName}");
            return matches[0];
        }
    }
}

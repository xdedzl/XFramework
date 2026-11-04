using System;
using System.Globalization;
using UnityEngine;

namespace XFramework.Command
{
    public sealed class XCommandCoreCommands : XCommandGroup
    {
        private const string Category = "XCommand";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterCommandGroup()
        {
            XCommandRegistry.RegisterGroup(typeof(XCommandCoreCommands));
        }

        [XCommandEntry("operation-list", name = "列出自动化操作", order = 80, mode = XCommandMode.Both, category = Category, description = "列出最近的 XCommand 异步操作及其状态。", usage = "operation-list [--limit N] [--compact]", displayType = XCommandDisplayType.Hidden)]
        private static object ListOperations(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, new[] { "limit" }, new[] { "compact" });
            args.RequireValueCount(0, "operation-list 不接受位置参数。");
            return XCommandPipeline.SerializeOperations(args.GetInt("limit", 20), !args.HasFlag("compact"));
        }

        [XCommandEntry("operation-status", name = "查询自动化操作", order = 90, mode = XCommandMode.Both, category = Category, description = "查询指定 XCommand 操作的状态、输出和错误。", usage = "operation-status [--compact] ID", displayType = XCommandDisplayType.Hidden)]
        private static object GetOperationStatus(string argument)
        {
            XCommandArguments args = XCommandArguments.Parse(argument, Array.Empty<string>(), new[] { "compact" });
            args.RequireValueCount(1, "usage: operation-status [--compact] ID");
            long operationId = ParseLong(args.Values[0], "operation ID");
            if (!XCommandPipeline.TryGetOperation(operationId, out XCommandOperationInfo operation))
            {
                throw new ArgumentException($"找不到 XCommand 操作：{operationId}");
            }
            return XCommandPipeline.SerializeOperation(operation, !args.HasFlag("compact"));
        }

        [XCommandEntry("operation-cancel", name = "取消自动化操作", order = 100, mode = XCommandMode.Runtime, category = Category, description = "取消指定 XCommand 操作，或使用 all 取消全部等待和排队操作。", usage = "operation-cancel ID|all", displayType = XCommandDisplayType.Hidden)]
        private static object CancelOperation(string argument)
        {
            string value = argument?.Trim() ?? string.Empty;
            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            {
                return $"Cancelled operations: {XCommandPipeline.CancelAllOperations()}";
            }
            long operationId = ParseLong(value, "operation ID");
            if (!XCommandPipeline.CancelOperation(operationId))
            {
                throw new InvalidOperationException($"操作 {operationId} 不存在或已经结束。");
            }
            XCommandPipeline.TryGetOperation(operationId, out XCommandOperationInfo operation);
            return XCommandPipeline.SerializeOperation(operation);
        }

        private static long ParseLong(string value, string name)
        {
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new ArgumentException($"{name} 不是有效整数：{value}");
            }
            return result;
        }
    }
}

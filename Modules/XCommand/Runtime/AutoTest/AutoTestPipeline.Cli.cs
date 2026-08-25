using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using XFramework.Command;

namespace XFramework.AutoTest
{
    internal sealed class AutoTestCliOperationProvider : IXFrameworkCliOperationProvider
    {
        private static readonly AutoTestCliOperationProvider s_Instance = new AutoTestCliOperationProvider();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            XFrameworkCliServer.SetOperationProvider(s_Instance);
        }

        public bool TryResolveOperation(object commandResult, out long operationId)
        {
            operationId = 0;
            if (!(commandResult is string json) || string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                JToken idToken = JObject.Parse(json)["id"];
                if (idToken == null || idToken.Type != JTokenType.Integer)
                    return false;
                operationId = idToken.Value<long>();
                return AutoTestPipeline.TryGetOperation(operationId, out _);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public bool TryGetOperation(long operationId, out XFrameworkCliOperationInfo operation)
        {
            operation = null;
            if (!AutoTestPipeline.TryGetOperation(operationId, out AutoTestOperationInfo autoTestOperation))
                return false;
            operation = new XFrameworkCliOperationInfo(autoTestOperation.Id, autoTestOperation.Name, ConvertState(autoTestOperation.State), autoTestOperation.CreatedTimeUtc, autoTestOperation.StartedTimeUtc, autoTestOperation.CompletedTimeUtc, autoTestOperation.DurationMilliseconds, autoTestOperation.Output, autoTestOperation.Error);
            return true;
        }

        private static XFrameworkCliOperationState ConvertState(AutoTestOperationState state)
        {
            switch (state)
            {
                case AutoTestOperationState.Queued:
                    return XFrameworkCliOperationState.Queued;
                case AutoTestOperationState.Running:
                    return XFrameworkCliOperationState.Running;
                case AutoTestOperationState.Succeeded:
                    return XFrameworkCliOperationState.Succeeded;
                case AutoTestOperationState.Failed:
                    return XFrameworkCliOperationState.Failed;
                case AutoTestOperationState.Cancelled:
                    return XFrameworkCliOperationState.Cancelled;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}

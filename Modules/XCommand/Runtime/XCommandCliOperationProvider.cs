using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace XFramework.Command
{
    internal sealed class XCommandCliOperationProvider : IXFrameworkCliOperationProvider
    {
        private static readonly XCommandCliOperationProvider s_Instance = new XCommandCliOperationProvider();

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
                return XCommandPipeline.TryGetOperation(operationId, out _);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public bool TryGetOperation(long operationId, out XFrameworkCliOperationInfo operation)
        {
            operation = null;
            if (!XCommandPipeline.TryGetOperation(operationId, out XCommandOperationInfo pipelineOperation))
                return false;
            operation = new XFrameworkCliOperationInfo(pipelineOperation.Id, pipelineOperation.Name, ConvertState(pipelineOperation.State), pipelineOperation.CreatedTimeUtc, pipelineOperation.StartedTimeUtc, pipelineOperation.CompletedTimeUtc, pipelineOperation.DurationMilliseconds, pipelineOperation.Output, pipelineOperation.Error);
            return true;
        }

        private static XFrameworkCliOperationState ConvertState(XCommandOperationState state)
        {
            switch (state)
            {
                case XCommandOperationState.Queued:
                    return XFrameworkCliOperationState.Queued;
                case XCommandOperationState.Running:
                    return XFrameworkCliOperationState.Running;
                case XCommandOperationState.Succeeded:
                    return XFrameworkCliOperationState.Succeeded;
                case XCommandOperationState.Failed:
                    return XFrameworkCliOperationState.Failed;
                case XCommandOperationState.Cancelled:
                    return XFrameworkCliOperationState.Cancelled;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}

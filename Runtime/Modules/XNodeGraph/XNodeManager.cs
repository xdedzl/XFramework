using System;
using System.Collections.Generic;
using XFramework;
using UnityEngine;

namespace XFramework.NodeKit
{
    /// <summary>
    /// 剧情管理
    /// </summary>
    [ModuleLifecycle(ModuleLifecycle.Persistent)]
    public class XNodeManager : GameModuleBase<XNodeManager>
    {
        /// <summary>
        /// 用于处理故事的处理器集合
        /// </summary>
        private readonly Dictionary<string, XNodeGraph> m_RunningGraphs = new Dictionary<string, XNodeGraph>();
        
        public event Action<string, bool> OnGraphStop;
        public event Action<string> OnGraphStart;

        public string StartNodeGraph(XNodeGraphAsset asset)
        {
            return StartNodeGraph(asset, null);
        }

        public string StartNodeGraph(XNodeGraphAsset asset, Action<string> onGraphCreated)
        {
            var storyGraph = new XNodeGraph(this, asset);
            m_RunningGraphs.Add(storyGraph.id, storyGraph);
            onGraphCreated?.Invoke(storyGraph.id);
            storyGraph.Start();
            if (m_RunningGraphs.ContainsKey(storyGraph.id))
            {
                OnGraphStart?.Invoke(storyGraph.id);
            }

            return storyGraph.id;
        }

        public bool TryEvaluate<T>(XNodeGraphAsset asset, out T result)
        {
            result = default;
            if (asset == null)
            {
                Debug.LogError($"[XNodeManager] Evaluate failed: asset is null, resultType={typeof(T).Name}.");
                return false;
            }

            var graph = new XNodeGraph(this, asset);
            graph.Start();

            if (!graph.IsCompleted)
            {
                Debug.LogError(
                    $"[XNodeManager] Evaluate failed: graph did not complete synchronously, graphId={graph.id}, resultType={typeof(T).Name}.");
                return false;
            }

            if (!graph.HasResult)
            {
                Debug.LogError(
                    $"[XNodeManager] Evaluate failed: graph completed without result, graphId={graph.id}, resultType={typeof(T).Name}.");
                return false;
            }

            if (graph.Result == null)
            {
                if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                {
                    Debug.LogError(
                        $"[XNodeManager] Evaluate failed: result is null but target type is non-nullable value type, graphId={graph.id}, resultType={typeof(T).Name}.");
                    return false;
                }

                result = default;
                return true;
            }

            if (graph.Result is T typedResult)
            {
                result = typedResult;
                return true;
            }

            Debug.LogError(
                $"[XNodeManager] Evaluate failed: result type mismatch, graphId={graph.id}, actualType={graph.Result.GetType().Name}, expectedType={typeof(T).Name}.");
            return false;
        }

        public T Evaluate<T>(XNodeGraphAsset asset)
        {
            if (TryEvaluate(asset, out T result))
            {
                return result;
            }

            throw new InvalidOperationException(
                $"XNodeGraph evaluate failed or result type is not {typeof(T).Name}.");
        }

        public bool IsGraphRunning(string id)
        {
            return m_RunningGraphs.ContainsKey(id);
        }

        public void StopGraph(string id)
        {
            StopGraph(id, false);
        }

        public void StopGraph(string id, bool isCompleted)
        {
            if (m_RunningGraphs.Remove(id))
            {
                OnGraphStop?.Invoke(id, isCompleted);
            }
        }
    }
}

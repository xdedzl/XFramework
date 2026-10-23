using System;
using System.Collections;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    [Serializable]
    internal sealed class XCommandPlayModeOperationOutput
    {
        public bool succeeded;
        public string action;
        public string state;
        public bool isPlaying;
        public bool isPlayingOrWillChangePlaymode;
        public int serverPort;
    }

    [Serializable]
    internal sealed class XCommandPlayModeOperationStorage
    {
        public XCommandPipeline.XCommandOperationJson operation;
        public string action;
        public string expectedState;
        public string observedState;
        public bool requestSubmitted;
    }

    [InitializeOnLoad]
    internal sealed class XCommandPlayModeOperation : IXCommandAsyncOperation
    {
        private const string STORAGE_KEY = "XFramework.XCommand.PlayModeOperation";
        private const string PLAY_START = "play-start";
        private const string PLAY_STOP = "play-stop";
        private const double REQUEST_DELAY_SECONDS = 0.5d;

        private static XCommandPlayModeOperationStorage storage;

        private readonly string m_Action;
        private readonly string m_ExpectedState;

        static XCommandPlayModeOperation()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Save;
            AssemblyReloadEvents.afterAssemblyReload += Restore;
            EditorApplication.quitting += OnEditorQuitting;
            Restore();
            EditorApplication.delayCall += Restore;
        }

        private XCommandPlayModeOperation(string action, string expectedState)
        {
            m_Action = action;
            m_ExpectedState = expectedState;
        }

        public string Name => m_Action;

        public bool CanCancel => false;

        internal static object StartStart()
        {
            return Start(PLAY_START, PlayModeStateChange.EnteredPlayMode);
        }

        internal static object StartStop()
        {
            return Start(PLAY_STOP, PlayModeStateChange.EnteredEditMode);
        }

        private static object Start(string action, PlayModeStateChange expectedState)
        {
            if (TryGetCurrentOperation(out XCommandOperationInfo current) && !IsCompleted(current.State))
            {
                if (storage != null && string.Equals(storage.action, action, StringComparison.Ordinal))
                {
                    return JsonUtility.ToJson(XCommandPipeline.XCommandOperationJson.From(current));
                }

                throw new InvalidOperationException($"已有播放模式操作正在执行：{storage?.action}");
            }

            if (string.Equals(action, PLAY_START, StringComparison.Ordinal) && (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                throw new InvalidOperationException("Unity 当前正在编译、导入资源、运行或进入 Play Mode，无法提交 play-start。");
            }

            return XCommandPipeline.RunOperation(new XCommandPlayModeOperation(action, expectedState.ToString()));
        }

        public IEnumerator Run(XCommandOperationContext context)
        {
            InitializeStorage(context.Id);

            double requestStartTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - requestStartTime < REQUEST_DELAY_SECONDS)
            {
                yield return null;
            }

            if (storage == null || storage.operation == null)
            {
                throw new InvalidOperationException("播放模式 operation 在请求切换前丢失。");
            }

            if (IsCompletedString(storage.operation.state))
            {
                if (string.Equals(storage.operation.state, nameof(XCommandOperationState.Failed), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(storage.operation.error);
                }

                context.SetOutput(storage.operation.output);
                yield break;
            }

            storage.requestSubmitted = true;
            Save();

            try
            {
                RequestTransition();
            }
            catch (Exception exception)
            {
                Complete(false, $"提交播放模式切换失败：{exception.Message}");
            }

            while (true)
            {
                if (storage == null || storage.operation == null || !Enum.TryParse(storage.operation.state, true, out XCommandOperationState state))
                {
                    throw new InvalidOperationException("播放模式 operation 在等待过程中丢失。");
                }

                if (IsCompleted(state))
                {
                    if (state == XCommandOperationState.Failed)
                    {
                        context.SetOutput(storage.operation.output);
                        throw new InvalidOperationException(storage.operation.error);
                    }

                    context.SetOutput(storage.operation.output);
                    yield break;
                }

                if (!TryGetCurrentOperation(out _))
                {
                    throw new InvalidOperationException("播放模式 operation 在等待过程中丢失。");
                }

                yield return null;
            }
        }

        private void InitializeStorage(long operationId)
        {
            if (!XCommandPipeline.TryGetOperation(operationId, out XCommandOperationInfo operation))
            {
                throw new InvalidOperationException($"找不到播放模式 operation：{operationId}");
            }

            storage = new XCommandPlayModeOperationStorage
            {
                operation = XCommandPipeline.XCommandOperationJson.From(operation),
                action = m_Action,
                expectedState = m_ExpectedState,
                observedState = GetCurrentStateName(),
                requestSubmitted = false,
            };
            Save();
        }

        private void RequestTransition()
        {
            if (string.Equals(m_Action, PLAY_START, StringComparison.Ordinal))
            {
                EditorApplication.isPlaying = true;
                return;
            }

            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode || change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall -= ResetPipelineRunner;
                EditorApplication.delayCall += ResetPipelineRunner;
            }

            if (storage == null || storage.operation == null || IsCompletedString(storage.operation.state))
            {
                return;
            }

            storage.observedState = change.ToString();

            if (storage.requestSubmitted && string.Equals(storage.expectedState, PlayModeStateChange.EnteredPlayMode.ToString(), StringComparison.Ordinal) && change == PlayModeStateChange.EnteredEditMode)
            {
                Complete(false, "Unity 未能进入 Play Mode，已返回 Edit Mode。");
                return;
            }

            if (storage.requestSubmitted && string.Equals(storage.expectedState, PlayModeStateChange.EnteredEditMode.ToString(), StringComparison.Ordinal) && change == PlayModeStateChange.EnteredPlayMode)
            {
                Complete(false, "Unity 未能退出 Play Mode，仍处于 Play Mode。");
                return;
            }

            Save();
        }

        private static void ResetPipelineRunner()
        {
            EditorApplication.delayCall -= ResetPipelineRunner;
            XCommandPipeline.ResetRunner();
        }

        private static void OnEditorUpdate()
        {
            if (storage == null || storage.operation == null || !storage.requestSubmitted || IsCompletedString(storage.operation.state))
            {
                return;
            }

            if (!string.Equals(storage.observedState, storage.expectedState, StringComparison.Ordinal))
            {
                return;
            }

            if (!XFrameworkCliServer.IsRunning)
            {
                return;
            }

            Complete(true, null);
        }

        private static void Complete(bool succeeded, string error)
        {
            if (storage == null || storage.operation == null || IsCompletedString(storage.operation.state))
            {
                return;
            }

            string finalState = succeeded ? storage.expectedState : storage.observedState;
            var output = new XCommandPlayModeOperationOutput
            {
                succeeded = succeeded,
                action = storage.action,
                state = finalState,
                isPlaying = EditorApplication.isPlaying,
                isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode,
                serverPort = XFrameworkCliServer.IsRunning ? XFrameworkCliServer.Port : 0,
            };

            DateTime completedTimeUtc = DateTime.UtcNow;
            if (!DateTime.TryParse(storage.operation.createdTimeUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime createdTimeUtc))
            {
                createdTimeUtc = completedTimeUtc;
            }

            DateTime startedTimeUtc = createdTimeUtc;
            if (!string.IsNullOrEmpty(storage.operation.startedTimeUtc) && !DateTime.TryParse(storage.operation.startedTimeUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out startedTimeUtc))
            {
                startedTimeUtc = createdTimeUtc;
            }

            if (succeeded)
            {
                storage.observedState = storage.expectedState;
            }

            storage.operation.state = succeeded ? XCommandOperationState.Succeeded.ToString() : XCommandOperationState.Failed.ToString();
            storage.operation.completedTimeUtc = completedTimeUtc.ToString("O");
            storage.operation.durationMilliseconds = (completedTimeUtc - startedTimeUtc).TotalMilliseconds;
            storage.operation.output = JsonUtility.ToJson(output);
            storage.operation.error = error ?? string.Empty;
            storage.requestSubmitted = false;
            Save();
            XCommandPipeline.RestoreOperation(JsonUtility.ToJson(storage.operation), false);
        }

        private static bool TryGetCurrentOperation(out XCommandOperationInfo operation)
        {
            operation = null;
            if (storage == null || storage.operation == null || storage.operation.id <= 0)
            {
                return false;
            }

            if (!XCommandPipeline.TryGetOperation(storage.operation.id, out operation))
            {
                XCommandPipeline.RestoreOperation(JsonUtility.ToJson(storage.operation), false);
            }

            return XCommandPipeline.TryGetOperation(storage.operation.id, out operation);
        }

        private static string GetCurrentStateName()
        {
            if (EditorApplication.isPlaying)
            {
                return PlayModeStateChange.EnteredPlayMode.ToString();
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return PlayModeStateChange.ExitingEditMode.ToString();
            }

            return PlayModeStateChange.EnteredEditMode.ToString();
        }

        private static void Save()
        {
            if (storage == null || storage.operation == null || storage.operation.id <= 0)
            {
                SessionState.EraseString(STORAGE_KEY);
                return;
            }

            SessionState.SetString(STORAGE_KEY, JsonUtility.ToJson(storage));
        }

        private static void Restore()
        {
            string json = SessionState.GetString(STORAGE_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                XCommandPlayModeOperationStorage restoredStorage = JsonUtility.FromJson<XCommandPlayModeOperationStorage>(json);
                if (restoredStorage == null || restoredStorage.operation == null || restoredStorage.operation.id <= 0)
                {
                    return;
                }

                storage = restoredStorage;
                XCommandPipeline.RestoreOperation(JsonUtility.ToJson(storage.operation), false);
                OnEditorUpdate();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[XCommand] 恢复播放模式 operation 失败：{exception.Message}");
            }
        }

        private static bool IsCompleted(XCommandOperationState state)
        {
            return state == XCommandOperationState.Succeeded || state == XCommandOperationState.Failed || state == XCommandOperationState.Cancelled;
        }

        private static bool IsCompletedString(string state)
        {
            return string.Equals(state, nameof(XCommandOperationState.Succeeded), StringComparison.Ordinal) || string.Equals(state, nameof(XCommandOperationState.Failed), StringComparison.Ordinal) || string.Equals(state, nameof(XCommandOperationState.Cancelled), StringComparison.Ordinal);
        }

        private static void OnEditorQuitting()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= Save;
            AssemblyReloadEvents.afterAssemblyReload -= Restore;
            EditorApplication.delayCall -= Restore;
            EditorApplication.delayCall -= ResetPipelineRunner;
            EditorApplication.quitting -= OnEditorQuitting;
            SessionState.EraseString(STORAGE_KEY);
        }
    }
}

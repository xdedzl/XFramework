using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XFramework.Command
{
    public enum XCommandOperationState
    {
        Queued,
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    public sealed class XCommandOperationInfo
    {
        internal XCommandOperationInfo(long id, string name, Func<XCommandOperationContext, IEnumerator> routineFactory)
        {
            Id = id;
            Name = name;
            State = XCommandOperationState.Queued;
            CreatedTimeUtc = DateTime.UtcNow;
            RoutineFactory = routineFactory;
        }

        public long Id { get; }
        public string Name { get; }
        public XCommandOperationState State { get; internal set; }
        public DateTime CreatedTimeUtc { get; }
        public DateTime? StartedTimeUtc { get; internal set; }
        public DateTime? CompletedTimeUtc { get; internal set; }
        public double DurationMilliseconds { get; internal set; }
        public string Output { get; internal set; } = string.Empty;
        public string Error { get; internal set; } = string.Empty;

        internal Func<XCommandOperationContext, IEnumerator> RoutineFactory { get; }
        internal bool CancellationRequested { get; set; }
    }

    internal sealed class XCommandOperationContext
    {
        private readonly XCommandOperationInfo m_Operation;

        public XCommandOperationContext(XCommandOperationInfo operation)
        {
            m_Operation = operation;
        }

        public bool IsCancellationRequested => m_Operation.CancellationRequested;

        public void SetOutput(string output)
        {
            m_Operation.Output = output ?? string.Empty;
        }
    }

    public static class XCommandPipeline
    {
        private const int OperationHistoryLimit = 100;
        private static readonly Queue<XCommandOperationInfo> s_PendingOperations = new Queue<XCommandOperationInfo>();
        private static readonly List<XCommandOperationInfo> s_Operations = new List<XCommandOperationInfo>();
        private static long s_NextOperationId = 1;
        private static XCommandPipelineRunner s_Runner;
        private static Coroutine s_OperationCoroutine;
        private static bool s_IsRunnerActive;
        private static Action s_EditorStopHandler;

        public static IReadOnlyList<XCommandOperationInfo> Operations => s_Operations;

        public static void SetEditorStopHandler(Action handler)
        {
            s_EditorStopHandler = handler;
        }

        public static void StopRunning()
        {
            if (Application.isEditor)
            {
                if (s_EditorStopHandler == null)
                    throw new InvalidOperationException("XCommand Editor stop handler is not registered.");
                s_EditorStopHandler.Invoke();
                return;
            }
            Application.Quit();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOperations()
        {
            if (s_Runner != null)
            {
                if (s_OperationCoroutine != null)
                    s_Runner.StopCoroutine(s_OperationCoroutine);
                Object.Destroy(s_Runner.gameObject);
            }
            s_PendingOperations.Clear();
            s_Operations.Clear();
            s_NextOperationId = 1;
            s_Runner = null;
            s_OperationCoroutine = null;
            s_IsRunnerActive = false;
            XCommandUtility.Input.ResetState();
            XCommandUI.ResetSnapshots();
        }

        public static string ListUI(XCommandUIQuery query)
        {
            return XCommandUI.List(query ?? new XCommandUIQuery());
        }

        public static string GetGameObjectState(XCommandGameObjectQuery query)
        {
            return XCommandGameObjects.GetState(query);
        }

        public static string ListGameObjects(XCommandGameObjectListQuery query)
        {
            return XCommandGameObjects.List(query ?? new XCommandGameObjectListQuery());
        }

        public static long ActOnUI(XCommandUIActionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("ui-act", context => XCommandUI.Act(request, () => context.IsCancellationRequested, context.SetOutput));
        }

        public static long WaitForUI(XCommandUIWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait-for", context => XCommandUI.Wait(request, () => context.IsCancellationRequested, context.SetOutput));
        }

        public static long WaitForGameObject(XCommandGameObjectWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait-for", context => XCommandGameObjects.Wait(request, context.SetOutput));
        }

        public static long WaitForLog(XCommandLogWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait-for", context => XCommandUtility.Logs.Wait(request, context.SetOutput));
        }

        public static long Wait(XCommandWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait", context => XCommandUtility.Wait.Run(request, context.SetOutput));
        }

        public static long RunInput(XCommandInputSequence sequence)
        {
            if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
                throw new ArgumentException("输入序列必须包含非空 steps 数组。", nameof(sequence));
            return EnqueueOperation("ui-input", context => XCommandUtility.Input.Run(sequence, () => context.IsCancellationRequested, context.SetOutput));
        }

        public static string GetInputState(bool compact = false)
        {
            return XCommandUtility.Input.GetState(compact);
        }

        public static string ResetInput(bool compact = false)
        {
            XCommandUtility.Input.ResetInterruptedInput();
            return XCommandUtility.Input.GetState(compact);
        }

        public static string GetLogs(XCommandLogQuery query)
        {
            return XCommandUtility.Logs.Query(query ?? new XCommandLogQuery());
        }

        public static long CaptureScreenshot(XCommandScreenshotRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("screenshot", context => XCommandUtility.Capture.Run(request, context.SetOutput));
        }

        public static bool TryGetOperation(long operationId, out XCommandOperationInfo operation)
        {
            operation = s_Operations.Find(item => item.Id == operationId);
            return operation != null;
        }

        public static bool CancelOperation(long operationId)
        {
            if (!TryGetOperation(operationId, out XCommandOperationInfo operation) || IsCompleted(operation.State))
                return false;
            operation.CancellationRequested = true;
            if (operation.State == XCommandOperationState.Queued)
                CompleteOperation(operation, XCommandOperationState.Cancelled, "操作已取消。");
            return true;
        }

        public static int CancelAllOperations()
        {
            int cancelled = 0;
            for (int i = 0; i < s_Operations.Count; i++)
            {
                if (CancelOperation(s_Operations[i].Id))
                    cancelled++;
            }
            return cancelled;
        }

        internal static string SerializeOperation(XCommandOperationInfo operation, bool prettyPrint = false)
        {
            return JsonUtility.ToJson(XCommandOperationJson.From(operation), prettyPrint);
        }

        internal static string SerializeOperations(int limit, bool prettyPrint = false)
        {
            int count = Mathf.Clamp(limit, 1, OperationHistoryLimit);
            var output = new XCommandOperationListJson();
            for (int i = s_Operations.Count - 1; i >= 0 && output.operations.Count < count; i--)
                output.operations.Add(XCommandOperationJson.From(s_Operations[i]));
            output.count = output.operations.Count;
            return JsonUtility.ToJson(output, prettyPrint);
        }

        private static long EnqueueOperation(string name, Func<XCommandOperationContext, IEnumerator> routineFactory)
        {
            var operation = new XCommandOperationInfo(s_NextOperationId++, name, routineFactory);
            s_Operations.Add(operation);
            s_PendingOperations.Enqueue(operation);
            if (!s_IsRunnerActive)
            {
                EnsureRunner();
                s_IsRunnerActive = true;
                Coroutine runner = s_Runner.StartCoroutine(RunOperations());
                if (s_IsRunnerActive)
                    s_OperationCoroutine = runner;
            }
            return operation.Id;
        }

        private static void EnsureRunner()
        {
            if (s_Runner != null)
                return;
            var gameObject = new GameObject("[XFramework XCommand Pipeline]") {
                hideFlags = HideFlags.HideAndDontSave,
            };
            Object.DontDestroyOnLoad(gameObject);
            s_Runner = gameObject.AddComponent<XCommandPipelineRunner>();
        }

        private static IEnumerator RunOperations()
        {
            try
            {
                while (s_PendingOperations.Count > 0)
                {
                    XCommandOperationInfo operation = s_PendingOperations.Dequeue();
                    if (operation.State == XCommandOperationState.Cancelled)
                        continue;

                    operation.State = XCommandOperationState.Running;
                    operation.StartedTimeUtc = DateTime.UtcNow;
                    var context = new XCommandOperationContext(operation);
                    Exception failure = null;
                    var enumerators = new Stack<IEnumerator>();
                    try
                    {
                        enumerators.Push(operation.RoutineFactory(context));
                        while (enumerators.Count > 0 && !operation.CancellationRequested)
                        {
                            IEnumerator current = enumerators.Peek();
                            bool moved;
                            object yielded = null;
                            try
                            {
                                moved = current.MoveNext();
                                if (moved)
                                    yielded = current.Current;
                            }
                            catch (Exception exception)
                            {
                                failure = exception;
                                break;
                            }

                            if (!moved)
                            {
                                try
                                {
                                    (enumerators.Pop() as IDisposable)?.Dispose();
                                }
                                catch (Exception exception)
                                {
                                    failure = exception;
                                    break;
                                }
                                continue;
                            }

                            if (yielded is IEnumerator nested)
                                enumerators.Push(nested);
                            else
                                yield return yielded;
                        }
                    }
                    finally
                    {
                        while (enumerators.Count > 0)
                        {
                            try
                            {
                                (enumerators.Pop() as IDisposable)?.Dispose();
                            }
                            catch (Exception exception)
                            {
                                if (failure == null)
                                    failure = exception;
                            }
                        }
                    }

                    if (operation.CancellationRequested)
                    {
                        string cancellationError = "操作已取消。";
                        if (operation.Name == "ui-act" || operation.Name == "ui-input")
                        {
                            try
                            {
                                XCommandUtility.Input.ResetInterruptedInput();
                            }
                            catch (Exception exception)
                            {
                                cancellationError += $" 输入重置失败：{exception}";
                            }
                        }
                        CompleteOperation(operation, XCommandOperationState.Cancelled, cancellationError);
                    }
                    else if (failure != null)
                        CompleteOperation(operation, XCommandOperationState.Failed, failure.ToString());
                    else
                        CompleteOperation(operation, XCommandOperationState.Succeeded, string.Empty);
                }
            }
            finally
            {
                s_OperationCoroutine = null;
                s_IsRunnerActive = false;
            }
        }

        private static void CompleteOperation(XCommandOperationInfo operation, XCommandOperationState state, string error)
        {
            if (IsCompleted(operation.State))
                return;
            operation.State = state;
            operation.Error = error ?? string.Empty;
            operation.CompletedTimeUtc = DateTime.UtcNow;
            DateTime started = operation.StartedTimeUtc ?? operation.CreatedTimeUtc;
            operation.DurationMilliseconds = (operation.CompletedTimeUtc.Value - started).TotalMilliseconds;
            Debug.Log($"[XCommand] {SerializeOperation(operation)}");
            TrimOperationHistory();
        }

        private static void TrimOperationHistory()
        {
            while (s_Operations.Count > OperationHistoryLimit && IsCompleted(s_Operations[0].State))
                s_Operations.RemoveAt(0);
        }

        private static bool IsCompleted(XCommandOperationState state)
        {
            return state == XCommandOperationState.Succeeded || state == XCommandOperationState.Failed || state == XCommandOperationState.Cancelled;
        }

        [Serializable]
        private sealed class XCommandOperationJson
        {
            public long id;
            public string name;
            public string state;
            public string createdTimeUtc;
            public string startedTimeUtc;
            public string completedTimeUtc;
            public double durationMilliseconds;
            public string output;
            public string error;

            public static XCommandOperationJson From(XCommandOperationInfo operation)
            {
                return new XCommandOperationJson {
                    id = operation.Id,
                    name = operation.Name,
                    state = operation.State.ToString(),
                    createdTimeUtc = operation.CreatedTimeUtc.ToString("O"),
                    startedTimeUtc = operation.StartedTimeUtc?.ToString("O") ?? string.Empty,
                    completedTimeUtc = operation.CompletedTimeUtc?.ToString("O") ?? string.Empty,
                    durationMilliseconds = operation.DurationMilliseconds,
                    output = operation.Output,
                    error = operation.Error,
                };
            }
        }

        [Serializable]
        private sealed class XCommandOperationListJson
        {
            public int count;
            public List<XCommandOperationJson> operations = new List<XCommandOperationJson>();
        }

        private sealed class XCommandPipelineRunner : MonoBehaviour
        {
        }
    }
}

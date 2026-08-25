using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.AutoTest
{
    public enum AutoTestOperationState
    {
        Queued,
        Running,
        Succeeded,
        Failed,
        Cancelled
    }

    public sealed class AutoTestOperationInfo
    {
        internal AutoTestOperationInfo(long id, string name, Func<AutoTestOperationContext, IEnumerator> routineFactory)
        {
            Id = id;
            Name = name;
            State = AutoTestOperationState.Queued;
            CreatedTimeUtc = DateTime.UtcNow;
            RoutineFactory = routineFactory;
        }

        public long Id { get; }
        public string Name { get; }
        public AutoTestOperationState State { get; internal set; }
        public DateTime CreatedTimeUtc { get; }
        public DateTime? StartedTimeUtc { get; internal set; }
        public DateTime? CompletedTimeUtc { get; internal set; }
        public double DurationMilliseconds { get; internal set; }
        public string Output { get; internal set; } = string.Empty;
        public string Error { get; internal set; } = string.Empty;

        internal Func<AutoTestOperationContext, IEnumerator> RoutineFactory { get; }
        internal bool CancellationRequested { get; set; }
    }

    [Serializable]
    public sealed class AutoTestUISelector
    {
        public string name;
        public string path;
        public string indexedPath;
        public string text;
        public string action;
    }

    [Serializable]
    public sealed class AutoTestUIQuery
    {
        public AutoTestUISelector selector = new AutoTestUISelector();
        public int sampleGrid = 5;
        public int textLimit = 512;
        public int maxResults;
        public long changedSince;
        public bool compact;
    }

    [Serializable]
    public sealed class AutoTestUIActionRequest
    {
        public string pointerAction;
        public AutoTestUISelector selector = new AutoTestUISelector();
        public string button = "left";
        public float scrollX;
        public float scrollY;
        public int sampleGrid = 5;
        public int stableFrames = 2;
        public float timeoutSeconds = 10f;
    }

    [Serializable]
    public sealed class AutoTestUIWaitRequest
    {
        public string state = "visible";
        public bool interactable;
        public AutoTestUISelector selector = new AutoTestUISelector();
        public int sampleGrid = 5;
        public int stableFrames = 2;
        public float timeoutSeconds = 30f;
    }

    [Serializable]
    public sealed class AutoTestInputSequence
    {
        public List<AutoTestInputStep> steps = new List<AutoTestInputStep>();
        public bool resetAfter;
    }

    [Serializable]
    public sealed class AutoTestInputStep
    {
        public string type;
        public string action;
        public string button;
        public string key;
        public string text;
        public float x;
        public float y;
        public float deltaX;
        public float deltaY;
        public float scrollX;
        public float scrollY;
        public int touchId;
        public float pressure;
        public int tapCount;
        public int frames;
        public List<AutoTestTouchPoint> touches;
    }

    [Serializable]
    public sealed class AutoTestTouchPoint
    {
        public int id;
        public string phase;
        public float x;
        public float y;
        public float deltaX;
        public float deltaY;
        public float pressure;
        public float radiusX;
        public float radiusY;
        public int tapCount;
    }

    [Serializable]
    public sealed class AutoTestLogQuery
    {
        public long since;
        public string level = "all";
        public string contains;
        public int limit = 200;
        public int messageLimit = 2048;
        public int stackLimit = 4096;
        public bool includeStack;
        public bool compact;
    }

    [Serializable]
    public sealed class AutoTestGameObjectWaitRequest
    {
        public string state = "exists";
        public AutoTestGameObjectQuery query = new AutoTestGameObjectQuery();
        public int stableFrames = 2;
        public float positionEpsilon = 0.001f;
        public float rotationEpsilon = 0.1f;
        public float timeoutSeconds = 30f;
        public bool compact;
    }

    [Serializable]
    public sealed class AutoTestLogWaitRequest
    {
        public long since = -1;
        public string level = "all";
        public string contains;
        public int count = 1;
        public int messageLimit = 2048;
        public int stackLimit = 4096;
        public bool includeStack;
        public float timeoutSeconds = 30f;
        public bool compact;
    }

    [Serializable]
    public sealed class AutoTestWaitRequest
    {
        public float realSeconds;
        public float gameSeconds;
        public int frames;
        public bool compact;
    }

    [Serializable]
    public sealed class AutoTestScreenshotRequest
    {
        public string path;
        public int width;
        public int height;
        public RectInt region;
        public bool uiOnly;
        public float timeoutSeconds = 10f;
    }

    internal sealed class AutoTestOperationContext
    {
        private readonly AutoTestOperationInfo m_Operation;

        public AutoTestOperationContext(AutoTestOperationInfo operation)
        {
            m_Operation = operation;
        }

        public bool IsCancellationRequested => m_Operation.CancellationRequested;

        public void SetOutput(string output)
        {
            m_Operation.Output = output ?? string.Empty;
        }
    }

    public static partial class AutoTestPipeline
    {
        private const int OperationHistoryLimit = 100;
        private static readonly Queue<AutoTestOperationInfo> s_PendingOperations = new Queue<AutoTestOperationInfo>();
        private static readonly List<AutoTestOperationInfo> s_Operations = new List<AutoTestOperationInfo>();
        private static long s_NextOperationId = 1;
        private static AutoTestPipelineRunner s_Runner;
        private static Coroutine s_OperationCoroutine;
        private static bool s_IsRunnerActive;
        private static Action s_EditorStopHandler;

        public static IReadOnlyList<AutoTestOperationInfo> Operations => s_Operations;

        public static void SetEditorStopHandler(Action handler)
        {
            s_EditorStopHandler = handler;
        }

        public static void StopRunning()
        {
            if (Application.isEditor)
            {
                if (s_EditorStopHandler == null)
                    throw new InvalidOperationException("AutoTest Editor stop handler is not registered.");
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
                UnityEngine.Object.Destroy(s_Runner.gameObject);
            }
            s_PendingOperations.Clear();
            s_Operations.Clear();
            s_NextOperationId = 1;
            s_Runner = null;
            s_OperationCoroutine = null;
            s_IsRunnerActive = false;
            AutoTestInput.ResetState();
            AutoTestUI.ResetSnapshots();
        }

        public static string ListUI(AutoTestUIQuery query)
        {
            return AutoTestUI.List(query ?? new AutoTestUIQuery());
        }

        public static string GetGameObjectState(AutoTestGameObjectQuery query)
        {
            return AutoTestGameObjects.GetState(query);
        }

        public static string ListGameObjects(AutoTestGameObjectListQuery query)
        {
            return AutoTestGameObjects.List(query ?? new AutoTestGameObjectListQuery());
        }

        public static string GetSceneState(bool compact = false)
        {
            return AutoTestState.GetScenes(compact);
        }

        public static string GetApplicationState(bool compact = false)
        {
            return AutoTestState.GetApplication(compact);
        }

        public static long ActOnUI(AutoTestUIActionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("ui-act", context => AutoTestUI.Act(request, context));
        }

        public static long WaitForUI(AutoTestUIWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait-for", context => AutoTestUI.Wait(request, context));
        }

        public static long WaitForGameObject(AutoTestGameObjectWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait-for", context => AutoTestGameObjects.Wait(request, context));
        }

        public static long WaitForLog(AutoTestLogWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait-for", context => AutoTestLogs.Wait(request, context));
        }

        public static long Wait(AutoTestWaitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("wait", context => AutoTestWait.Run(request, context));
        }

        public static long RunInput(AutoTestInputSequence sequence)
        {
            if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
                throw new ArgumentException("输入序列必须包含非空 steps 数组。", nameof(sequence));
            return EnqueueOperation("ui-input", context => AutoTestInput.Run(sequence, context));
        }

        public static string GetInputState(bool compact = false)
        {
            return AutoTestInput.GetState(compact);
        }

        public static string ResetInput(bool compact = false)
        {
            AutoTestInput.ResetInterruptedInput();
            return AutoTestInput.GetState(compact);
        }

        public static string GetLogs(AutoTestLogQuery query)
        {
            return AutoTestLogs.Query(query ?? new AutoTestLogQuery());
        }

        public static void InitializeLogCapture()
        {
            AutoTestLogs.InitializeCapture();
        }

        public static long CaptureScreenshot(AutoTestScreenshotRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return EnqueueOperation("screenshot", context => AutoTestCapture.Capture(request, context));
        }

        public static bool TryGetOperation(long operationId, out AutoTestOperationInfo operation)
        {
            operation = s_Operations.Find(item => item.Id == operationId);
            return operation != null;
        }

        public static bool CancelOperation(long operationId)
        {
            if (!TryGetOperation(operationId, out AutoTestOperationInfo operation) || IsCompleted(operation.State))
                return false;
            operation.CancellationRequested = true;
            if (operation.State == AutoTestOperationState.Queued)
                CompleteOperation(operation, AutoTestOperationState.Cancelled, "操作已取消。");
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

        internal static string SerializeOperation(AutoTestOperationInfo operation, bool prettyPrint = false)
        {
            return JsonUtility.ToJson(AutoTestOperationJson.From(operation), prettyPrint);
        }

        internal static string SerializeOperations(int limit, bool prettyPrint = false)
        {
            int count = Mathf.Clamp(limit, 1, OperationHistoryLimit);
            var output = new AutoTestOperationListJson();
            for (int i = s_Operations.Count - 1; i >= 0 && output.operations.Count < count; i--)
                output.operations.Add(AutoTestOperationJson.From(s_Operations[i]));
            output.count = output.operations.Count;
            return JsonUtility.ToJson(output, prettyPrint);
        }

        private static long EnqueueOperation(string name, Func<AutoTestOperationContext, IEnumerator> routineFactory)
        {
            var operation = new AutoTestOperationInfo(s_NextOperationId++, name, routineFactory);
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
            var gameObject = new GameObject("[XFramework AutoTest Pipeline]") {
                hideFlags = HideFlags.HideAndDontSave,
            };
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            s_Runner = gameObject.AddComponent<AutoTestPipelineRunner>();
        }

        private static IEnumerator RunOperations()
        {
            try
            {
                while (s_PendingOperations.Count > 0)
                {
                    AutoTestOperationInfo operation = s_PendingOperations.Dequeue();
                    if (operation.State == AutoTestOperationState.Cancelled)
                        continue;

                    operation.State = AutoTestOperationState.Running;
                    operation.StartedTimeUtc = DateTime.UtcNow;
                    var context = new AutoTestOperationContext(operation);
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
                                AutoTestInput.ResetInterruptedInput();
                            }
                            catch (Exception exception)
                            {
                                cancellationError += $" 输入重置失败：{exception}";
                            }
                        }
                        CompleteOperation(operation, AutoTestOperationState.Cancelled, cancellationError);
                    }
                    else if (failure != null)
                        CompleteOperation(operation, AutoTestOperationState.Failed, failure.ToString());
                    else
                        CompleteOperation(operation, AutoTestOperationState.Succeeded, string.Empty);
                }
            }
            finally
            {
                s_OperationCoroutine = null;
                s_IsRunnerActive = false;
            }
        }

        private static void CompleteOperation(AutoTestOperationInfo operation, AutoTestOperationState state, string error)
        {
            if (IsCompleted(operation.State))
                return;
            operation.State = state;
            operation.Error = error ?? string.Empty;
            operation.CompletedTimeUtc = DateTime.UtcNow;
            DateTime started = operation.StartedTimeUtc ?? operation.CreatedTimeUtc;
            operation.DurationMilliseconds = (operation.CompletedTimeUtc.Value - started).TotalMilliseconds;
            Debug.Log($"[AutoTest] {SerializeOperation(operation)}");
            TrimOperationHistory();
        }

        private static void TrimOperationHistory()
        {
            while (s_Operations.Count > OperationHistoryLimit && IsCompleted(s_Operations[0].State))
                s_Operations.RemoveAt(0);
        }

        private static bool IsCompleted(AutoTestOperationState state)
        {
            return state == AutoTestOperationState.Succeeded || state == AutoTestOperationState.Failed || state == AutoTestOperationState.Cancelled;
        }

        [Serializable]
        private sealed class AutoTestOperationJson
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

            public static AutoTestOperationJson From(AutoTestOperationInfo operation)
            {
                return new AutoTestOperationJson {
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
        private sealed class AutoTestOperationListJson
        {
            public int count;
            public List<AutoTestOperationJson> operations = new List<AutoTestOperationJson>();
        }

        private sealed class AutoTestPipelineRunner : MonoBehaviour
        {
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using XFramework.Command;

namespace XFramework.Editor
{
    [Serializable]
    internal sealed class XCommandCompileDiagnostic
    {
        public string assembly;
        public string file;
        public int line;
        public int column;
        public string message;
    }

    [Serializable]
    internal sealed class XCommandCompileOperationOutput
    {
        public bool succeeded;
        public int errorCount;
        public int warningCount;
        public bool errorsTruncated;
        public string errorSummary;
        public List<XCommandCompileDiagnostic> errors = new List<XCommandCompileDiagnostic>();
    }

    [Serializable]
    internal sealed class XCommandCompileOperationStorage
    {
        public XCommandPipeline.XCommandOperationJson operation;
        public bool requestSubmitted;
        public int errorCount;
        public int warningCount;
        public bool errorsTruncated;
        public List<XCommandCompileDiagnostic> errors = new List<XCommandCompileDiagnostic>();
    }

    [InitializeOnLoad]
    internal sealed class XCommandCompileOperation : IXCommandAsyncOperation
    {
        private const string STORAGE_KEY = "XFramework.XCommand.CompileOperation";
        private const string OPERATION_NAME = "recompile";
        private const double COMPILE_DELAY_SECONDS = 0.5d;
        private const int MAX_REPORTED_ERRORS = 100;

        private static readonly List<XCommandCompileDiagnostic> errors = new List<XCommandCompileDiagnostic>();
        private static XCommandCompileOperationStorage storage;
        private static int errorCount;
        private static int warningCount;
        private static bool requestSubmitted;

        static XCommandCompileOperation()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += Restore;
            EditorApplication.quitting += OnEditorQuitting;
            Restore();
            EditorApplication.delayCall += Restore;
        }

        internal XCommandCompileOperation()
        {
        }

        public string Name => OPERATION_NAME;
        public bool CanCancel => false;

        internal static string Start()
        {
            if (TryGetCurrentOperation(out XCommandOperationInfo current) && !IsCompleted(current.State))
            {
                return JsonUtility.ToJson(XCommandPipeline.XCommandOperationJson.From(current));
            }
            if (EditorApplication.isCompiling)
            {
                throw new InvalidOperationException("Unity 当前正在编译脚本，未提交新的重新编译请求。");
            }

            return XCommandPipeline.RunOperation(new XCommandCompileOperation());
        }

        public IEnumerator Run(XCommandOperationContext context)
        {
            InitializeStorage(context.Id);
            yield return CompileAfterDelay();
            yield return WaitForCompletion(context);
        }

        private static void InitializeStorage(long operationId)
        {
            if (!XCommandPipeline.TryGetOperation(operationId, out XCommandOperationInfo operation))
            {
                throw new InvalidOperationException($"找不到重编译操作：{operationId}");
            }

            errors.Clear();
            errorCount = 0;
            warningCount = 0;
            requestSubmitted = false;
            storage = new XCommandCompileOperationStorage
            {
                operation = XCommandPipeline.XCommandOperationJson.From(operation),
                requestSubmitted = false,
                errorCount = 0,
                warningCount = 0,
                errorsTruncated = false,
                errors = new List<XCommandCompileDiagnostic>(),
            };
            Save();
        }

        private static IEnumerator CompileAfterDelay()
        {
            double startTime = EditorApplication.timeSinceStartup;
            while (EditorApplication.timeSinceStartup - startTime < COMPILE_DELAY_SECONDS)
            {
                yield return null;
            }

            if (EditorApplication.isCompiling)
            {
                CompleteCompilation(true, "Unity 已开始其他编译，未重复提交重新编译请求。");
                yield break;
            }

            try
            {
                Debug.Log("[XCommand] 刷新 AssetDatabase 并请求重新编译脚本。");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                requestSubmitted = true;
                storage.requestSubmitted = true;
                Save();
                if (!EditorApplication.isCompiling)
                {
                    CompilationPipeline.RequestScriptCompilation();
                }
            }
            catch (Exception exception)
            {
                CompleteCompilation(true, $"提交重新编译请求失败：{exception.Message}");
            }
        }

        private static IEnumerator WaitForCompletion(XCommandOperationContext context)
        {
            while (true)
            {
                if (storage == null || storage.operation == null || !Enum.TryParse(storage.operation.state, true, out XCommandOperationState state))
                {
                    throw new InvalidOperationException("重编译 operation 在等待过程中丢失。");
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
                    throw new InvalidOperationException("重编译 operation 在等待过程中丢失。");
                }

                yield return null;
            }
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
                string operationJson = JsonUtility.ToJson(storage.operation);
                XCommandPipeline.RestoreOperation(operationJson, false);
            }

            return XCommandPipeline.TryGetOperation(storage.operation.id, out operation);
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            if (!requestSubmitted || messages == null || !TryGetCurrentOperation(out XCommandOperationInfo operation) || IsCompleted(operation.State))
            {
                return;
            }

            for (int i = 0; i < messages.Length; i++)
            {
                CompilerMessage message = messages[i];
                if (message.type == CompilerMessageType.Warning)
                {
                    warningCount++;
                    continue;
                }
                if (message.type != CompilerMessageType.Error)
                {
                    continue;
                }

                errorCount++;
                if (errors.Count < MAX_REPORTED_ERRORS)
                {
                    errors.Add(new XCommandCompileDiagnostic
                    {
                        assembly = assemblyPath ?? string.Empty,
                        file = message.file ?? string.Empty,
                        line = message.line,
                        column = message.column,
                        message = message.message ?? string.Empty,
                    });
                }
            }
        }

        private static void OnCompilationFinished(object context)
        {
            if (!requestSubmitted || !TryGetCurrentOperation(out XCommandOperationInfo operation) || IsCompleted(operation.State))
            {
                return;
            }

            CompleteCompilation(EditorUtility.scriptCompilationFailed || errorCount > 0, null);
        }

        private static void OnBeforeAssemblyReload()
        {
            if (!TryGetCurrentOperation(out XCommandOperationInfo operation) || IsCompleted(operation.State))
            {
                Save();
                return;
            }

            if (requestSubmitted)
            {
                CompleteCompilation(false, null);
            }
            else
            {
                CompleteCompilation(true, "重新编译请求尚未提交，Editor 发生了程序集重载。");
            }

            Save();
        }

        private static void CompleteCompilation(bool failed, string failureOverride)
        {
            if (storage == null || storage.operation == null || IsCompletedString(storage.operation.state))
            {
                return;
            }

            string errorMessage = failed ? string.IsNullOrEmpty(failureOverride) ? BuildErrorSummary() : failureOverride : string.Empty;
            XCommandCompileOperationOutput output = new XCommandCompileOperationOutput
            {
                succeeded = !failed,
                errorCount = errorCount,
                warningCount = warningCount,
                errorsTruncated = errorCount > errors.Count,
                errorSummary = errorMessage,
                errors = new List<XCommandCompileDiagnostic>(errors),
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

            storage.operation.state = failed ? XCommandOperationState.Failed.ToString() : XCommandOperationState.Succeeded.ToString();
            storage.operation.completedTimeUtc = completedTimeUtc.ToString("O");
            storage.operation.durationMilliseconds = (completedTimeUtc - startedTimeUtc).TotalMilliseconds;
            storage.operation.output = JsonUtility.ToJson(output);
            storage.operation.error = errorMessage;
            storage.requestSubmitted = false;
            storage.errorCount = errorCount;
            storage.warningCount = warningCount;
            storage.errorsTruncated = output.errorsTruncated;
            storage.errors = new List<XCommandCompileDiagnostic>(errors);
            requestSubmitted = false;
            Save();
        }

        private static string BuildErrorSummary()
        {
            if (errors.Count == 0)
            {
                return $"Unity 脚本编译失败，共 {errorCount} 个错误。";
            }

            XCommandCompileDiagnostic first = errors[0];
            return $"Unity 脚本编译失败，共 {errorCount} 个错误。首个错误：{first.file}({first.line},{first.column}): {first.message}";
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
                XCommandCompileOperationStorage restoredStorage = JsonUtility.FromJson<XCommandCompileOperationStorage>(json);
                if (restoredStorage == null || restoredStorage.operation == null || restoredStorage.operation.id <= 0)
                {
                    return;
                }

                storage = restoredStorage;
                storage.errors = storage.errors ?? new List<XCommandCompileDiagnostic>();
                requestSubmitted = storage.requestSubmitted;
                errorCount = storage.errorCount;
                warningCount = storage.warningCount;
                errors.Clear();
                errors.AddRange(storage.errors);
                XCommandPipeline.RestoreOperation(JsonUtility.ToJson(storage.operation), false);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[XCommand] 恢复重编译操作失败：{exception.Message}");
            }
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

        private static bool IsCompleted(XCommandOperationState state)
        {
            return state == XCommandOperationState.Succeeded || state == XCommandOperationState.Failed || state == XCommandOperationState.Cancelled;
        }

        private static bool IsCompletedString(string state)
        {
            return state == nameof(XCommandOperationState.Succeeded) || state == nameof(XCommandOperationState.Failed) || state == nameof(XCommandOperationState.Cancelled);
        }

        private static void OnEditorQuitting()
        {
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= Restore;
            EditorApplication.quitting -= OnEditorQuitting;
            SessionState.EraseString(STORAGE_KEY);
        }
    }
}

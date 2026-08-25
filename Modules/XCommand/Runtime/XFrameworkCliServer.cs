using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace XFramework.Command
{
    public enum XFrameworkCliProcessType
    {
        Editor,
        DevelopmentPlayer
    }

    public static class XFrameworkCliServer
    {
        private const int ProtocolVersion = 1;
        private const int MaxRequestBytes = 4 * 1024 * 1024;
        private const double DefaultOperationTimeoutSeconds = 60d;
        private static readonly object s_StateLock = new object();
        private static readonly ConcurrentQueue<PendingRequest> s_PendingRequests = new ConcurrentQueue<PendingRequest>();
        private static readonly ConcurrentQueue<Exception> s_BackgroundErrors = new ConcurrentQueue<Exception>();
        private static readonly List<PendingOperationResponse> s_PendingOperations = new List<PendingOperationResponse>();
        private static readonly JsonSerializerSettings s_JsonSettings = new JsonSerializerSettings {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        private static TcpListener s_Listener;
        private static Thread s_ListenerThread;
        private static volatile bool s_IsRunning;
        private static int s_Port;
        private static XFrameworkCliProcessType s_ProcessType;
        private static string s_ProjectPath;
        private static string s_ProcessStartTimeUtc;
        private static long s_ProcessStartTimeUtcTicks;
        private static string s_RegistrationPath;
        private static IXFrameworkCliOperationProvider s_OperationProvider;

        public static bool IsRunning => s_IsRunning;
        public static int Port => s_Port;

        public static void SetOperationProvider(IXFrameworkCliOperationProvider provider)
        {
            s_OperationProvider = provider;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOperationProvider()
        {
            s_OperationProvider = null;
        }

        public static void Start(XFrameworkCliProcessType processType, string projectPath)
        {
            lock (s_StateLock)
            {
                if (s_IsRunning)
                    return;

                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                try
                {
                    Process process = Process.GetCurrentProcess();
                    DateTime processStartTimeUtc = process.StartTime.ToUniversalTime();
                    s_Listener = listener;
                    s_Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    s_ProcessType = processType;
                    s_ProjectPath = Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    s_ProcessStartTimeUtc = processStartTimeUtc.ToString("O", CultureInfo.InvariantCulture);
                    s_ProcessStartTimeUtcTicks = processStartTimeUtc.Ticks;
                    s_IsRunning = true;
                    WriteRegistration();
                    s_ListenerThread = new Thread(ListenLoop) {
                        IsBackground = true,
                        Name = "XFramework CLI Server",
                    };
                    s_ListenerThread.Start(listener);
                }
                catch
                {
                    s_IsRunning = false;
                    s_Listener = null;
                    s_Port = 0;
                    listener.Stop();
                    throw;
                }
            }
        }

        public static void Stop()
        {
            TcpListener listener;
            Thread listenerThread;
            string registrationPath;
            lock (s_StateLock)
            {
                if (!s_IsRunning)
                    return;
                s_IsRunning = false;
                listener = s_Listener;
                listenerThread = s_ListenerThread;
                registrationPath = s_RegistrationPath;
                s_Listener = null;
                s_ListenerThread = null;
                s_RegistrationPath = null;
                s_Port = 0;
            }

            listener.Stop();
            if (listenerThread != null && listenerThread != Thread.CurrentThread)
                listenerThread.Join(1000);

            while (s_PendingRequests.TryDequeue(out PendingRequest request))
                CompleteRequest(request, CreateErrorResponse(request.Request?.requestId, 4, "ServerStopped", "XFramework CLI Server has stopped."));
            for (int i = 0; i < s_PendingOperations.Count; i++)
            {
                PendingRequest request = s_PendingOperations[i].Request;
                CompleteRequest(request, CreateErrorResponse(request.Request?.requestId, 4, "ServerStopped", "XFramework CLI Server has stopped."));
            }
            s_PendingOperations.Clear();

            if (!string.IsNullOrEmpty(registrationPath) && File.Exists(registrationPath))
            {
                try
                {
                    File.Delete(registrationPath);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        public static void Pump()
        {
            while (s_BackgroundErrors.TryDequeue(out Exception backgroundError))
                UnityEngine.Debug.LogException(backgroundError);

            while (s_PendingRequests.TryDequeue(out PendingRequest pendingRequest))
            {
                try
                {
                    ProcessRequest(pendingRequest);
                }
                catch (Exception exception)
                {
                    CompleteRequest(pendingRequest, CreateExceptionResponse(pendingRequest.Request?.requestId, 1, "Failed", exception));
                }
            }

            UpdatePendingOperations();
        }

        private static void ListenLoop(object state)
        {
            var listener = (TcpListener)state;
            while (s_IsRunning)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (SocketException exception)
                {
                    if (s_IsRunning)
                        s_BackgroundErrors.Enqueue(exception);
                }
                catch (ObjectDisposedException)
                {
                    if (s_IsRunning)
                        s_BackgroundErrors.Enqueue(new InvalidOperationException("XFramework CLI listener was disposed while running."));
                }
                catch (Exception exception)
                {
                    s_BackgroundErrors.Enqueue(exception);
                }
            }
        }

        private static void HandleClient(object state)
        {
            using (var client = (TcpClient)state)
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;
                NetworkStream stream = client.GetStream();
                PendingRequest pendingRequest = null;
                try
                {
                    string requestJson = ReadRequest(stream);
                    var request = JsonConvert.DeserializeObject<XFrameworkCliRequest>(requestJson, s_JsonSettings);
                    pendingRequest = new PendingRequest(request);
                    lock (s_StateLock)
                    {
                        if (s_IsRunning)
                            s_PendingRequests.Enqueue(pendingRequest);
                        else
                            CompleteRequest(pendingRequest, CreateErrorResponse(request?.requestId, 4, "ServerStopped", "XFramework CLI Server has stopped."));
                    }
                    pendingRequest.Completed.Wait();
                    WriteResponse(stream, pendingRequest.Response);
                }
                catch (Exception exception)
                {
                    if (pendingRequest == null)
                    {
                        try
                        {
                            WriteResponse(stream, CreateExceptionResponse(null, 4, "ProtocolError", exception));
                        }
                        catch (Exception writeException)
                        {
                            s_BackgroundErrors.Enqueue(writeException);
                        }
                    }
                }
                finally
                {
                    pendingRequest?.Completed.Dispose();
                }
            }
        }

        private static string ReadRequest(NetworkStream stream)
        {
            using (var buffer = new MemoryStream())
            {
                bool endedWithNewline = false;
                while (buffer.Length <= MaxRequestBytes)
                {
                    int value = stream.ReadByte();
                    if (value < 0)
                        break;
                    if (value == '\n')
                    {
                        endedWithNewline = true;
                        break;
                    }
                    if (value != '\r')
                        buffer.WriteByte((byte)value);
                }
                if (buffer.Length > MaxRequestBytes)
                    throw new InvalidDataException($"CLI request exceeds {MaxRequestBytes} bytes.");
                if (!endedWithNewline)
                    throw new InvalidDataException("CLI request must end with a newline.");
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        private static void WriteResponse(NetworkStream stream, XFrameworkCliResponse response)
        {
            string json = JsonConvert.SerializeObject(response, Formatting.None, s_JsonSettings) + "\n";
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private static void ProcessRequest(PendingRequest pendingRequest)
        {
            XFrameworkCliRequest request = pendingRequest.Request;
            if (request == null)
            {
                CompleteRequest(pendingRequest, CreateErrorResponse(null, 4, "ProtocolError", "Request JSON cannot be null."));
                return;
            }
            if (request.protocolVersion != ProtocolVersion)
            {
                CompleteRequest(pendingRequest, CreateErrorResponse(request.requestId, 4, "ProtocolError", $"Unsupported protocol version: {request.protocolVersion}."));
                return;
            }
            if (string.IsNullOrWhiteSpace(request.requestId))
            {
                CompleteRequest(pendingRequest, CreateErrorResponse(null, 4, "ProtocolError", "requestId cannot be empty."));
                return;
            }

            switch (request.action?.Trim().ToLowerInvariant())
            {
                case "ping":
                    CompleteRequest(pendingRequest, CreateSuccessResponse(request.requestId, "XFramework CLI Server is ready."));
                    break;
                case "commands":
                    CompleteRequest(pendingRequest, CreateCommandsResponse(request));
                    break;
                case "diagnostics":
                    CompleteRequest(pendingRequest, CreateDiagnosticsResponse(request.requestId));
                    break;
                case "execute":
                    ExecuteCommand(pendingRequest);
                    break;
                default:
                    CompleteRequest(pendingRequest, CreateErrorResponse(request.requestId, 4, "ProtocolError", $"Unsupported action: {request.action}"));
                    break;
            }
        }

        private static XFrameworkCliResponse CreateCommandsResponse(XFrameworkCliRequest request)
        {
            XFrameworkCliResponse response = CreateSuccessResponse(request.requestId, "Commands discovered.");
            response.commands = new List<XFrameworkCliCommandInfo>();
            IReadOnlyList<XCommandDescriptor> commands = XCommandRegistry.VisibleCommands;
            for (int i = 0; i < commands.Count; i++)
            {
                XCommandDescriptor command = commands[i];
                bool available = XCommandRegistry.CanExecute(command);
                if (!request.availableOnly || available)
                    response.commands.Add(CreateCommandInfo(command));
            }
            return response;
        }

        private static XFrameworkCliResponse CreateDiagnosticsResponse(string requestId)
        {
            XFrameworkCliResponse response = CreateSuccessResponse(requestId, "Diagnostics discovered.");
            response.diagnostics = new List<XFrameworkCliDiagnosticInfo>();
            IReadOnlyList<XCommandDiagnostic> diagnostics = XCommandRegistry.Diagnostics;
            for (int i = 0; i < diagnostics.Count; i++)
            {
                XCommandDiagnostic diagnostic = diagnostics[i];
                response.diagnostics.Add(new XFrameworkCliDiagnosticInfo {
                    command = diagnostic.Command,
                    message = diagnostic.Message,
                    declaringType = diagnostic.DeclaringTypeName,
                    method = diagnostic.MethodName,
                });
            }
            return response;
        }

        private static void ExecuteCommand(PendingRequest pendingRequest)
        {
            XFrameworkCliRequest request = pendingRequest.Request;
            if (string.IsNullOrWhiteSpace(request.commandLine))
            {
                CompleteRequest(pendingRequest, CreateErrorResponse(request.requestId, 2, "InvalidArguments", "commandLine cannot be empty."));
                return;
            }

            if (XCommandRegistry.TryGetCommand(request.commandLine, out XCommandDescriptor descriptor) && descriptor.RequireConfirmation && !request.confirm)
            {
                XFrameworkCliResponse confirmationResponse = CreateErrorResponse(request.requestId, 1, "ConfirmationRequired", $"Command {descriptor.Command} requires explicit confirmation.");
                confirmationResponse.command = CreateCommandInfo(descriptor);
                CompleteRequest(pendingRequest, confirmationResponse);
                return;
            }

            XCommandExecutionResult result = XCommandHub.Execute(request.commandLine, XCommandSource.Cli);
            XFrameworkCliResponse response = CreateExecutionResponse(request.requestId, result);
            IXFrameworkCliOperationProvider operationProvider = s_OperationProvider;
            if (!result.Succeeded || !request.waitForOperation || !TryGetOperation(operationProvider, result.Value, out XFrameworkCliOperationInfo operation))
            {
                CompleteRequest(pendingRequest, response);
                return;
            }

            if (IsOperationCompleted(operation.State))
            {
                ApplyOperation(response, operation);
                CompleteRequest(pendingRequest, response);
                return;
            }

            double timeoutSeconds = request.timeoutSeconds > 0d ? request.timeoutSeconds : DefaultOperationTimeoutSeconds;
            s_PendingOperations.Add(new PendingOperationResponse(pendingRequest, response, operationProvider, operation.Id, DateTime.UtcNow.AddSeconds(timeoutSeconds)));
        }

        private static void UpdatePendingOperations()
        {
            DateTime now = DateTime.UtcNow;
            for (int i = s_PendingOperations.Count - 1; i >= 0; i--)
            {
                PendingOperationResponse pending = s_PendingOperations[i];
                if (!pending.OperationProvider.TryGetOperation(pending.OperationId, out XFrameworkCliOperationInfo operation))
                {
                    pending.Response.exitCode = 1;
                    pending.Response.status = "Failed";
                    pending.Response.message = $"Operation no longer exists: {pending.OperationId}.";
                    CompleteRequest(pending.Request, pending.Response);
                    s_PendingOperations.RemoveAt(i);
                    continue;
                }

                if (IsOperationCompleted(operation.State))
                {
                    ApplyOperation(pending.Response, operation);
                    CompleteRequest(pending.Request, pending.Response);
                    s_PendingOperations.RemoveAt(i);
                    continue;
                }

                if (now >= pending.DeadlineUtc)
                {
                    pending.Response.exitCode = 124;
                    pending.Response.status = "Timeout";
                    pending.Response.message = $"Timed out waiting for operation {operation.Id}. The operation was not cancelled.";
                    pending.Response.operation = CreateOperationResponse(operation);
                    CompleteRequest(pending.Request, pending.Response);
                    s_PendingOperations.RemoveAt(i);
                }
            }
        }

        private static bool TryGetOperation(IXFrameworkCliOperationProvider provider, object value, out XFrameworkCliOperationInfo operation)
        {
            operation = null;
            if (provider == null || !provider.TryResolveOperation(value, out long operationId))
                return false;
            return provider.TryGetOperation(operationId, out operation);
        }

        private static void ApplyOperation(XFrameworkCliResponse response, XFrameworkCliOperationInfo operation)
        {
            response.operation = CreateOperationResponse(operation);
            switch (operation.State)
            {
                case XFrameworkCliOperationState.Succeeded:
                    response.exitCode = 0;
                    response.status = "Succeeded";
                    response.message = "Operation succeeded.";
                    break;
                case XFrameworkCliOperationState.Failed:
                    response.exitCode = 1;
                    response.status = "Failed";
                    response.message = operation.Error;
                    break;
                case XFrameworkCliOperationState.Cancelled:
                    response.exitCode = 1;
                    response.status = "Cancelled";
                    response.message = operation.Error;
                    break;
            }
        }

        private static bool IsOperationCompleted(XFrameworkCliOperationState state)
        {
            return state == XFrameworkCliOperationState.Succeeded || state == XFrameworkCliOperationState.Failed || state == XFrameworkCliOperationState.Cancelled;
        }

        private static XFrameworkCliResponse CreateExecutionResponse(string requestId, XCommandExecutionResult result)
        {
            return new XFrameworkCliResponse {
                protocolVersion = ProtocolVersion,
                requestId = requestId,
                exitCode = result.Succeeded ? 0 : 1,
                status = result.Status.ToString(),
                message = result.Message,
                durationMilliseconds = result.DurationMilliseconds,
                value = NormalizeValue(result.Value),
                command = result.Command == null ? null : CreateCommandInfo(result.Command),
                exception = result.Exception == null ? null : CreateExceptionInfo(result.Exception),
                server = CreateServerInfo(),
            };
        }

        private static object NormalizeValue(object value)
        {
            if (value == null || value is JToken)
                return value;
            if (value is string text)
            {
                string trimmed = text.Trim();
                if (!trimmed.StartsWith("{", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
                    return text;
                try
                {
                    return JToken.Parse(trimmed);
                }
                catch (JsonException)
                {
                    return text;
                }
            }
            if (value is UnityEngine.Object unityObject)
            {
                return new JObject {
                    ["type"] = unityObject.GetType().FullName,
                    ["name"] = unityObject == null ? string.Empty : unityObject.name,
                    ["instanceId"] = unityObject == null ? 0 : unityObject.GetInstanceID(),
                };
            }

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal || value is DateTime || value is Guid)
                return value;
            try
            {
                return JToken.FromObject(value, JsonSerializer.Create(s_JsonSettings));
            }
            catch (JsonException)
            {
                return value.ToString();
            }
        }

        private static XFrameworkCliResponse CreateSuccessResponse(string requestId, string message)
        {
            return new XFrameworkCliResponse {
                protocolVersion = ProtocolVersion,
                requestId = requestId,
                exitCode = 0,
                status = "Succeeded",
                message = message,
                server = CreateServerInfo(),
            };
        }

        private static XFrameworkCliResponse CreateErrorResponse(string requestId, int exitCode, string status, string message)
        {
            return new XFrameworkCliResponse {
                protocolVersion = ProtocolVersion,
                requestId = requestId,
                exitCode = exitCode,
                status = status,
                message = message,
            };
        }

        private static XFrameworkCliResponse CreateExceptionResponse(string requestId, int exitCode, string status, Exception exception)
        {
            XFrameworkCliResponse response = CreateErrorResponse(requestId, exitCode, status, exception.Message);
            response.exception = CreateExceptionInfo(exception);
            return response;
        }

        private static XFrameworkCliCommandInfo CreateCommandInfo(XCommandDescriptor command)
        {
            bool available = XCommandRegistry.CanExecute(command);
            return new XFrameworkCliCommandInfo {
                command = command.Command,
                name = command.Name,
                category = command.Category,
                description = command.Description,
                usage = command.Usage,
                order = command.Order,
                mode = command.Mode.ToString(),
                available = available,
                unavailableReason = available ? string.Empty : $"Command {command.Command} cannot execute while Unity is in {(Application.isPlaying ? "Runtime" : "Edit Mode")}.",
                hasArgument = command.HasArgument,
                requireConfirmation = command.RequireConfirmation,
                declaringType = command.DeclaringTypeName,
                method = command.MethodName,
                assembly = command.AssemblyName,
            };
        }

        private static XFrameworkCliOperationResponse CreateOperationResponse(XFrameworkCliOperationInfo operation)
        {
            return new XFrameworkCliOperationResponse {
                id = operation.Id,
                name = operation.Name,
                state = operation.State.ToString(),
                createdTimeUtc = operation.CreatedTimeUtc.ToString("O", CultureInfo.InvariantCulture),
                startedTimeUtc = operation.StartedTimeUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                completedTimeUtc = operation.CompletedTimeUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                durationMilliseconds = operation.DurationMilliseconds,
                output = NormalizeValue(operation.Output),
                error = operation.Error,
            };
        }

        private static XFrameworkCliExceptionInfo CreateExceptionInfo(Exception exception)
        {
            return new XFrameworkCliExceptionInfo {
                type = exception.GetType().FullName,
                message = exception.Message,
                stackTrace = exception.ToString(),
            };
        }

        private static XFrameworkCliServerInfo CreateServerInfo()
        {
            return new XFrameworkCliServerInfo {
                pid = Process.GetCurrentProcess().Id,
                port = s_Port,
                processStartTimeUtc = s_ProcessStartTimeUtc,
                processStartTimeUtcTicks = s_ProcessStartTimeUtcTicks,
                processType = s_ProcessType.ToString(),
                projectPath = s_ProjectPath,
                dataPath = Application.dataPath,
                isPlaying = Application.isPlaying,
            };
        }

        private static void CompleteRequest(PendingRequest pendingRequest, XFrameworkCliResponse response)
        {
            if (Interlocked.Exchange(ref pendingRequest.IsCompleted, 1) != 0)
                return;
            pendingRequest.Response = response;
            pendingRequest.Completed.Set();
        }

        private static void WriteRegistration()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XFrameworkCLI", "servers");
            Directory.CreateDirectory(directory);
            s_RegistrationPath = Path.Combine(directory, $"{Process.GetCurrentProcess().Id}-{s_Port}.json");
            string json = JsonConvert.SerializeObject(CreateServerInfo(), Formatting.Indented, s_JsonSettings);
            File.WriteAllText(s_RegistrationPath, json, new UTF8Encoding(false));
        }

        private sealed class PendingRequest
        {
            public PendingRequest(XFrameworkCliRequest request)
            {
                Request = request;
            }

            public readonly XFrameworkCliRequest Request;
            public readonly ManualResetEventSlim Completed = new ManualResetEventSlim(false);
            public XFrameworkCliResponse Response;
            public int IsCompleted;
        }

        private sealed class PendingOperationResponse
        {
            public PendingOperationResponse(PendingRequest request, XFrameworkCliResponse response, IXFrameworkCliOperationProvider operationProvider, long operationId, DateTime deadlineUtc)
            {
                Request = request;
                Response = response;
                OperationProvider = operationProvider;
                OperationId = operationId;
                DeadlineUtc = deadlineUtc;
            }

            public readonly PendingRequest Request;
            public readonly XFrameworkCliResponse Response;
            public readonly IXFrameworkCliOperationProvider OperationProvider;
            public readonly long OperationId;
            public readonly DateTime DeadlineUtc;
        }

        private sealed class XFrameworkCliRequest
        {
            public int protocolVersion;
            public string requestId;
            public string action;
            public string commandLine;
            public bool waitForOperation;
            public double timeoutSeconds;
            public bool confirm;
            public bool availableOnly;
        }

        private sealed class XFrameworkCliResponse
        {
            public int protocolVersion;
            public string requestId;
            public int exitCode;
            public string status;
            public string message;
            public double durationMilliseconds;
            public object value;
            public XFrameworkCliCommandInfo command;
            public XFrameworkCliExceptionInfo exception;
            public XFrameworkCliOperationResponse operation;
            public XFrameworkCliServerInfo server;
            public List<XFrameworkCliCommandInfo> commands;
            public List<XFrameworkCliDiagnosticInfo> diagnostics;
        }

        private sealed class XFrameworkCliServerInfo
        {
            public int pid;
            public int port;
            public string processStartTimeUtc;
            public long processStartTimeUtcTicks;
            public string processType;
            public string projectPath;
            public string dataPath;
            public bool isPlaying;
        }

        private sealed class XFrameworkCliCommandInfo
        {
            public string command;
            public string name;
            public string category;
            public string description;
            public string usage;
            public int order;
            public string mode;
            public bool available;
            public string unavailableReason;
            public bool hasArgument;
            public bool requireConfirmation;
            public string declaringType;
            public string method;
            public string assembly;
        }

        private sealed class XFrameworkCliDiagnosticInfo
        {
            public string command;
            public string message;
            public string declaringType;
            public string method;
        }

        private sealed class XFrameworkCliOperationResponse
        {
            public long id;
            public string name;
            public string state;
            public string createdTimeUtc;
            public string startedTimeUtc;
            public string completedTimeUtc;
            public double durationMilliseconds;
            public object output;
            public string error;
        }

        private sealed class XFrameworkCliExceptionInfo
        {
            public string type;
            public string message;
            public string stackTrace;
        }
    }
}

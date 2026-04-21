using System;
using System.Collections.Generic;
using System.Linq;

namespace XFramework.Fsm
{
    /// <summary>
    /// 现代单活跃状态机。
    /// </summary>
    public class Fsm<TContext> : IManagedFsm
    {
        private enum TransitionRequestType
        {
            ChangeState = 0,
            Stop = 1
        }

        private struct PendingTransitionRequest
        {
            public bool HasValue;
            public TransitionRequestType RequestType;
            public FsmState<TContext> TargetState;
            public object Payload;
            public bool StartRunning;

            public static PendingTransitionRequest Create(FsmState<TContext> targetState, object payload, bool startRunning, TransitionRequestType requestType)
            {
                return new PendingTransitionRequest
                {
                    HasValue = true,
                    RequestType = requestType,
                    TargetState = targetState,
                    Payload = payload,
                    StartRunning = startRunning
                };
            }
        }

        private readonly Dictionary<Type, FsmState<TContext>> m_States = new Dictionary<Type, FsmState<TContext>>();
        private readonly List<string> m_RegisteredStateNames = new List<string>();
        private readonly TContext m_Context;
        private readonly string m_DebugName;
        private readonly bool m_AutoStartOnFirstState;

        private FsmManager m_Manager;
        private string m_RegistrationKey;
        private FsmState<TContext> m_CurrentState;
        private FsmState<TContext> m_PreviousState;
        private FsmTransition m_LastTransition;
        private bool m_IsDisposed;
        private bool m_IsRunning;
        private bool m_IsTransitioning;
        private PendingTransitionRequest m_PendingTransitionRequest;

        internal Fsm(TContext context, string debugName = null, bool autoStartOnFirstState = false)
        {
            m_Context = context;
            m_DebugName = string.IsNullOrWhiteSpace(debugName) ? typeof(TContext).Name : debugName;
            m_AutoStartOnFirstState = autoStartOnFirstState;
        }

        public event Action<FsmTransition> StateChanging;
        public event Action<FsmTransition> StateChanged;
        public event Action<FsmTransition> StateStopped;

        public string DebugName => m_DebugName;
        public Type ContextType => typeof(TContext);
        public bool IsRunning => m_IsRunning;
        public bool IsDisposed => m_IsDisposed;
        public FsmState<TContext> CurrentState => m_CurrentState;
        public FsmState<TContext> PreviousState => m_PreviousState;
        public string CurrentStateName => m_CurrentState != null ? m_CurrentState.StateName : string.Empty;
        public string PreviousStateName => m_PreviousState != null ? m_PreviousState.StateName : string.Empty;
        public IReadOnlyList<string> RegisteredStateNames => m_RegisteredStateNames;
        public FsmTransition LastTransition => m_LastTransition;

        public TState AddState<TState>() where TState : FsmState<TContext>, new()
        {
            return (TState)AddState(new TState());
        }

        public FsmState<TContext> AddState(FsmState<TContext> state)
        {
            EnsureNotDisposed();

            if (state == null)
            {
                throw new XFrameworkException("[FSM] state can not be null");
            }

            Type stateType = state.GetType();
            if (m_States.ContainsKey(stateType))
            {
                throw new XFrameworkException($"[FSM] duplicate state: {stateType.Name}");
            }

            state.Bind(this, m_Context);
            m_States.Add(stateType, state);
            m_RegisteredStateNames.Add(state.StateName);

            if (m_AutoStartOnFirstState && !m_IsRunning && m_States.Count == 1)
            {
                RequestTransition(state, null, true, TransitionRequestType.ChangeState);
            }

            return state;
        }

        public void Start<TState>(object payload = null) where TState : FsmState<TContext>
        {
            EnsureNotDisposed();
            RequestTransition(GetState<TState>(), payload, true, TransitionRequestType.ChangeState);
        }

        public void ChangeState<TState>(object payload = null) where TState : FsmState<TContext>
        {
            EnsureNotDisposed();
            RequestTransition(GetState<TState>(), payload, true, TransitionRequestType.ChangeState);
        }

        public void Stop(object payload = null)
        {
            EnsureNotDisposed();
            RequestTransition(null, payload, false, TransitionRequestType.Stop);
        }

        public void Update()
        {
            EnsureNotDisposed();

            if (!m_IsRunning || m_CurrentState == null)
            {
                return;
            }

            m_CurrentState.OnUpdate();
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            if (m_CurrentState != null)
            {
                FsmTransition transition = CreateTransition(m_CurrentState, null, null);
                StateChanging?.Invoke(transition);
                m_CurrentState.OnExit(transition);
                m_PreviousState = m_CurrentState;
                m_CurrentState = null;
                m_LastTransition = transition;
                m_IsRunning = false;
                StateStopped?.Invoke(transition);
            }

            foreach (FsmState<TContext> state in m_States.Values.ToList())
            {
                state.OnDispose();
            }

            m_States.Clear();
            m_RegisteredStateNames.Clear();
            m_PendingTransitionRequest = default;
            m_IsDisposed = true;

            FsmManager manager = m_Manager;
            string registrationKey = m_RegistrationKey;
            m_Manager = null;
            m_RegistrationKey = null;

            manager?.Unregister(registrationKey);

            StateChanging = null;
            StateChanged = null;
            StateStopped = null;
        }

        private void BindManagerInternal(FsmManager manager, string registrationKey)
        {
            m_Manager = manager;
            m_RegistrationKey = registrationKey;
        }

        internal void BindManager(FsmManager manager, string registrationKey)
        {
            BindManagerInternal(manager, registrationKey);
        }

        private TState GetState<TState>() where TState : FsmState<TContext>
        {
            Type type = typeof(TState);
            if (!m_States.TryGetValue(type, out FsmState<TContext> state))
            {
                throw new XFrameworkException($"[FSM] state not registered: {type.Name}");
            }

            return (TState)state;
        }

        private void RequestTransition(FsmState<TContext> targetState, object payload, bool startRunning, TransitionRequestType requestType)
        {
            if (requestType == TransitionRequestType.ChangeState && targetState == null)
            {
                throw new XFrameworkException("[FSM] target state can not be null");
            }

            if (m_IsTransitioning)
            {
                m_PendingTransitionRequest = PendingTransitionRequest.Create(targetState, payload, startRunning, requestType);
                return;
            }

            ExecuteTransitionRequests(PendingTransitionRequest.Create(targetState, payload, startRunning, requestType));
        }

        private void ExecuteTransitionRequests(PendingTransitionRequest request)
        {
            m_IsTransitioning = true;
            try
            {
                PendingTransitionRequest currentRequest = request;
                while (currentRequest.HasValue)
                {
                    ExecuteSingleTransition(currentRequest);
                    currentRequest = m_PendingTransitionRequest;
                    m_PendingTransitionRequest = default;

                    while (currentRequest.HasValue && ShouldSkipRequest(currentRequest))
                    {
                        currentRequest = default;
                    }
                }
            }
            finally
            {
                m_IsTransitioning = false;
            }
        }

        private void ExecuteSingleTransition(PendingTransitionRequest request)
        {
            if (request.RequestType == TransitionRequestType.Stop)
            {
                if (!m_IsRunning || m_CurrentState == null)
                {
                    return;
                }

                FsmTransition stopTransition = CreateTransition(m_CurrentState, null, request.Payload);
                StateChanging?.Invoke(stopTransition);
                m_CurrentState.OnExit(stopTransition);
                m_PreviousState = m_CurrentState;
                m_CurrentState = null;
                m_LastTransition = stopTransition;
                m_IsRunning = false;
                StateStopped?.Invoke(stopTransition);
                return;
            }

            FsmState<TContext> newState = request.TargetState;
            if (newState == null)
            {
                throw new XFrameworkException("[FSM] target state can not be null");
            }

            if (ReferenceEquals(m_CurrentState, newState))
            {
                return;
            }

            FsmTransition transition = CreateTransition(m_CurrentState, newState, request.Payload);
            StateChanging?.Invoke(transition);

            m_CurrentState?.OnExit(transition);

            m_PreviousState = m_CurrentState;
            m_CurrentState = newState;
            m_LastTransition = transition;
            m_IsRunning = request.StartRunning;

            m_CurrentState.OnEnter(transition);
            StateChanged?.Invoke(transition);
        }

        private bool ShouldSkipRequest(PendingTransitionRequest request)
        {
            if (!request.HasValue)
            {
                return true;
            }

            if (request.RequestType == TransitionRequestType.Stop)
            {
                return !m_IsRunning || m_CurrentState == null;
            }

            return ReferenceEquals(m_CurrentState, request.TargetState);
        }

        private FsmTransition CreateTransition(FsmState<TContext> fromState, FsmState<TContext> toState, object payload)
        {
            return new FsmTransition(fromState != null ? fromState.StateName : string.Empty,
                toState != null ? toState.StateName : string.Empty,
                payload);
        }

        private void EnsureNotDisposed()
        {
            if (m_IsDisposed)
            {
                throw new XFrameworkException($"[FSM] {DebugName} has been disposed");
            }
        }
    }
}

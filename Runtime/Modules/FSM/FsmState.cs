namespace XFramework.Fsm
{
    /// <summary>
    /// 带上下文的状态基类。
    /// </summary>
    public abstract class FsmState<TContext>
    {
        private bool m_IsInitialized;

        protected TContext Context { get; private set; }
        protected Fsm<TContext> Fsm { get; private set; }

        public virtual string StateName => GetType().Name;

        internal void Bind(Fsm<TContext> fsm, TContext context)
        {
            if (Fsm != null && !object.ReferenceEquals(Fsm, fsm))
            {
                throw new XFrameworkException($"[FSM] state {StateName} can not bind to multiple fsm instances");
            }

            Fsm = fsm;
            Context = context;

            if (!m_IsInitialized)
            {
                OnInit();
                m_IsInitialized = true;
            }
        }

        public virtual void OnInit() { }
        public virtual void OnEnter(FsmTransition transition) { }
        public virtual void OnUpdate() { }
        public virtual void OnExit(FsmTransition transition) { }
        public virtual void OnDispose() { }
    }
}

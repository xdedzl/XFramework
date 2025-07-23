namespace XFramework.Fsm
{
    /// <summary>
    /// 状态基类
    /// </summary>
    public abstract class FsmState
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void OnInit() { }

        /// <summary>
        /// 进入该状态
        /// </summary>
        /// <param name="parms">启动参数</param>
        public virtual void OnEnter(params object[] parms) { }

        /// <summary>
        /// 每帧运行
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// 离开该状态
        /// </summary>
        public virtual void OnExit() { }
    }

    public abstract class FsmStateTowLevel : FsmState
    {
        protected int currentSubState { get; private set; } = -1;
        protected void ChangeSubState(int state)
        {
            if(currentSubState == state)
            {
                return;
            }
            OnSubStateExit(currentSubState);
            currentSubState = state;
            OnSubStateEnter(currentSubState);
        }

        protected virtual void OnSubStateExit(int state)
        {

        }

        protected virtual void OnSubStateEnter(int state)
        {

        }
    }
}
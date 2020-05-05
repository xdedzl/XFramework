using XFramework.Fsm;

namespace XFramework
{
    /// <summary>
    /// 流程基类
    /// </summary>
    public abstract class ProcedureBase : FsmState 
    {
        public virtual void OnEnter(ProcedureBase preProcedure)
        {

        }
    }
}
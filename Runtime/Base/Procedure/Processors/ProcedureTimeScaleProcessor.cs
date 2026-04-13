using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程时间缩放处理器
    /// </summary>
    public class ProcedureTimeScaleProcessor : IProcedureProcessor
    {
        public void OnRefreshProcedureState(ProcedureBase procedure, ProcedureAttributeContext subContext, ProcedureAttributeContext parentContext)
        {
            var timeScaleAttr = subContext?.TimeScaleAttr ?? parentContext?.TimeScaleAttr;

            if (timeScaleAttr != null)
            {
                Time.timeScale = timeScaleAttr.TimeScale;
            }
        }
    }
}

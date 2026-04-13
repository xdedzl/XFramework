using System;

namespace XFramework
{
    /// <summary>
    /// 流程时间缩放特性，用于在进入流程时自动设置 Time.timeScale。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ProcedureTimeScaleAttribute : Attribute
    {
        public float TimeScale { get; private set; }

        public ProcedureTimeScaleAttribute(float timeScale)
        {
            TimeScale = timeScale;
        }
    }
}

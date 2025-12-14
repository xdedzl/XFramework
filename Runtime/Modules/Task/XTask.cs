using UnityEngine;

namespace XFramework.Tasks
{
    public abstract partial class XTask
    {
        public virtual bool IsDone { get; protected set; }
        public XTask Next { get; set; }
        public virtual void Update() { }
        
        public static XTask Delay(float time)
        {
            return new TimeTask(time);
        }
        
        /// <summary>
        /// 任务开始
        /// </summary>
        /// <param name="task">任务</param>
        public static void Start(XTask task)
        {
            TaskManager.Instance.StartTask(task);
        }

        /// <summary>
        /// 任务终止
        /// </summary>
        /// <param name="task">任务</param>
        public static void Stop(XTask task)
        {
            TaskManager.Instance.StopTask(task);
        }
    }
}

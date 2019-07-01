using System.Collections.Generic;

namespace XFramework.Tasks
{
    /// <summary>
    /// 任务管理器
    /// 每帧会产生40B的GC
    /// </summary>
    public class TaskManager : IGameModule
    {
        private List<ITask> m_Tasks;
        List<int> m_ToRemove;

        public TaskManager()
        {
            m_Tasks = new List<ITask>();
            m_ToRemove = new List<int>();
        }

        public void StartTask(ITask task)
        {
            m_Tasks.Add(task);
        }

        #region 接口实现

        public int Priority => 2000;

        public void Update(float elapseSeconds, float realElapseSeconds)
        {

            m_ToRemove.Clear();
            for (int i = 0; i < m_Tasks.Count; i++)
            {
                m_Tasks[i].Update();
                if (m_Tasks[i].IsDone)
                {
                    if (m_Tasks[i].Next != null)
                    {
                        m_Tasks[i] = m_Tasks[i].Next;
                    }
                    else
                    {
                        m_ToRemove.Add(i);
                    }
                }
            }

            foreach (var item in m_ToRemove)
            {
                m_Tasks.RemoveAt(item);
            }
        }

        public void Shutdown()
        {
            m_Tasks.Clear();
            m_ToRemove.Clear();
        }

        #endregion
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum TaskState
    {
        /// <summary>
        /// 等待中
        /// </summary>
        Waiting,
        /// <summary>
        /// 运行中
        /// </summary>
        Running,
        /// <summary>
        /// 已完成
        /// </summary>
        Completed,
        /// <summary>
        /// 已失败
        /// </summary>
        Failed,
        /// <summary>
        /// 已取消
        /// </summary>
        Canceled,
    }
}
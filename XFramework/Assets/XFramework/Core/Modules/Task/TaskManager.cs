using System.Collections.Generic;

namespace XFramework.Tasks
{
    public class TaskManager : IGameModule
    {
        private LinkedList<ITask> m_Tasks;

        public TaskManager()
        {
            m_Tasks = new LinkedList<ITask>();
        }

        public void StartTask(ITask task)
        {
            m_Tasks.AddLast(task);
        }

        #region 接口实现

        public int Priority => 2000;

        public void Shutdown()
        {

        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            List<ITask> temp = new List<ITask>();

            foreach (var item in m_Tasks)
            {
                item.Update();
                if (item.IsDone)
                {
                    temp.Add(item);
                }
            }

            for (int i = 0; i < temp.Count; i++)
            {
                if(temp[i].Next != null)
                {
                    var task = m_Tasks.Find(temp[i]);
                    task.Value = task.Value.Next;
                }
                else
                {
                    m_Tasks.Remove(temp[i]);
                }
            }
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
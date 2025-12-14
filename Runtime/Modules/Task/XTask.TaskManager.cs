using System.Collections.Generic;

namespace XFramework.Tasks
{
    public abstract partial class XTask
    {
        /// <summary>
        /// 任务管理器
        /// 每帧会产生40B的GC
        /// </summary>
        internal class TaskManager : MonoSingleton<TaskManager>
        {
            private List<XTask> m_Tasks = new();
            private List<int> m_ToRemove = new();

            /// <summary>
            /// 开启一个任务
            /// </summary>
            /// <param name="task"></param>
            public void StartTask(XTask task)
            {
                m_Tasks.Add(task);
            }

            /// <summary>
            /// 终止一个任务
            /// </summary>
            /// <param name="task"></param>
            public void StopTask(XTask task)
            {
                m_Tasks.Remove(task);
            }

            public void Update()
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
        }
    }
}
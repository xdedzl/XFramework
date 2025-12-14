using System;
using UnityEngine;

namespace XFramework.Tasks
{
    /// <summary>
    /// 任务基类
    /// </summary>
    public abstract class TaskBase : XTask {}
    
    public abstract partial class XTask
    {
        /// <summary>
        /// 单个任务
        /// </summary>
        private class SingleTask : TaskBase
        {
            private Func<bool> action;

            public SingleTask(Func<bool> action)
            {
                this.action = action;
            }

            public override void Update()
            {
                if (action())
                {
                    IsDone = true;
                }
            }

            public static SingleTask Create(Func<bool> action)
            {
                return new SingleTask(action);
            }
        }
    }

    /// <summary>
    /// 任务组（组内任一任务完成就算完成）
    /// </summary>
    public class RaceTask : TaskBase
    {
        /// <summary>
        /// 任务组
        /// </summary>
        private XTask[] m_Tasks;

        public RaceTask(XTask[] tasks)
        {
            m_Tasks = tasks;
        }

        public override void Update()
        {
            if (!IsDone)
            {
                for (int i = 0; i < m_Tasks.Length; i++)
                {
                    m_Tasks[i].Update();
                    if (m_Tasks[i].IsDone)
                    {
                        if (m_Tasks[i].Next != null)
                        {
                            m_Tasks[i] = m_Tasks[i].Next;
                            continue;
                        }
                        else
                        {
                            IsDone = true;
                            return;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// 任务组（组内所有任务完成算完成）
    /// </summary>
    public class AllTask : TaskBase
    {
        /// <summary>
        /// 任务组
        /// </summary>
        private XTask[] m_Tasks;

        public AllTask(XTask[] tasks)
        {
            m_Tasks = tasks;
        }

        public override void Update()
        {
            if (!IsDone)
            {
                bool isDone = true;
                for (int i = 0; i < m_Tasks.Length; i++)
                {
                    if (!m_Tasks[i].IsDone)
                    {
                        m_Tasks[i].Update();
                        isDone = false;
                    }
                    else if(m_Tasks[i].Next != null)
                    {
                        m_Tasks[i] = m_Tasks[i].Next;
                        isDone = false;
                    }
                }
                IsDone = isDone;
            }
        }
    }

    public class TimeTask: TaskBase
    {
        private readonly float time;
        private float pastTime;

        public TimeTask(float time)
        {
            this.time = time;
        }

        public override void Update()
        {
            pastTime += Time.deltaTime;
            if (pastTime >= time)
            {
                IsDone = true;
            }
        }
    }
}
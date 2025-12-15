using System;
using System.Collections.Generic;
using System.Linq;
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
            private readonly Func<bool> predicate;

            public SingleTask(Func<bool> predicate)
            {
                this.predicate = predicate;
            }

            public override void Update()
            {
                if (predicate())
                {
                    IsDone = true;
                }
            }
        }
        
        /// <summary>
        /// 单个任务
        /// </summary>
        private class SingleTask<T> : SingleTask, ITask<T>
        {
            private readonly Func<T> getter;

            public SingleTask(Func<bool> predicate, Func<T> getter) : base(predicate)
            {
                this.getter = getter;
            }

            public T Result => getter();
        }
    }
    
    public class DynamicTask<T, TResult> : TaskBase, ITask<TResult> where T : ITask<TResult>
    {
        private T m_Task;
        private readonly Func<T> m_Creater;
        
        public DynamicTask(Func<T> creater)
        {
            m_Creater = creater;
        }

        public override void Update()
        {
            m_Task ??= m_Creater();
            m_Task.Update();
        }
        
        public override bool IsDone
        {
            get => m_Task != null && m_Task.IsDone;
        }

        public TResult Result
        {
            get
            {
                if (m_Task == null)
                    throw new Exception("Task not started yet.");
                return m_Task.Result;
            }
        }
    }
    
    public class DynamicProgressTask<T, TResult> : TaskBase, IProgressTask<TResult> where T : IProgressTask<TResult>
    {
        private T m_Task;
        private readonly Func<T> m_Creater;
        
        public DynamicProgressTask(Func<T> creater)
        {
            m_Creater = creater;
        }

        public override void Update()
        {
            m_Task ??= m_Creater();
            m_Task.Update();
        }
        
        public override bool IsDone
        {
            get => m_Task != null && m_Task.IsDone;
        }

        public float Progress
        {
            get
            {
                if (m_Task == null)
                    return 0f;
                return m_Task.Progress;
            }
        }

        public TResult Result
        {
            get
            {
                if (m_Task == null)
                    throw new Exception("Task not started yet.");
                return m_Task.Result;
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
        private readonly ITask[] m_Tasks;

        public RaceTask(ITask[] tasks)
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
        private readonly ITask[] m_Tasks;

        public AllTask(ITask[] tasks)
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
    
    public class ActionTask : TaskBase
    {
        private readonly Action action;

        public ActionTask(Action action)
        {
            this.action = action;
        }

        public override void Update()
        {
            if (IsDone)
            {
                action();
                IsDone = true;
            }
        }
    }
    
    public class ProgressTask : TaskBase, IProgressTask
    {
        private readonly IProgress progress;

        public ProgressTask(IProgress progress)
        {
            this.progress = progress;
        }

        public override bool IsDone => progress.IsDone;
        
        public float Progress => progress.Progress;
    }
    
    public class ProgressTask<T> : TaskBase, IProgressTask<T>
    {
        private readonly IProgress<T> progress;

        public ProgressTask(IProgress<T> progress)
        {
            this.progress = progress;
        }

        public override bool IsDone => progress.IsDone;
        
        public float Progress => progress.Progress;
        
        public T Result => progress.Result;
    }

    public class ProgressListTask<T> : TaskBase, IProgressTask<T>
    {
        private readonly IList<ITask> tasks = new List<ITask>();
        private readonly ITask<T> mainTask;
        private readonly float[] weights;
        
        public ProgressListTask(ITask task, ITask<T> mainTask, params float[] weights)
        {
            var cur = task;
            while (cur != null)
            {
                tasks.Add(cur);
                cur = cur.Next;
            }

            if (tasks.Count != weights.Length)
            {
                throw new Exception("Tasks count and weights count do not match.");
            }

            this.mainTask = mainTask;

            var sum = weights.Sum();
            this.weights = weights.Select(w => w / sum).ToArray();
        }

        public override bool IsDone
        {
            get
            {
                // 现在还有会直接返回IsDone的任务，先遍历，后面改成判断最后一个即可
                for (int i = 0; i < tasks.Count; i++)
                {
                    if (!tasks[i].IsDone)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public T Result => mainTask.Result;
        
        public float Progress
        {
            get
            {
                float progress = 0f;
                for (int i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    var weight = weights[i];
                    if (task is IProgressTask progressTask)
                    {
                        progress += progressTask.Progress * weight; 
                    }
                    else
                    {
                        progress += (tasks[i].IsDone ? 1f : 0f) * weight;
                    }
                }

                return progress;
            }
        }
    }
}
using System;

namespace XFramework.Tasks
{
    /// <summary>
    /// 任务基类
    /// </summary>
    public abstract class BaseTask : ITask
    {
        /// <summary>
        /// 下一个任务
        /// </summary>
        public ITask Next { get; set; }
        /// <summary>
        /// 当前任务是否完成
        /// </summary>
        public virtual bool IsDone { get; set; }
        /// <summary>
        /// 任务完成前每帧执行
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public ITask All(params ITask[] tasks)
        {
            Next = new AllTask(tasks);
            return Next;
        }
        public ITask All(params Func<bool>[] funcs)
        {
            ITask[] tasks = new ITask[funcs.Length];
            for (int i = 0; i < funcs.Length; i++)
            {
                tasks[i] = new SingleTask(funcs[i]);
            }
            Next = new AllTask(tasks);
            return Next;
        }

        /// <summary>
        /// 创建一个任一任务执行完就会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public ITask Race(params ITask[] tasks)
        {
            Next = new RaceTask(tasks);
            return Next;
        }
        public ITask Race(params Func<bool>[] funcs)
        {
            ITask[] tasks = new ITask[funcs.Length];
            for (int i = 0; i < funcs.Length; i++)
            {
                tasks[i] = new SingleTask(funcs[i]);
            }
            Next = new RaceTask(tasks);
            return Next;
        }

        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public ITask Then(ITask task)
        {
            Next = task;
            return Next;
        }
        public ITask Then(Func<bool> func)
        {
            Next = new SingleTask(func);
            return Next;
        }
    }

    public class SingleTask : BaseTask
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
    }

    public class RaceTask : BaseTask
    {
        /// <summary>
        /// 任务组
        /// </summary>
        private ITask[] m_Tasks;

        public RaceTask(ITask[] tasks)
        {
            m_Tasks = tasks;
        }

        public override void Update()
        {
            if (!IsDone)
            {
                foreach (var item in m_Tasks)
                {
                    item.Update();
                    if (item.IsDone)
                    {
                        IsDone = true;
                        return;
                    }
                }
            }
        }
    }

    public class AllTask : BaseTask
    {
        /// <summary>
        /// 任务组
        /// </summary>
        private ITask[] m_Tasks;

        public AllTask(ITask[] tasks)
        {
            m_Tasks = tasks;
        }

        public override void Update()
        {
            if (!IsDone)
            {
                bool isDone = true;
                foreach (var item in m_Tasks)
                {
                    if (!item.IsDone)
                    {
                        item.Update();
                        isDone = false;
                    }
                }
                IsDone = isDone;
            }
        }
    }
}
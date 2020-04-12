using System;

namespace XFramework.Tasks
{
    public static class TaskExten
    {
        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks">任务组</param>
        /// <returns>由tasks构建的任务</returns>
        public static ITask All(this ITask task, params ITask[] tasks)
        {
            task.Next = new AllTask(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="funcs">任务组</param>
        /// <returns>由funcs组成的任务</returns>
        public static ITask All(this ITask task, params Func<bool>[] funcs)
        {
            ITask[] tasks = new ITask[funcs.Length];
            for (int i = 0; i < funcs.Length; i++)
            {
                tasks[i] = new SingleTask(funcs[i]);
            }
            task.Next = new AllTask(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个任一任务执行完就会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks">任务组</param>
        /// <returns>由tasks构建的任务</returns>
        public static ITask Race(this ITask task, params ITask[] tasks)
        {
            task.Next = new RaceTask(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个任一任务执行完就会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="funcs">任务组</param>
        /// <returns>由funcs构建的任务</returns>
        public static ITask Race(this ITask task, params Func<bool>[] funcs)
        {
            ITask[] tasks = new ITask[funcs.Length];
            for (int i = 0; i < funcs.Length; i++)
            {
                tasks[i] = new SingleTask(funcs[i]);
            }
            task.Next = new RaceTask(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        /// <param name="nextTask"></param>
        /// <returns>nextTask</returns>
        public static ITask Then(this ITask task, ITask nextTask)
        {
            task.Next = nextTask;
            return task.Next;
        }

        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        /// <param name="func"></param>
        /// <returns>nextTask</returns>
        public static ITask Then(this ITask task, Func<bool> func)
        {
            task.Next = new SingleTask(func);
            return task.Next;
        }

        /// <summary>
        /// 任务开始
        /// </summary>
        /// <param name="task">任务</param>
        public static void Start(this ITask task)
        {
            TaskManager.Instance.StartTask(task);
        }
    }
}
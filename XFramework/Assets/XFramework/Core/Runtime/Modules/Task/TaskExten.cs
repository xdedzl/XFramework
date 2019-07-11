using System;
using System.Collections;
using System.Collections.Generic;

namespace XFramework.Tasks
{
    public static class TaskExten
    {
        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public static ITask All(this ITask task, params ITask[] tasks)
        {
            task.Next = new AllTask(tasks);
            return task.Next;
        }
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
        /// <param name="tasks"></param>
        /// <returns></returns>
        public static ITask Race(this ITask task, params ITask[] tasks)
        {
            task.Next = new RaceTask(tasks);
            return task.Next;
        }
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
        /// <returns></returns>
        public static ITask Then(this ITask task, ITask nextTask)
        {
            task.Next = nextTask;
            return task.Next;
        }
        public static ITask Then(this ITask task, Func<bool> func)
        {
            task.Next = new SingleTask(func);
            return task.Next;
        }
    }
}
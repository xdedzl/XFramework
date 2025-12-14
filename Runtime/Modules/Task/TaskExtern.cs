using System;

namespace XFramework.Tasks
{
    public static class TaskExtern
    {
        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks">任务组</param>
        /// <returns>由tasks构建的任务</returns>
        public static XTask All(this XTask task, params XTask[] tasks)
        {
            task.Next = XTask.WhenAll(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="funcs">任务组</param>
        /// <returns>由funcs组成的任务</returns>
        public static XTask All(this XTask task, params Func<bool>[] predicates)
        {
            task.Next = XTask.WhenAll(predicates);
            return task.Next;
        }

        /// <summary>
        /// 创建一个任一任务执行完就会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks">任务组</param>
        /// <returns>由tasks构建的任务</returns>
        public static XTask Race(this XTask task, params XTask[] tasks)
        {
            task.Next = XTask.WhenAny(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个任一任务执行完就会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="funcs">任务组</param>
        /// <returns>由funcs构建的任务</returns>
        public static XTask Race(this XTask task, params Func<bool>[] predicates)
        {
            task.Next = XTask.WhenAny(predicates);
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个触发事件
        /// </summary>
        public static XTask ContinueWith(this XTask task, Action callback)
        {
            task.Next = XTask.WaitUntil(()=> { callback.Invoke(); return true; });
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static XTask ContinueWith(this XTask task, XTask nextTask)
        {
            task.Next = nextTask;
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static XTask ContinueWith(this XTask task, Func<bool> predicate)
        {
            task.Next = XTask.WaitUntil(predicate);
            return task.Next;
        }

        /// <summary>
        /// 任务开始
        /// </summary>
        /// <param name="task">任务</param>
        public static void Start(this XTask task)
        {
            XTask.Start(task);
        }

        /// <summary>
        /// 任务终止
        /// </summary>
        /// <param name="task">任务</param>
        public static void Stop(this XTask task)
        {
            XTask.Stop(task);
        }
    }
}
using System;
using UnityEngine;

namespace XFramework.Tasks
{
    public static class TaskExtern
    {
        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks">任务组</param>
        /// <returns>由tasks构建的任务</returns>
        public static ITask WhenAll(this ITask task, params ITask[] tasks)
        {
            task.Next = XTask.WhenAll(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个所有任务执行完才会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="funcs">任务组</param>
        /// <returns>由funcs组成的任务</returns>
        public static ITask WhenAll(this ITask task, params Func<bool>[] predicates)
        {
            task.Next = XTask.WhenAll(predicates);
            return task.Next;
        }

        /// <summary>
        /// 创建一个任一任务执行完就会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="tasks">任务组</param>
        /// <returns>由tasks构建的任务</returns>
        public static ITask WhenAny(this ITask task, params ITask[] tasks)
        {
            task.Next = XTask.WhenAny(tasks);
            return task.Next;
        }

        /// <summary>
        /// 创建一个任一任务执行完就会继续执行下一个任务的队列任务
        /// </summary>
        /// <param name="funcs">任务组</param>
        /// <returns>由funcs构建的任务</returns>
        public static ITask WhenAny(this ITask task, params Func<bool>[] predicates)
        {
            task.Next = XTask.WhenAny(predicates);
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个触发事件
        /// </summary>
        public static ITask ContinueWith(this ITask task, Action callback)
        {
            task.Next = XTask.WaitUntil(()=> { callback.Invoke(); return true; });
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个触发事件
        /// </summary>
        public static ITask ContinueWith<T>(this ITask<T> task, Action<T> callback)
        {
            task.Next = XTask.WaitUntil(()=> { callback.Invoke(task.Result); return true; });
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个触发事件
        /// </summary>
        public static ITask<TResult> ContinueWith<T, TResult>(this ITask<T> task, Func<T, TResult> getter)
        {
            var next = XTask.WaitUntil(() => true, () => getter(task.Result)); 
            task.Next = next;
            return next;
        }
        
        /// <summary>
        /// 创建一个触发事件
        /// </summary>
        public static IProgressTask<TResult> ContinueWithProgress<T, TResult>(this ITask<T> task , Func<T, TResult> getter)
        {
            var p = new DefaultProgress<TResult>(() => getter(task.Result));
            var next = XTask.WaitProgress(p); 
            task.Next = next;
            return next;
        }
        
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static ITask ContinueWith(this ITask task, ITask nextTask)
        {
            task.Next = nextTask;
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static ITask<T> ContinueWith<T>(this ITask task, ITask<T> nextTask)
        {
            task.Next = nextTask;
            return nextTask;
        }
        
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static ITask ContinueWith(this ITask task, Func<bool> predicate)
        {
            task.Next = XTask.WaitUntil(predicate);
            return task.Next;
        }
        
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static ITask<TResult> ContinueWith<T, TResult>(this ITask<T> task, Func<bool> predicate, Func<T, TResult> getter)
        {
            var next = XTask.WaitUntil(predicate, ()=>getter(task.Result)); 
            task.Next = next;
            return next;
        }
                
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static ITask<TResult> ContinueWithDynamic<T, TResult>(this ITask task, Func<T> creater) where T : ITask<TResult>
        {
            var next = new DynamicTask<T, TResult>(creater);
            task.ContinueWith(next);
            return next;
        }
        
        /// <summary>
        /// 创建一个后续任务
        /// </summary>
        public static IProgressTask<TResult> ContinueWithDynamicProgress<T, TResult>(this ITask task, Func<T> creater) where T : IProgressTask<TResult>
        {
            var next = new DynamicProgressTask<T,TResult>(creater);
            task.ContinueWith(next);
            return next;
        }

        /// <summary>
        /// 任务开始
        /// </summary>
        /// <param name="task">任务</param>
        public static ITask Start(this ITask task)
        {
            XTask.Start(task);
            return task;
        }

        /// <summary>
        /// 任务终止
        /// </summary>
        /// <param name="task">任务</param>
        public static void Stop(this ITask task)
        {
            XTask.Stop(task);
        }
    }
}
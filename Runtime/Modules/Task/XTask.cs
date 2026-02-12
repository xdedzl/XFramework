using System;
using UnityEngine;

namespace XFramework.Tasks
{
    public interface ITask
    {
        bool IsDone { get;}
        ITask Next { get; set; }
        
        void Update();
    }
    
    public interface ITask<out T> : ITask
    {
        T Result { get; }
    }
    
    public interface IProgressTask : ITask
    {
        float Progress { get; }
    }
    
    public interface IProgressTask<T> : ITask<T>
    {
        float Progress { get; }
    }
    
    public abstract partial class XTask : ITask
    {
        public virtual bool IsDone { get; protected set; }
        public ITask Next { get; set; }
        public virtual void Update() { }
        
        public static ITask Delay(float time)
        {
            return new TimeTask(time);
        }
        
        public static ITask WhenAll(params ITask[] tasks)
        {
            return new AllTask(tasks);
        }
        
        public static ITask WhenAll(params Func<bool>[] predicates)
        {
            var tasks = new ITask[predicates.Length];
            for (int i = 0; i < predicates.Length; i++)
            {
                tasks[i] = WaitUntil(predicates[i]);
            }
            return WhenAll(tasks);
        }

        public static ITask WhenAny(params ITask[] tasks)
        {
            return new RaceTask(tasks);
        }
        
        public static ITask WhenAny(params Func<bool>[] predicates)
        {
            var tasks = new ITask[predicates.Length];
            for (int i = 0; i < predicates.Length; i++)
            {
                tasks[i] = WaitUntil(predicates[i]);
            }
            return WhenAny(tasks);
        }
        
        public static ITask WaitUntil(Func<bool> predicate)
        {
            return new SingleTask(predicate);
        }
        
        public static ITask<T> WaitUntil<T>(Func<bool> predicate, Func<T> getter)
        {
            return new SingleTask<T>(predicate, getter);
        }

        public static ITask WaitWhile(Func<bool> predicate)
        {
            return new SingleTask(() => !predicate());
        }

        public static ITask WaitAction(Action action)
        {
            return new ActionTask(action);
        }

        public static IProgressTask WaitProgress(IProgress progress)
        {
            return new ProgressTask(progress);
        }
        
        public static IProgressTask<T> WaitProgress<T>(IProgress<T> progress)
        {
            return new ProgressTask<T>(progress);
        }

        public static ITask<TResult> WaitDynamic<T, TResult>(Func<T> creator) where T : ITask<TResult>
        {
            return new DynamicTask<T, TResult>(creator);
        }
        
        /// <summary>
        /// 任务开始
        /// </summary>
        /// <param name="task">任务</param>
        public static void Start(ITask task)
        {
            TaskManager.Instance.StartTask(task);
        }

        /// <summary>
        /// 任务终止
        /// </summary>
        /// <param name="task">任务</param>
        public static void Stop(ITask task)
        {
            TaskManager.Instance.StopTask(task);
        }
    }
}

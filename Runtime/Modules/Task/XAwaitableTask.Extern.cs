using System;

namespace XFramework.Tasks
{
    public partial class XAwaitableTask
    {
        public static XAwaitableTask Delay(float seconds)
        {
            return new XAwaitableTask(XTask.Delay(seconds).Start());
        }
        
        public static XAwaitableTask<T> Delay<T>(float seconds, T result)
        {
            return new XAwaitableTask<T>(XTask.Delay(seconds, result).Start());
        }
        
        public static XAwaitableTask WhenAll(params XAwaitableTask[] tasks)
        {
            return new XAwaitableTask(XTask.WaitUntil(() =>
            {
                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        return false;
                    }
                }
                return true;
            }).Start());
        }
        
        public static XAwaitableTask WhenAny(params XAwaitableTask[] tasks)
        {
            return new XAwaitableTask(XTask.WaitUntil(() =>
            {
                foreach (var task in tasks)
                {
                    if (task.IsCompleted)
                    {
                        return true;
                    }
                }
                return false;
            }).Start());
        }
    }


    public static class XAwaitableTaskExtern
    {
        public static XAwaitableTask ToXAwaitableTask(this ITask task)
        {
            return new XAwaitableTask(task);
        }
        
        public static async XAwaitableTask ContinueWith(this XAwaitableTask awaitableTask, Action continuation)
        {
            await awaitableTask;
            continuation();
        }
        
        public static async XAwaitableTask ContinueWith<T>(this XAwaitableTask<T> awaitableTask, Action<T> continuation)
        {
            continuation(await awaitableTask);
        }
    }
}
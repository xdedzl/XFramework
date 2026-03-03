using System;
using UnityEngine;
using UObject = UnityEngine.Object;

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
        
        public static XAwaitableTask<UObject> ToXAwaitableTask(this ResourceRequest request)
        {
            var task = new XAwaitableTask<UObject>();
            request.completed += _ =>
            {
                task.SetResult(request.asset);
            };
            return task;
        }
        
        public static XAwaitableTask<T> ToXAwaitableTask<T>(this ResourceRequest request) where T : UObject
        {
            var task = new XAwaitableTask<T>();
            request.completed += _ =>
            {
                task.SetResult(request.asset as T);
            };
            return task;
        }
        
        public static XAwaitableTask ToXAwaitableTask(this AsyncOperation asyncOperation)
        {
            var task = new XAwaitableTask();
            asyncOperation.completed += _ =>
            {
                task.SetResult();
            };
            return task;
        }
        
        public static XAwaitableTask<AssetBundle> ToXAwaitableTask(this AssetBundleCreateRequest request)
        {
            var task = new XAwaitableTask<AssetBundle>();
            request.completed += _ =>
            {
                task.SetResult(request.assetBundle);
            };
            return task;
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
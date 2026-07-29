using System;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 带 XScene 的主流程基类，派生类设置 XScene 资源路径。
    /// 流程切换时自动加载 XScene，Module 和 UI 会延迟到加载完成后再处理。
    /// </summary>
    public abstract class SceneProcedureBase : ProcedureBase
    {
        /// <summary>
        /// XScene 资源路径
        /// </summary>
        public abstract string XScenePath { get; }

        /// <summary>
        /// 异步加载 XScene，完成后调用 onReady 以触发 Module/UI 处理
        /// </summary>
        public override void OnPrepare(Action onReady)
        {
            PrepareXSceneAsync(onReady);
        }

        private async void PrepareXSceneAsync(Action onReady)
        {
            bool loaded = await XSceneManager.LoadSceneAsync(XScenePath);
            if (!loaded)
            {
                Debug.LogError($"[Procedure] Load XScene failed. procedure:{GetType().Name}, xScenePath:{XScenePath}.");
                return;
            }

            if (!ProcedureManager.IsValid || ProcedureManager.Instance.CurrentProcedure != this)
            {
                return;
            }

            onReady?.Invoke();
            OnXSceneLoaded();
        }

        /// <summary>
        /// XScene 加载完成后调用，此时 Module 和 UI 已经加载完毕
        /// </summary>
        public virtual void OnXSceneLoaded() { }
    }
}

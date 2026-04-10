using System;
using UnityEngine.SceneManagement;

namespace XFramework
{
    /// <summary>
    /// 带场景的流程基类，派生类设置场景路径。
    /// 流程切换时自动加载场景，Module和UI会延迟到场景加载完成后再处理。
    /// </summary>
    public abstract class SceneProcedureBase : ProcedureBase
    {
        /// <summary>
        /// 场景路径
        /// </summary>
        public abstract string ScenePath { get; }

        /// <summary>
        /// 场景加载模式，默认为 Single
        /// </summary>
        public virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Single;

        private ProcedureBase m_preProcedure;

        public override void OnEnter(ProcedureBase preProcedure)
        {
            base.OnEnter(preProcedure);
            m_preProcedure = preProcedure;
        }

        /// <summary>
        /// 异步加载场景，完成后调用 onReady 以触发 Module/UI 处理
        /// </summary>
        public override void OnPrepare(Action onReady)
        {
            // 如果上一个流程也是 SceneProcedureBase，并且场景路径相同，则不需要重新加载场景
            if (m_preProcedure is SceneProcedureBase preSceneProcedure && preSceneProcedure.ScenePath == this.ScenePath)
            {
                onReady?.Invoke();
                OnSceneLoaded();
                return;
            }

            var asyncOp = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode);
            asyncOp.completed += _ =>
            {
                onReady?.Invoke();
                OnSceneLoaded();
            };
        }

        /// <summary>
        /// 场景加载完成后调用，此时 Module 和 UI 已经加载完毕
        /// </summary>
        public virtual void OnSceneLoaded() { }
    }
}

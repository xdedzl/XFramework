// ==========================================
// 描述： 
// 作者： HAK
// 时间： 2018-10-22 08:45:01
// 版本： V 1.0
// ==========================================
using System;
using System.Collections;
using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// Mono生命周期事件
    /// 一些不继承Mono的类如果想在Mono生命周期做一些事，可以往这里添加
    /// </summary>
    public class MonoEvent : MonoSingleton<MonoEvent>
    {
        public event Action UPDATE;
        public event Action FIXEDUPDATE;
        public event Action ONGUI;
        public event Action LATEUPDATE;

        private void Update()
        {
            UPDATE?.Invoke();
        }

        private void FixedUpdate()
        {
            FIXEDUPDATE?.Invoke();
        }

        private void OnGUI()
        {
            ONGUI?.Invoke();
        }

        private void LateUpdate()
        {
            LATEUPDATE?.Invoke();
        }

        /// <summary>
        /// 延迟n帧调用
        /// </summary>
        /// <param name="action">事件</param>
        /// <param name="delayFrameCount">延迟帧数</param>
        public void DoInNextFrame(Action action, int delayFrameCount = 1)
        {
            StartCoroutine(Do(action, delayFrameCount));
        }

        private IEnumerator Do(Action action, int delayFrameCount = 1)
        {
            for (int i = 0; i < delayFrameCount; i++)
            {
                yield return new WaitForEndOfFrame();
            }
            action?.Invoke();
        }
    }
}

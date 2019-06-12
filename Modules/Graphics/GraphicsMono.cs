using System.Collections.Generic;
using UnityEngine;
using System;

namespace XFramework
{
    /// <summary>
    /// 用于在屏幕上绘制图形
    /// </summary>
    public class GraphicsMono : MonoBehaviour
    {
        /// <summary>
        /// 是否激活
        /// </summary>
        private bool m_IsActive;
        /// <summary>
        /// 绘制者集合
        /// </summary>
        private List<IDraw> m_Painters;
        /// <summary>
        /// 绘制方法
        /// </summary>
        private Action m_Action;

        private void Start()
        {
            m_Painters = new List<IDraw>();
        }

        private void OnPostRender()
        {
            if (m_IsActive)
            {
                foreach (var item in m_Painters)
                {
                    item.Draw();
                }

                m_Action?.Invoke();
            }
        }

        /// <summary>
        /// 添加一个绘制者
        /// </summary>
        /// <param name="paniter"></param>
        public void AddGraphics(IDraw paniter)
        {
            m_Painters.Add(paniter);
        }
        /// <summary>
        /// 添加一个绘制方法
        /// </summary>
        /// <param name="action"></param>
        public void AddGraphics(Action action)
        {
            m_Action += action;
        }

        /// <summary>
        /// 移除一个绘制者
        /// </summary>
        /// <param name="paniter"></param>
        public void RemoveGraphics(IDraw paniter)
        {
            m_Painters.Remove(paniter);
        }
        /// <summary>
        /// 移除一个绘制方法
        /// </summary>
        /// <param name="action"></param>
        public void RemoveGraphics(Action action)
        {
            m_Action -= action;
        }

        /// <summary>
        /// 清楚所有绘制
        /// </summary>
        public void Clear()
        {
            m_Painters.Clear();
            m_Action = null;
        }

        /// <summary>
        /// 设置状态
        /// </summary>
        /// <param name="isActive"></param>
        public void SetActive(bool isActive)
        {
            m_IsActive = isActive;
        }
    }
}
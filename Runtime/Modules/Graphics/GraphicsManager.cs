using System.Collections.Generic;
using UnityEngine;
using System;

namespace XFramework
{
    /// <summary>
    /// GL管理器
    /// </summary>
    public class GraphicsManager : IGameModule
    {
        /// <summary>
        /// 渲染列表
        /// </summary>
        private Dictionary<Camera, GraphicsMono> m_GraphicsDic;

        private Material m_InternalColoredMat;
        /// <summary>
        /// 内置shader，可用于画线
        /// </summary>
        public Material InternalColoredMat
        {
            get
            {
                m_InternalColoredMat = m_InternalColoredMat ?? new Material(Shader.Find("Hidden/Internal-Colored"));
                return m_InternalColoredMat;
            }
        }

        private Material m_InternalClearMat;
        /// <summary>
        /// 内置shader
        /// </summary>
        public Material InternalClearMat
        {
            get
            {
                m_InternalClearMat = m_InternalClearMat ?? new Material(Shader.Find("Hidden/InternalClear"));
                return m_InternalClearMat;
            }
        }

        private Material m_InternalErrorMat;
        /// <summary>
        /// 内置shader
        /// </summary>
        public Material InternalErrorMat
        {
            get
            {
                m_InternalErrorMat = m_InternalErrorMat ?? new Material(Shader.Find("Hidden/InternalErrorShader"));
                return m_InternalErrorMat;
            }
        }


        public GraphicsManager()
        {
            m_GraphicsDic = new Dictionary<Camera, GraphicsMono>();
        }

        /// <summary>
        /// 添加一个渲染任务
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="paniter"></param>
        public void AddGraphics(Camera camera, IDraw paniter)
        {
            if (m_GraphicsDic.ContainsKey(camera))
            {
                m_GraphicsDic[camera].AddGraphics(paniter);
            }
            else
            {
                GraphicsMono temp = camera.gameObject.AddComponent<GraphicsMono>();
                m_GraphicsDic.Add(camera, temp);
                temp.AddGraphics(paniter);
            }
        }
        /// <summary>
        /// 添加一个渲染任务
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="action"></param>
        public void AddGraphics(Camera camera, Action action)
        {
            if (m_GraphicsDic.ContainsKey(camera))
            {
                m_GraphicsDic[camera].AddGraphics(action);
            }
            else
            {
                GraphicsMono temp = camera.gameObject.AddComponent<GraphicsMono>();
                m_GraphicsDic.Add(camera, temp);
                temp.AddGraphics(action);
            }
        }

        /// <summary>
        /// 移除一个渲染任务
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="paniter"></param>
        public void RemoveGraphics(Camera camera, IDraw paniter)
        {
            if (m_GraphicsDic.ContainsKey(camera))
            {
                m_GraphicsDic[camera].RemoveGraphics(paniter);
            }
        }
        /// <summary>
        /// 移除一个渲染任务
        /// </summary>
        /// <param name="camera"></param>
        /// <param name="action"></param>
        public void RemoveGraphics(Camera camera, Action action)
        {
            if (m_GraphicsDic.ContainsKey(camera))
            {
                m_GraphicsDic[camera].RemoveGraphics(action);
            }
        }

        /// <summary>
        /// 打开一个渲染层
        /// </summary>
        /// <param name="camera"></param>
        public void OpenGraphics(Camera camera)
        {
            if (m_GraphicsDic.ContainsKey(camera) && !camera.gameObject.activeSelf)
            {
                m_GraphicsDic[camera].SetActive(true);
            }
        }

        /// <summary>
        /// 关闭一个渲染层
        /// </summary>
        /// <param name="camera"></param>
        public void CloseGraphics(Camera camera)
        {
            if (m_GraphicsDic.ContainsKey(camera) && camera.gameObject.activeSelf)
            {
                m_GraphicsDic[camera].SetActive(false);
            }
        }

        /// <summary>
        /// 清空一个渲染队列的所有渲染任务
        /// </summary>
        /// <param name="camera"></param>
        public void ClearGraphics(Camera camera)
        {
            if (m_GraphicsDic.ContainsKey(camera))
            {
                m_GraphicsDic[camera].Clear();
            }
        }


        #region 接口实现

        public int Priority { get { return 5000; } }

        public void Shutdown()
        {
            m_GraphicsDic.Clear();
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {

        }

        #endregion
    }
}
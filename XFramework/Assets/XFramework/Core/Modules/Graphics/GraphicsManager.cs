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

        /// <summary>
        /// 用于画线的材质
        /// </summary>
        public Material LineMaterial
        {
            get
            {
                if (m_LineMaterial == null)
                    m_LineMaterial = new Material(Shader.Find("RunTimeHandles/VertexColor"));
                return m_LineMaterial;
            }
        }
        private Material m_LineMaterial;

        /// <summary>
        /// 用于面片的材质
        /// </summary>
        public Material QuadeMaterial
        {
            get
            {
                if (m_QuadeMaterial == null)
                    m_QuadeMaterial = new Material(Shader.Find("RunTimeHandles/VertexColor"));
                return m_QuadeMaterial;
            }
        }
        private Material m_QuadeMaterial;

        /// <summary>
        /// 用于画三维物体的材质
        /// </summary>
        public Material ShapeMaterial
        {
            get
            {
                if (m_ShapeMaterial == null)
                    m_ShapeMaterial = new Material(Shader.Find("RunTimeHandles/Shape"));
                return m_ShapeMaterial;
            }
        }
        private Material m_ShapeMaterial;

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

        public int Priority { get { return 10; } }

        public void Shutdown()
        {
            m_GraphicsDic.Clear();
            m_LineMaterial = null;
            m_QuadeMaterial = null;
            m_ShapeMaterial = null;
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {

        }

        #endregion
    }
}
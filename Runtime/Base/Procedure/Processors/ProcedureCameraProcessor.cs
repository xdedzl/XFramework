using UnityEngine;

namespace XFramework
{
    /// <summary>
    /// 流程相机切换处理器
    /// </summary>
    public class ProcedureCameraProcessor : IProcedureProcessor
    {
        /// <summary>
        /// 当前激活的相机对象名称
        /// </summary>
        private string m_ActiveCameraName;
        private bool m_HasOverlaySnapshot;
        private string m_PreOverlayCameraName;

        internal string DebugActiveCameraName => m_ActiveCameraName;

        public void OnRefreshProcedureState(ProcedureRefreshContext context)
        {
            var overlayAttr = context.OverlayContext?.CameraAttr;
            var baseAttr = context.SubContext?.CameraAttr ?? context.ParentContext?.CameraAttr;
            if (overlayAttr != null && !m_HasOverlaySnapshot)
            {
                m_PreOverlayCameraName = m_ActiveCameraName;
                m_HasOverlaySnapshot = true;
            }

            var camAttr = overlayAttr ?? baseAttr;
            string targetCameraName = camAttr?.CameraName;
            if (camAttr == null && m_HasOverlaySnapshot)
            {
                targetCameraName = m_PreOverlayCameraName;
            }

            // 1. 如果都有相机且名称一致，则无需任何操作
            if (!string.IsNullOrEmpty(targetCameraName) && targetCameraName == m_ActiveCameraName)
            {
                if (overlayAttr == null)
                {
                    m_HasOverlaySnapshot = false;
                    m_PreOverlayCameraName = null;
                }
                return;
            }

            // 2. 关闭旧相机 (如果名称有效)
            if (!string.IsNullOrEmpty(m_ActiveCameraName))
            {
                GameObject oldGo = UObjectFinder.Find(m_ActiveCameraName);
                if (oldGo != null)
                {
                    ToggleCameraObject(oldGo, false);
                }
            }

            // 3. 加载新相机
            if (!string.IsNullOrEmpty(targetCameraName))
            {
                GameObject newGo = UObjectFinder.Find(targetCameraName);
                if (newGo != null)
                {
                    ToggleCameraObject(newGo, true);
                    m_ActiveCameraName = targetCameraName;
                    Debug.Log($"[ProcedureManager] Switch Camera to: {m_ActiveCameraName}");
                }
                else
                {
                    throw new XFrameworkException($"[ProcedureManager] Camera '{targetCameraName}' not found. Please check your procedure configuration.");
                }
            }
            else
            {
                // 4. 新流程没有相机配置，重置激活状态
                m_ActiveCameraName = null;
            }

            if (overlayAttr == null)
            {
                m_HasOverlaySnapshot = false;
                m_PreOverlayCameraName = null;
            }
        }

        private void ToggleCameraObject(GameObject go, bool active)
        {
            if (go == null) return;

            var behaviours = go.GetComponents<Behaviour>();
            bool componentFound = false;
            foreach (var b in behaviours)
            {
                if (b == null) continue;
                string typeName = b.GetType().Name;
                
                if (typeName.Contains("CinemachineCamera") || 
                    typeName.Contains("CinemachineVirtualCamera") || 
                    typeName.Contains("CinemachineFreeLook"))
                {
                    b.enabled = active;
                    componentFound = true;
                }
                else if (typeName == "Camera")
                {
                    b.enabled = active;
                    componentFound = true;
                }
            }

            if (!componentFound)
            {
                go.SetActive(active);
            }
        }
    }
}

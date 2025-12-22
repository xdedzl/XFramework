using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// 直接放在场景中Panel，用此类来注册到UIManager中，注册完立即销毁
    /// </summary>
    public class PanelRegister : MonoBehaviour
    {
        private void Start()
        {
            if (TryGetComponent<PanelBase>(out var panel))
            {
                UIManager.Instance.RegisterExistPanel(gameObject, panel.GetType());
                DestroyImmediate(this);
            }
        }
    }
}

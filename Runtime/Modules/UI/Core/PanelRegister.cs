using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XFramework.UI
{

    public class PanelRegister : MonoBehaviour
    {
        public string panelName;
        public bool openPanel = true;

        private void Start()
        {
            if (string.IsNullOrEmpty(panelName))
            {
                return;
            }

            if (TryGetComponent<PanelBase>(out var panel))
            {
                UIManager.Instance.RegisterExistPanel(panelName, panel, openPanel);
                DestroyImmediate(this);
            }
        }

    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XFramework.UI;

public class PanelRegister: MonoBehaviour
{
    private void Start()
    {
        var panel = GetComponent<PanelBase>();

        if(panel != null)
        {
            UIManager.Instance.RegisterExistPanel(panel);
        }
    }

}

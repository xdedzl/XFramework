using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework
{
    [CreateAssetMenu(fileName = "XFrameworkSetting", menuName = "XFramework/Setting")]
    public class XFrameworkSetting : ScriptableObject
    {
        public TMP_FontAsset font;
        public PanelSettings defaultUIToolkitPanelSettings;
        public bool UseABInEditor = false;
    }
}

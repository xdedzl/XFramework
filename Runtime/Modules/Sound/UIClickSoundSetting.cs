using System;
using UnityEngine;

namespace XFramework
{
    [Serializable]
    public class UIClickSoundSetting
    {
        [Tooltip("点击音效 Key，用于 XButton 下拉选择。")]
        public string key;
        [Tooltip("点击音效资源路径。")]
        [AssetPath(typeof(AudioClip))]
        public string path;
    }
}

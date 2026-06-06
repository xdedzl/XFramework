using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework
{
    [System.Serializable]
    public class UIClickSoundSetting
    {
        [Tooltip("点击音效 Key，用于 XButton 下拉选择。")]
        public string key;
        [Tooltip("点击音效资源路径。")]
        [AssetPath(typeof(AudioClip))]
        public string path;
    }

    [CreateAssetMenu(fileName = "XFrameworkSetting", menuName = "XFramework/Setting")]
    public class XFrameworkSetting : ScriptableObject
    {
        [Header("AB")]
        public bool UseABInEditor = false;

        [Header("UI")]
        public TMP_FontAsset font;
        public PanelSettings defaultUIToolkitPanelSettings;
        [Tooltip("UI 点击音效配置。")]
        public UIClickSoundSetting[] uiClickSounds;

        public static IEnumerable<string> GetUIClickSoundKeyOptions()
        {
            return XApplication.Setting.GetUIClickSoundKeys();
        }

        public IEnumerable<string> GetUIClickSoundKeys()
        {
            if (uiClickSounds == null)
            {
                yield break;
            }

            foreach (UIClickSoundSetting clickSound in uiClickSounds)
            {
                if (clickSound == null || string.IsNullOrEmpty(clickSound.key))
                {
                    continue;
                }

                yield return clickSound.key;
            }
        }

        public string GetUIClickSoundPath(string key)
        {
            return TryGetUIClickSoundPath(key, out string path) ? path : string.Empty;
        }

        public bool ContainsUIClickSoundKey(string key)
        {
            return TryGetUIClickSoundSetting(key, out _);
        }

        public bool TryGetUIClickSoundPath(string key, out string path)
        {
            path = string.Empty;
            if (!TryGetUIClickSoundSetting(key, out UIClickSoundSetting clickSound))
            {
                return false;
            }

            path = clickSound.path;
            return !string.IsNullOrEmpty(path);
        }

        public bool SetUIClickSoundPath(string key, string path)
        {
            if (!TryGetUIClickSoundSetting(key, out UIClickSoundSetting clickSound))
            {
                return false;
            }

            clickSound.path = path;
            return true;
        }

        public bool TryGetUIClickSoundSetting(string key, out UIClickSoundSetting setting)
        {
            setting = null;
            if (string.IsNullOrEmpty(key) || uiClickSounds == null)
            {
                return false;
            }

            foreach (UIClickSoundSetting clickSound in uiClickSounds)
            {
                if (clickSound == null || clickSound.key != key)
                {
                    continue;
                }

                setting = clickSound;
                return true;
            }

            return false;
        }
    }
}

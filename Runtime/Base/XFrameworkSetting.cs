using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework
{
    [Serializable]
    public sealed class XSceneType
    {
        public const string MainName = "Main";
        public const string SubName = "Sub";

        private static readonly XSceneType[] s_BuiltIn =
        {
            new(MainName, 1, 0, false),
            new(SubName, int.MaxValue, 100, true)
        };

        [SerializeField] private string name;
        [SerializeField, Min(1)] private int maxLoadedSceneCount = 1;
        [SerializeField] private int activePriority;
        [SerializeField]
        [Tooltip("切换 Main 类型场景时，是否卸载该类型下已加载的 XScene。")]
        private bool unloadOnMainSceneChanged = true;

        public XSceneType() { }

        private XSceneType(
            string name,
            int maxLoadedSceneCount,
            int activePriority,
            bool unloadOnMainSceneChanged)
        {
            this.name = name;
            this.maxLoadedSceneCount = maxLoadedSceneCount;
            this.activePriority = activePriority;
            this.unloadOnMainSceneChanged = unloadOnMainSceneChanged;
        }

        public string Name => name;
        public int MaxLoadedSceneCount => maxLoadedSceneCount;
        public int ActivePriority => activePriority;
        public bool UnloadOnMainSceneChanged => unloadOnMainSceneChanged;
        public static IReadOnlyList<XSceneType> BuiltIn => s_BuiltIn;
    }

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
        public string UIRootPrefabPath;

        [Header("Scene")]
        [Tooltip("项目补充的场景类型。Main 和 Sub 由框架内置，无需重复配置。")]
        [SerializeField] private XSceneType[] sceneTypes = Array.Empty<XSceneType>();

        public IReadOnlyList<XSceneType> SceneTypes => sceneTypes ?? Array.Empty<XSceneType>();

        public bool TryGetSceneType(string name, out XSceneType sceneType)
        {
            sceneType = null;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            foreach (XSceneType builtInSceneType in XSceneType.BuiltIn)
            {
                if (builtInSceneType.Name == name)
                {
                    sceneType = builtInSceneType;
                    break;
                }
            }

            foreach (XSceneType item in SceneTypes)
            {
                if (item == null || item.Name != name)
                {
                    continue;
                }

                if (sceneType != null)
                {
                    throw new XFrameworkException($"[XSceneManager] Duplicate scene type: {name}.");
                }

                sceneType = item;
            }

            return sceneType != null;
        }

        public IEnumerable<string> GetSceneTypeNames()
        {
            foreach (XSceneType sceneType in XSceneType.BuiltIn)
            {
                yield return sceneType.Name;
            }

            foreach (XSceneType sceneType in SceneTypes)
            {
                if (sceneType != null && !string.IsNullOrEmpty(sceneType.Name))
                {
                    yield return sceneType.Name;
                }
            }
        }

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

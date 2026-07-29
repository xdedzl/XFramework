using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace XFramework
{
    [CreateAssetMenu(fileName = "XScene", menuName = "XFramework/Scene/XScene")]
    public sealed class XScene : ScriptableObject
    {
        [SerializeField]
        [TextDropdown(typeof(XScene), nameof(GetSceneTypeOptions), false)]
        private string sceneType = XSceneType.MainName;

        [SerializeField, AssetPath(typeof(SceneAsset))]
        private string[] scenePaths = Array.Empty<string>();

        public string SceneType => sceneType;
        public IReadOnlyList<string> ScenePaths => scenePaths;

        private static IEnumerable<string> GetSceneTypeOptions()
        {
            return XApplication.Setting.GetSceneTypeNames();
        }
    }
}

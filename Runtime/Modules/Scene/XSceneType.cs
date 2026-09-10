using System;
using System.Collections.Generic;
using UnityEngine;

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

        private XSceneType(string name, int maxLoadedSceneCount, int activePriority, bool unloadOnMainSceneChanged)
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
}

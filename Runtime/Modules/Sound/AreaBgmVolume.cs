using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public readonly struct AreaBgmVolumeDebugSnapshot
    {
        public AreaBgmVolumeDebugSnapshot(
            int priority,
            bool randomOnEnter,
            int playerColliderCount,
            bool hasBgmClip,
            int validPathCount,
            IReadOnlyList<string> bgmPaths)
        {
            Priority = priority;
            RandomOnEnter = randomOnEnter;
            PlayerColliderCount = playerColliderCount;
            HasBgmClip = hasBgmClip;
            ValidPathCount = validPathCount;
            BgmPaths = bgmPaths;
        }

        public int Priority { get; }
        public bool RandomOnEnter { get; }
        public int PlayerColliderCount { get; }
        public bool HasBgmClip { get; }
        public int ValidPathCount { get; }
        public IReadOnlyList<string> BgmPaths { get; }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class AreaBgmVolume : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [SerializeField]
        [AssetPath(typeof(AudioClip))]
        [Tooltip("进入该区域时可播放的背景音乐资源路径。")]
        private string[] bgmClips;

        [SerializeField]
        [Tooltip("区域背景音乐优先级，数值越大越优先。")]
        private int priority;

        [SerializeField]
        [Tooltip("成为当前区域时是否随机选择一首背景音乐。")]
        private bool randomOnEnter = true;

        private readonly HashSet<Collider> m_PlayerColliders = new();

        public int Priority => priority;

        public bool HasBgmClip => GetValidClipCount() > 0;

        public AreaBgmVolumeDebugSnapshot GetDebugSnapshot()
        {
            return new AreaBgmVolumeDebugSnapshot(
                priority,
                randomOnEnter,
                m_PlayerColliders.Count,
                HasBgmClip,
                GetValidPathCount(),
                GetBgmPaths());
        }

        private void Reset()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerCollider(other))
            {
                return;
            }

            bool wasEmpty = m_PlayerColliders.Count == 0;
            m_PlayerColliders.Add(other);

            if (wasEmpty && m_PlayerColliders.Count > 0)
            {
                NotifyEnter();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!m_PlayerColliders.Remove(other))
            {
                return;
            }

            if (m_PlayerColliders.Count == 0)
            {
                NotifyExit();
            }
        }

        private void OnDisable()
        {
            m_PlayerColliders.Clear();
            NotifyExit();
        }

        public string GetBgmPath()
        {
            if (bgmClips == null || bgmClips.Length == 0)
            {
                return null;
            }

            if (!randomOnEnter)
            {
                return GetFirstValidPath();
            }

            int validCount = GetValidPathCount();
            if (validCount == 0)
            {
                return null;
            }

            int selectedIndex = Random.Range(0, validCount);
            int currentIndex = 0;
            for (int i = 0; i < bgmClips.Length; i++)
            {
                string path = bgmClips[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (currentIndex == selectedIndex)
                {
                    return path;
                }

                currentIndex++;
            }

            return null;
        }

        private bool IsPlayerCollider(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            Transform current = other.transform;
            while (current != null)
            {
                if (current.CompareTag(PlayerTag))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private string GetFirstValidPath()
        {
            for (int i = 0; i < bgmClips.Length; i++)
            {
                string path = bgmClips[i];
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return null;
        }

        private int GetValidClipCount()
        {
            return GetValidPathCount();
        }

        private int GetValidPathCount()
        {
            if (bgmClips == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < bgmClips.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(bgmClips[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private IReadOnlyList<string> GetBgmPaths()
        {
            if (bgmClips == null || bgmClips.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            string[] paths = new string[bgmClips.Length];
            for (int i = 0; i < bgmClips.Length; i++)
            {
                string path = bgmClips[i];
                paths[i] = string.IsNullOrWhiteSpace(path) ? "<Empty>" : path;
            }

            return paths;
        }

        private void NotifyEnter()
        {
            try
            {
                SoundManager.Instance.EnterBgmVolume(this);
            }
            catch (XFrameworkException)
            {
            }
        }

        private void NotifyExit()
        {
            try
            {
                SoundManager.Instance.ExitBgmVolume(this);
            }
            catch (XFrameworkException)
            {
            }
        }
    }
}

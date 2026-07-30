using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public readonly struct AreaLightVolumeDebugSnapshot
    {
        public AreaLightVolumeDebugSnapshot(
            int priority,
            int playerColliderCount,
            bool hasLightSettings)
        {
            Priority = priority;
            PlayerColliderCount = playerColliderCount;
            HasLightSettings = hasLightSettings;
        }

        public int Priority { get; }
        public int PlayerColliderCount { get; }
        public bool HasLightSettings { get; }
    }

    /// <summary>
    /// 区域全局光照 Volume。挂在带 Collider（isTrigger=true）的 GameObject 上，
    /// 玩家进入范围时通知 <see cref="LightVolumeManager"/> 用本 Volume 配置的光照参数覆盖当前全局光。
    /// 参考 <see cref="AreaBgmVolume"/> 的实现模式。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class AreaLightVolume : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        [SerializeField]
        [Tooltip("区域全局光优先级，数值越大越优先。")]
        private int priority;

        [SerializeField]
        [Tooltip("全局方向光参数。进入该区域时覆盖当前全局光。")]
        private DirectionalLightSettings lightSettings = DirectionalLightSettings.Default;

        private readonly HashSet<Collider> m_PlayerColliders = new();

        public int Priority => priority;

        public bool HasLightSettings => lightSettings != null && lightSettings.IsValid;

        public DirectionalLightSettings LightSettings => lightSettings;

        public AreaLightVolumeDebugSnapshot GetDebugSnapshot()
        {
            return new AreaLightVolumeDebugSnapshot(
                priority,
                m_PlayerColliders.Count,
                HasLightSettings);
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

        /// <summary>
        /// 校验已注册的玩家碰撞体是否仍在本 Volume 的触发范围内。
        /// 用于处理玩家被传送（禁用/启用 CharacterController 或直接设置 Transform）
        /// 导致 <see cref="OnTriggerExit"/> 未触发、<see cref="m_PlayerColliders"/> 残留过期引用的情况。
        /// 调用前应先 <see cref="Physics.SyncTransforms"/> 以保证 bounds 最新。
        /// </summary>
        public void ValidatePlayerOverlap()
        {
            if (m_PlayerColliders.Count == 0)
            {
                return;
            }

            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider == null)
            {
                return;
            }

            Bounds triggerBounds = triggerCollider.bounds;
            bool removedAny = false;
            m_PlayerColliders.RemoveWhere(collider =>
            {
                if (collider == null || !triggerBounds.Intersects(collider.bounds))
                {
                    removedAny = true;
                    return true;
                }

                return false;
            });

            if (removedAny && m_PlayerColliders.Count == 0)
            {
                NotifyExit();
            }
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

        private void NotifyEnter()
        {
            try
            {
                LightVolumeManager.Instance.EnterLightVolume(this);
            }
            catch (XFrameworkException)
            {
            }
        }

        private void NotifyExit()
        {
            try
            {
                LightVolumeManager.Instance.ExitLightVolume(this);
            }
            catch (XFrameworkException)
            {
            }
        }
    }
}

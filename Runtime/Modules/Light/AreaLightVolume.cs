using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public enum CameraEnvironmentBackgroundType
    {
        Skybox,
        SolidColor
    }

    /// <summary>
    /// 主相机环境背景设置。由 <see cref="AreaLightVolume"/> 按区域覆盖，
    /// 实际的相机与天空盒状态保存/恢复由 <see cref="LightVolumeManager"/> 负责。
    /// </summary>
    [System.Serializable]
    public class CameraEnvironmentSettings
    {
        [SerializeField]
        [Tooltip("区域背景类型。")]
        private CameraEnvironmentBackgroundType backgroundType = CameraEnvironmentBackgroundType.Skybox;

        [SerializeField]
        [Tooltip("天空盒材质。背景类型为 Skybox 时使用。")]
        private Material skyboxMaterial;

        [SerializeField]
        [Tooltip("纯色背景。背景类型为 Solid Color 时使用。")]
        private Color backgroundColor = Color.black;

        public CameraEnvironmentBackgroundType BackgroundType => backgroundType;
        public Material SkyboxMaterial => skyboxMaterial;
        public Color BackgroundColor => backgroundColor;

        public static CameraEnvironmentSettings Default => new();

        public void ApplyTo(Camera camera)
        {
            if (backgroundType == CameraEnvironmentBackgroundType.Skybox)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
        }
    }

    public readonly struct AreaLightVolumeDebugSnapshot
    {
        public AreaLightVolumeDebugSnapshot(
            int priority,
            int playerColliderCount,
            bool hasLightSettings,
            bool hasCameraEnvironmentSettings,
            CameraEnvironmentBackgroundType cameraEnvironmentBackgroundType)
        {
            Priority = priority;
            PlayerColliderCount = playerColliderCount;
            HasLightSettings = hasLightSettings;
            HasCameraEnvironmentSettings = hasCameraEnvironmentSettings;
            CameraEnvironmentBackgroundType = cameraEnvironmentBackgroundType;
        }

        public int Priority { get; }
        public int PlayerColliderCount { get; }
        public bool HasLightSettings { get; }
        public bool HasCameraEnvironmentSettings { get; }
        public CameraEnvironmentBackgroundType CameraEnvironmentBackgroundType { get; }
    }

    /// <summary>
    /// 区域光照与相机环境 Volume。挂在带 Collider（isTrigger=true）的 GameObject 上，
    /// 玩家进入范围时通知 <see cref="LightVolumeManager"/> 覆盖当前全局光和主相机环境。
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
        [Tooltip("是否覆盖全局方向光。关闭后该区域只参与相机环境设置。")]
        private bool overrideLightSettings = true;

        [SerializeField]
        [Tooltip("全局方向光参数。进入该区域时覆盖当前全局光。")]
        private DirectionalLightSettings lightSettings = DirectionalLightSettings.Default;

        [SerializeField]
        [Tooltip("是否覆盖主相机环境。")]
        private bool overrideEnvironmentSettings;

        [SerializeField]
        [Tooltip("主相机环境背景参数。启用后进入该区域时覆盖天空盒或纯色背景。")]
        private CameraEnvironmentSettings cameraEnvironmentSettings = CameraEnvironmentSettings.Default;

        private readonly HashSet<Collider> m_PlayerColliders = new();

        public int Priority => priority;

        public bool OverrideLightSettings => overrideLightSettings;

        public bool HasLightSettings => overrideLightSettings && lightSettings != null && lightSettings.IsValid;

        public bool OverrideEnvironmentSettings => overrideEnvironmentSettings;

        public bool HasCameraEnvironmentSettings => overrideEnvironmentSettings && cameraEnvironmentSettings != null;

        public bool HasAnySettings => HasLightSettings || HasCameraEnvironmentSettings;

        public DirectionalLightSettings LightSettings => lightSettings;

        public CameraEnvironmentSettings CameraEnvironmentSettings => cameraEnvironmentSettings;

        public AreaLightVolumeDebugSnapshot GetDebugSnapshot()
        {
            return new AreaLightVolumeDebugSnapshot(
                priority,
                m_PlayerColliders.Count,
                HasLightSettings,
                HasCameraEnvironmentSettings,
                cameraEnvironmentSettings != null
                    ? cameraEnvironmentSettings.BackgroundType
                    : CameraEnvironmentBackgroundType.Skybox);
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

using System.Collections.Generic;
using UnityEngine;

namespace XFramework
{
    public readonly struct LightVolumeManagerDebugSnapshot
    {
        public LightVolumeManagerDebugSnapshot(
            AreaLightVolume currentLightVolume,
            IReadOnlyList<AreaLightVolume> activeLightVolumes,
            bool hasSceneMainLight,
            string sceneMainLightName,
            bool hasManagerLight,
            DirectionalLightSettings originalSettings)
        {
            CurrentLightVolume = currentLightVolume;
            ActiveLightVolumes = activeLightVolumes;
            HasSceneMainLight = hasSceneMainLight;
            SceneMainLightName = sceneMainLightName;
            HasManagerLight = hasManagerLight;
            OriginalSettings = originalSettings;
        }

        public AreaLightVolume CurrentLightVolume { get; }
        public IReadOnlyList<AreaLightVolume> ActiveLightVolumes { get; }
        public bool HasSceneMainLight { get; }
        public string SceneMainLightName { get; }
        public bool HasManagerLight { get; }
        public DirectionalLightSettings OriginalSettings { get; }
    }

    /// <summary>
    /// 区域全局光管理器。参考 <see cref="SoundManager"/> 的 AreaBgmVolume 管理模式。
    /// 双模式逻辑（与编辑器 <c>LightVolumeEditorPreview</c> 一致）：
    /// 1. 场景有全局方向光时：进入 Volume 覆盖全局光参数；离开恢复原始参数
    /// 2. 场景没有全局方向光时：进入 Volume 创建临时光并应用参数；离开删除临时光
    /// </summary>
    [ModuleLifecycle(ModuleLifecycle.RuntimePersistent)]
    public class LightVolumeManager : GameModuleBase<LightVolumeManager>
    {
        private const string MainLightObjectName = "Directional Light";
        private const string ManagerLightName = "LightVolumeManager (Temp)";

        private readonly List<AreaLightVolume> m_ActiveLightVolumes = new();
        private AreaLightVolume m_CurrentLightVolume;
        private Light m_SceneMainLight;
        private DirectionalLightSettings m_SceneOriginalSettings;
        private Light m_ManagerLight;
        private bool m_IsOverridingSceneLight;

        public override void Initialize()
        {
            ResolveSceneMainLight();
        }

        public override void Shutdown()
        {
            RestoreSceneLight();
            DestroyManagerLight();
            m_SceneMainLight = null;
            m_SceneOriginalSettings = null;
            m_ActiveLightVolumes.Clear();
            m_CurrentLightVolume = null;
        }

        public void EnterLightVolume(AreaLightVolume volume)
        {
            if (volume == null || !volume.HasLightSettings)
            {
                return;
            }

            m_ActiveLightVolumes.Remove(volume);
            m_ActiveLightVolumes.Add(volume);
            RefreshAreaLight();
        }

        public void ExitLightVolume(AreaLightVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            m_ActiveLightVolumes.Remove(volume);
            RefreshAreaLight();
        }

        public void RefreshLightVolumes()
        {
            // 先校验各 Volume 的玩家碰撞体是否仍在其触发范围内，
            // 用于清理玩家被传送后残留的过期引用（OnTriggerExit 未触发的情况）。
            for (int i = m_ActiveLightVolumes.Count - 1; i >= 0; i--)
            {
                AreaLightVolume volume = m_ActiveLightVolumes[i];
                if (volume != null)
                {
                    volume.ValidatePlayerOverlap();
                }
            }

            RefreshAreaLight();
        }

        /// <summary>
        /// 通知某个 Volume 的光设置发生变化。如果该 Volume 正在生效，立即重新应用到当前光。
        /// </summary>
        public void NotifyVolumeSettingsChanged(AreaLightVolume volume)
        {
            if (volume == null || m_CurrentLightVolume != volume)
            {
                return;
            }

            if (m_SceneMainLight != null && m_IsOverridingSceneLight)
            {
                volume.LightSettings.ApplyTo(m_SceneMainLight);
            }
            else if (m_ManagerLight != null)
            {
                volume.LightSettings.ApplyTo(m_ManagerLight);
            }
        }

        public LightVolumeManagerDebugSnapshot GetDebugSnapshot()
        {
            var activeVolumes = new List<AreaLightVolume>();
            for (int i = 0; i < m_ActiveLightVolumes.Count; i++)
            {
                AreaLightVolume volume = m_ActiveLightVolumes[i];
                if (volume != null)
                {
                    activeVolumes.Add(volume);
                }
            }

            return new LightVolumeManagerDebugSnapshot(
                m_CurrentLightVolume,
                activeVolumes,
                m_SceneMainLight != null,
                m_SceneMainLight != null ? m_SceneMainLight.name : "<None>",
                m_ManagerLight != null,
                m_SceneOriginalSettings);
        }

        private void RefreshAreaLight()
        {
            ResolveSceneMainLight();

            AreaLightVolume nextVolume = ResolveCurrentLightVolume();
            AreaLightVolume previousVolume = m_CurrentLightVolume;
            m_CurrentLightVolume = nextVolume;

            if (nextVolume == null)
            {
                // 没有激活的 Volume
                if (m_SceneMainLight != null)
                {
                    RestoreSceneLight();
                }
                else
                {
                    DestroyManagerLight();
                }

                return;
            }

            // 有激活的 Volume
            if (m_SceneMainLight != null)
            {
                // 场景有全局光，覆盖参数
                EnsureSceneOriginalSettings();
                nextVolume.LightSettings.ApplyTo(m_SceneMainLight);
                m_IsOverridingSceneLight = true;
            }
            else
            {
                // 场景没有全局光，创建临时光
                EnsureManagerLight();
                nextVolume.LightSettings.ApplyTo(m_ManagerLight);
            }
        }

        private AreaLightVolume ResolveCurrentLightVolume()
        {
            AreaLightVolume bestVolume = null;
            int bestPriority = int.MinValue;

            for (int i = m_ActiveLightVolumes.Count - 1; i >= 0; i--)
            {
                AreaLightVolume volume = m_ActiveLightVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.HasLightSettings)
                {
                    m_ActiveLightVolumes.RemoveAt(i);
                    continue;
                }

                if (bestVolume == null || volume.Priority > bestPriority)
                {
                    bestVolume = volume;
                    bestPriority = volume.Priority;
                }
            }

            return bestVolume;
        }

        private void ResolveSceneMainLight()
        {
            if (m_SceneMainLight != null)
            {
                return;
            }

            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light.type == LightType.Directional && light.name == MainLightObjectName)
                {
                    m_SceneMainLight = light;
                    return;
                }
            }

            m_SceneMainLight = null;
        }

        private void EnsureSceneOriginalSettings()
        {
            if (m_IsOverridingSceneLight || m_SceneOriginalSettings != null)
            {
                return;
            }

            m_SceneOriginalSettings = DirectionalLightSettings.CaptureFrom(m_SceneMainLight);
        }

        private void RestoreSceneLight()
        {
            if (!m_IsOverridingSceneLight || m_SceneOriginalSettings == null)
            {
                return;
            }

            m_SceneOriginalSettings.ApplyTo(m_SceneMainLight);
            m_IsOverridingSceneLight = false;
        }

        private void EnsureManagerLight()
        {
            if (m_ManagerLight != null)
            {
                return;
            }

            GameObject go = new GameObject(ManagerLightName);
            m_ManagerLight = go.AddComponent<Light>();
            m_ManagerLight.type = LightType.Directional;
        }

        private void DestroyManagerLight()
        {
            if (m_ManagerLight != null)
            {
                Object.Destroy(m_ManagerLight.gameObject);
                m_ManagerLight = null;
            }
        }
    }
}

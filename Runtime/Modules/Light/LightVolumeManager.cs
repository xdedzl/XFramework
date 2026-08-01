using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XFramework
{
    public readonly struct LightVolumeManagerDebugSnapshot
    {
        public LightVolumeManagerDebugSnapshot(
            AreaLightVolume currentLightVolume,
            AreaLightVolume currentEnvironmentVolume,
            IReadOnlyList<AreaLightVolume> activeLightVolumes,
            bool hasSceneMainLight,
            string sceneMainLightName,
            bool hasManagerLight,
            DirectionalLightSettings originalSettings,
            bool hasMainCamera,
            string mainCameraName,
            bool isOverridingEnvironment,
            CameraClearFlags originalCameraClearFlags,
            Color originalCameraBackgroundColor,
            Material originalSkybox)
        {
            CurrentLightVolume = currentLightVolume;
            CurrentEnvironmentVolume = currentEnvironmentVolume;
            ActiveLightVolumes = activeLightVolumes;
            HasSceneMainLight = hasSceneMainLight;
            SceneMainLightName = sceneMainLightName;
            HasManagerLight = hasManagerLight;
            OriginalSettings = originalSettings;
            HasMainCamera = hasMainCamera;
            MainCameraName = mainCameraName;
            IsOverridingEnvironment = isOverridingEnvironment;
            OriginalCameraClearFlags = originalCameraClearFlags;
            OriginalCameraBackgroundColor = originalCameraBackgroundColor;
            OriginalSkybox = originalSkybox;
        }

        public AreaLightVolume CurrentLightVolume { get; }
        public AreaLightVolume CurrentEnvironmentVolume { get; }
        public IReadOnlyList<AreaLightVolume> ActiveLightVolumes { get; }
        public bool HasSceneMainLight { get; }
        public string SceneMainLightName { get; }
        public bool HasManagerLight { get; }
        public DirectionalLightSettings OriginalSettings { get; }
        public bool HasMainCamera { get; }
        public string MainCameraName { get; }
        public bool IsOverridingEnvironment { get; }
        public CameraClearFlags OriginalCameraClearFlags { get; }
        public Color OriginalCameraBackgroundColor { get; }
        public Material OriginalSkybox { get; }
    }

    /// <summary>
    /// 区域全局光与主相机环境管理器。参考 <see cref="SoundManager"/> 的 AreaBgmVolume 管理模式。
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
        private AreaLightVolume m_CurrentEnvironmentVolume;
        private Light m_SceneMainLight;
        private DirectionalLightSettings m_SceneOriginalSettings;
        private Light m_ManagerLight;
        private bool m_IsOverridingSceneLight;
        private Camera m_MainCamera;
        private CameraClearFlags m_OriginalCameraClearFlags;
        private Color m_OriginalCameraBackgroundColor;
        private Material m_OriginalSkybox;
        private int m_MainCameraSceneHandle;
        private bool m_IsOverridingEnvironment;

        public override void Initialize()
        {
            ResolveSceneMainLight();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        public override void Shutdown()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RestoreCameraEnvironment();
            RestoreSceneLight();
            DestroyManagerLight();
            m_SceneMainLight = null;
            m_SceneOriginalSettings = null;
            m_ActiveLightVolumes.Clear();
            m_CurrentLightVolume = null;
            m_CurrentEnvironmentVolume = null;
        }

        public void EnterLightVolume(AreaLightVolume volume)
        {
            if (volume == null || !volume.HasAnySettings)
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
        /// 通知某个 Volume 的设置发生变化，重新解析当前光照和相机环境 Volume。
        /// </summary>
        public void NotifyVolumeSettingsChanged(AreaLightVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            RefreshAreaLight();
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
                m_CurrentEnvironmentVolume,
                activeVolumes,
                m_SceneMainLight != null,
                m_SceneMainLight != null ? m_SceneMainLight.name : "<None>",
                m_ManagerLight != null,
                m_SceneOriginalSettings,
                m_MainCamera != null,
                m_MainCamera != null ? m_MainCamera.name : "<None>",
                m_IsOverridingEnvironment,
                m_OriginalCameraClearFlags,
                m_OriginalCameraBackgroundColor,
                m_OriginalSkybox);
        }

        private void RefreshAreaLight()
        {
            ResolveSceneMainLight();
            CleanupActiveVolumes();

            AreaLightVolume nextVolume = ResolveCurrentLightVolume();
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

            }
            else if (m_SceneMainLight != null)
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

            RefreshCameraEnvironment(ResolveCurrentEnvironmentVolume());
        }

        private void CleanupActiveVolumes()
        {
            for (int i = m_ActiveLightVolumes.Count - 1; i >= 0; i--)
            {
                AreaLightVolume volume = m_ActiveLightVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.HasAnySettings)
                {
                    m_ActiveLightVolumes.RemoveAt(i);
                }
            }
        }

        private AreaLightVolume ResolveCurrentLightVolume()
        {
            AreaLightVolume bestVolume = null;
            int bestPriority = int.MinValue;

            for (int i = m_ActiveLightVolumes.Count - 1; i >= 0; i--)
            {
                AreaLightVolume volume = m_ActiveLightVolumes[i];
                if (!volume.HasLightSettings)
                {
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

        private AreaLightVolume ResolveCurrentEnvironmentVolume()
        {
            AreaLightVolume bestVolume = null;
            int bestPriority = int.MinValue;

            for (int i = m_ActiveLightVolumes.Count - 1; i >= 0; i--)
            {
                AreaLightVolume volume = m_ActiveLightVolumes[i];
                if (!volume.HasCameraEnvironmentSettings)
                {
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

        private void RefreshCameraEnvironment(AreaLightVolume nextVolume)
        {
            Camera mainCamera = Camera.main;
            m_CurrentEnvironmentVolume = nextVolume;
            if (nextVolume == null)
            {
                RestoreCameraEnvironment();
                return;
            }

            if (mainCamera == null)
            {
                return;
            }

            ApplyCameraEnvironment(nextVolume, mainCamera);
        }

        private void ApplyCameraEnvironment(AreaLightVolume volume, Camera mainCamera)
        {
            if (!m_IsOverridingEnvironment || m_MainCamera != mainCamera)
            {
                CaptureMainCameraEnvironment(mainCamera);
            }

            CameraEnvironmentSettings settings = volume.CameraEnvironmentSettings;
            settings.ApplyTo(mainCamera);

            Material targetSkybox = settings.BackgroundType == CameraEnvironmentBackgroundType.Skybox
                ? settings.SkyboxMaterial
                : m_OriginalSkybox;
            SetSkybox(targetSkybox);
        }

        private void CaptureMainCameraEnvironment(Camera mainCamera)
        {
            bool preserveOriginalSkybox = m_IsOverridingEnvironment &&
                                          mainCamera.gameObject.scene.handle == m_MainCameraSceneHandle;
            if (m_MainCamera != null)
            {
                m_MainCamera.clearFlags = m_OriginalCameraClearFlags;
                m_MainCamera.backgroundColor = m_OriginalCameraBackgroundColor;
            }

            m_MainCamera = mainCamera;
            m_MainCameraSceneHandle = mainCamera.gameObject.scene.handle;
            m_OriginalCameraClearFlags = mainCamera.clearFlags;
            m_OriginalCameraBackgroundColor = mainCamera.backgroundColor;
            if (!preserveOriginalSkybox)
            {
                m_OriginalSkybox = RenderSettings.skybox;
            }

            m_IsOverridingEnvironment = true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (m_CurrentEnvironmentVolume == null ||
                camera.cameraType != CameraType.Game ||
                camera == m_MainCamera ||
                !camera.CompareTag("MainCamera"))
            {
                return;
            }

            ApplyCameraEnvironment(m_CurrentEnvironmentVolume, camera);
        }

        private void RestoreCameraEnvironment()
        {
            if (!m_IsOverridingEnvironment)
            {
                return;
            }

            if (m_MainCamera != null)
            {
                m_MainCamera.clearFlags = m_OriginalCameraClearFlags;
                m_MainCamera.backgroundColor = m_OriginalCameraBackgroundColor;
            }

            SetSkybox(m_OriginalSkybox);
            ClearCameraEnvironmentState();
        }

        private void ClearCameraEnvironmentState()
        {
            m_MainCamera = null;
            m_OriginalSkybox = null;
            m_MainCameraSceneHandle = 0;
            m_IsOverridingEnvironment = false;
        }

        private static void SetSkybox(Material skybox)
        {
            if (RenderSettings.skybox == skybox)
            {
                return;
            }

            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();
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

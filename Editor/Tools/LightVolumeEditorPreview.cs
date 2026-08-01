using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using XFramework;

namespace XFramework.Editor
{
    /// <summary>
    /// 编辑器预览光与相机环境管理器。方向光和相机环境分别按优先级选取当前 Volume，
    /// 场景保存、关闭以及切换 Play Mode 前恢复被预览覆盖的状态。
    /// </summary>
    [InitializeOnLoad]
    internal static class LightVolumeEditorPreview
    {
        private const string PreviewLightName = "LightVolumeEditorPreview (Temp)";

        private static Light s_SceneMainLight;
        private static DirectionalLightSettings s_SceneOriginalSettings;
        private static Light s_PreviewLight;
        private static bool s_IsOverridingSceneLight;
        private static AreaLightVolume s_CurrentLightVolumeAtCamera;

        private static AreaLightVolume s_CurrentEnvironmentVolumeAtCamera;
        private static SceneView s_EnvironmentSceneView;
        private static Material s_OriginalSkybox;
        private static bool s_OriginalSceneViewSkyboxEnabled;
        private static bool s_IsOverridingEnvironment;

#if UNITY_6000_0_OR_NEWER
        private static Camera s_RenderingSceneViewCamera;
        private static CameraClearFlags s_RenderingOriginalClearFlags;
        private static Color s_RenderingOriginalBackgroundColor;
#endif

        public readonly struct EditModeSnapshot
        {
            public EditModeSnapshot(
                bool hasSceneMainLight,
                string sceneMainLightName,
                bool isOverridingSceneLight,
                bool hasPreviewLight,
                AreaLightVolume currentLightVolumeAtCamera,
                DirectionalLightSettings originalSettings,
                AreaLightVolume currentEnvironmentVolumeAtCamera,
                bool isOverridingEnvironment,
                string environmentPreviewMode,
                Material originalSkybox)
            {
                HasSceneMainLight = hasSceneMainLight;
                SceneMainLightName = sceneMainLightName;
                IsOverridingSceneLight = isOverridingSceneLight;
                HasPreviewLight = hasPreviewLight;
                CurrentLightVolumeAtCamera = currentLightVolumeAtCamera;
                OriginalSettings = originalSettings;
                CurrentEnvironmentVolumeAtCamera = currentEnvironmentVolumeAtCamera;
                IsOverridingEnvironment = isOverridingEnvironment;
                EnvironmentPreviewMode = environmentPreviewMode;
                OriginalSkybox = originalSkybox;
            }

            public bool HasSceneMainLight { get; }
            public string SceneMainLightName { get; }
            public bool IsOverridingSceneLight { get; }
            public bool HasPreviewLight { get; }
            public AreaLightVolume CurrentLightVolumeAtCamera { get; }
            public DirectionalLightSettings OriginalSettings { get; }
            public AreaLightVolume CurrentEnvironmentVolumeAtCamera { get; }
            public bool IsOverridingEnvironment { get; }
            public string EnvironmentPreviewMode { get; }
            public Material OriginalSkybox { get; }
        }

        static LightVolumeEditorPreview()
        {
            CleanupOrphanPreviewLights();

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            SceneManager.sceneLoaded += OnSceneLoadedRuntime;
            SceneManager.sceneUnloaded += OnSceneUnloadedRuntime;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += RestorePreviewState;

#if UNITY_6000_0_OR_NEWER
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#endif
        }

        private static void CleanupOrphanPreviewLights()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool foundOrphan = false;
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.name == PreviewLightName)
                {
                    Object.DestroyImmediate(light.gameObject);
                    foundOrphan = true;
                }
            }

            if (foundOrphan)
            {
                InvalidatePreviewState();
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += () =>
            {
                RestorePreviewState();
                InvalidatePreviewState();
                UpdatePreviewBySceneCamera();
            };
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            RestorePreviewState();
            InvalidatePreviewState();
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            RestorePreviewState();
            s_CurrentLightVolumeAtCamera = null;
            s_CurrentEnvironmentVolumeAtCamera = null;
        }

        private static void OnSceneLoadedRuntime(Scene scene, LoadSceneMode mode)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    RestorePreviewState();
                    InvalidatePreviewState();
                    UpdatePreviewBySceneCamera();
                };
            }
        }

        private static void OnSceneUnloadedRuntime(Scene scene)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    RestorePreviewState();
                    InvalidatePreviewState();
                    UpdatePreviewBySceneCamera();
                };
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RestorePreviewState();
                InvalidatePreviewState();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += UpdatePreviewBySceneCamera;
            }
        }

        private static void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying)
            {
                UpdatePreviewBySceneCamera();
            }
        }

        [MenuItem("TheWar/Tools/Rendering/Refresh Light Volume Preview", false, 1200)]
        private static void RefreshPreviewMenu()
        {
            UpdatePreviewBySceneCamera();
        }

        private static void UpdatePreviewBySceneCamera()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                return;
            }

            ResolveSceneMainLight();

            Vector3 cameraPosition = sceneView.camera.transform.position;
            AreaLightVolume lightVolume = FindVolumeContainingPoint(cameraPosition, false);
            AreaLightVolume environmentVolume = FindVolumeContainingPoint(cameraPosition, true);

            if (lightVolume != s_CurrentLightVolumeAtCamera)
            {
                RestoreSceneLight();
                DestroyPreviewLight();
                s_CurrentLightVolumeAtCamera = lightVolume;
                ApplyLightPreview(lightVolume);
            }

            if (environmentVolume != s_CurrentEnvironmentVolumeAtCamera || sceneView != s_EnvironmentSceneView)
            {
                RestoreEnvironmentPreview();
                s_CurrentEnvironmentVolumeAtCamera = environmentVolume;
                ApplyEnvironmentPreview(environmentVolume, sceneView);
            }
        }

        public static EditModeSnapshot GetEditModeSnapshot()
        {
            return new EditModeSnapshot(
                s_SceneMainLight != null,
                s_SceneMainLight != null ? s_SceneMainLight.name : "<None>",
                s_IsOverridingSceneLight,
                s_PreviewLight != null,
                s_CurrentLightVolumeAtCamera,
                s_SceneOriginalSettings,
                s_CurrentEnvironmentVolumeAtCamera,
                s_IsOverridingEnvironment,
                GetEnvironmentPreviewMode(),
                s_OriginalSkybox);
        }

        internal static void NotifyVolumeSettingsChanged(AreaLightVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            bool wasCurrentEnvironment = s_CurrentEnvironmentVolumeAtCamera == volume;
            UpdatePreviewBySceneCamera();

            if (s_CurrentLightVolumeAtCamera == volume)
            {
                ApplyCurrentLightSettings(volume);
            }

            if (wasCurrentEnvironment && s_CurrentEnvironmentVolumeAtCamera == volume)
            {
                SceneView sceneView = s_EnvironmentSceneView;
                RestoreEnvironmentPreview();
                ApplyEnvironmentPreview(volume, sceneView);
            }
        }

        private static void ApplyLightPreview(AreaLightVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            if (s_SceneMainLight != null)
            {
                EnsureSceneOriginalSettings();
                volume.LightSettings.ApplyTo(s_SceneMainLight);
                s_IsOverridingSceneLight = true;
            }
            else
            {
                EnsurePreviewLight();
                volume.LightSettings.ApplyTo(s_PreviewLight);
            }
        }

        private static void ApplyCurrentLightSettings(AreaLightVolume volume)
        {
            if (s_SceneMainLight != null && s_IsOverridingSceneLight)
            {
                volume.LightSettings.ApplyTo(s_SceneMainLight);
            }
            else if (s_PreviewLight != null)
            {
                volume.LightSettings.ApplyTo(s_PreviewLight);
            }
        }

        private static void ApplyEnvironmentPreview(AreaLightVolume volume, SceneView sceneView)
        {
            if (volume == null || sceneView == null)
            {
                return;
            }

            CameraEnvironmentSettings settings = volume.CameraEnvironmentSettings;
            s_EnvironmentSceneView = sceneView;
            s_OriginalSkybox = RenderSettings.skybox;
            s_OriginalSceneViewSkyboxEnabled = sceneView.sceneViewState.showSkybox;
            s_IsOverridingEnvironment = true;

            bool useSkybox = settings.BackgroundType == CameraEnvironmentBackgroundType.Skybox;
            sceneView.sceneViewState.showSkybox = useSkybox;
            SetSkybox(useSkybox ? settings.SkyboxMaterial : s_OriginalSkybox);
            SceneView.RepaintAll();
        }

        private static void RestorePreviewState()
        {
            RestoreEnvironmentPreview();
            RestoreSceneLight();
            DestroyPreviewLight();
        }

        private static void RestoreEnvironmentPreview()
        {
            if (!s_IsOverridingEnvironment)
            {
                return;
            }

            if (s_EnvironmentSceneView != null)
            {
                s_EnvironmentSceneView.sceneViewState.showSkybox = s_OriginalSceneViewSkyboxEnabled;
            }

            SetSkybox(s_OriginalSkybox);
            s_EnvironmentSceneView = null;
            s_OriginalSkybox = null;
            s_IsOverridingEnvironment = false;
            SceneView.RepaintAll();
        }

        private static AreaLightVolume FindVolumeContainingPoint(Vector3 point, bool environment)
        {
            AreaLightVolume bestVolume = null;
            int bestPriority = int.MinValue;

            AreaLightVolume[] volumes = Object.FindObjectsByType<AreaLightVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                AreaLightVolume volume = volumes[i];
                bool hasSettings = environment ? volume.HasCameraEnvironmentSettings : volume.HasLightSettings;
                if (!volume.isActiveAndEnabled || !hasSettings)
                {
                    continue;
                }

                Collider collider = volume.GetComponent<Collider>();
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Vector3 closest = collider.ClosestPoint(point);
                if ((closest - point).sqrMagnitude < 0.0001f &&
                    (bestVolume == null || volume.Priority > bestPriority))
                {
                    bestVolume = volume;
                    bestPriority = volume.Priority;
                }
            }

            return bestVolume;
        }

        private static void EnsurePreviewLight()
        {
            if (s_PreviewLight != null)
            {
                return;
            }

            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].name == PreviewLightName)
                {
                    s_PreviewLight = lights[i];
                    ApplyPreviewLightHideFlags(s_PreviewLight);
                    return;
                }
            }

            GameObject go = new GameObject(PreviewLightName);
            s_PreviewLight = go.AddComponent<Light>();
            s_PreviewLight.type = LightType.Directional;
            ApplyPreviewLightHideFlags(s_PreviewLight);
        }

        private static void ApplyPreviewLightHideFlags(Light light)
        {
            const HideFlags flags = HideFlags.DontSave | HideFlags.NotEditable;
            light.gameObject.hideFlags = flags;
            light.hideFlags = flags;
        }

        private static void DestroyPreviewLight()
        {
            if (s_PreviewLight != null)
            {
                Object.DestroyImmediate(s_PreviewLight.gameObject);
                s_PreviewLight = null;
            }
        }

        private static void ResolveSceneMainLight()
        {
            if (s_SceneMainLight != null)
            {
                return;
            }

            s_SceneOriginalSettings = null;
            s_IsOverridingSceneLight = false;

            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light.type == LightType.Directional && light.name == "Directional Light")
                {
                    s_SceneMainLight = light;
                    return;
                }
            }

            s_SceneMainLight = null;
        }

        private static void EnsureSceneOriginalSettings()
        {
            if (s_IsOverridingSceneLight || s_SceneOriginalSettings != null)
            {
                return;
            }

            s_SceneOriginalSettings = DirectionalLightSettings.CaptureFrom(s_SceneMainLight);
        }

        private static void RestoreSceneLight()
        {
            if (!s_IsOverridingSceneLight || s_SceneOriginalSettings == null)
            {
                return;
            }

            s_SceneOriginalSettings.ApplyTo(s_SceneMainLight);
            s_IsOverridingSceneLight = false;
        }

        private static void InvalidatePreviewState()
        {
            s_SceneMainLight = null;
            s_SceneOriginalSettings = null;
            s_IsOverridingSceneLight = false;
            s_CurrentLightVolumeAtCamera = null;
            s_CurrentEnvironmentVolumeAtCamera = null;
            s_EnvironmentSceneView = null;
            s_OriginalSkybox = null;
            s_IsOverridingEnvironment = false;
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

        private static string GetEnvironmentPreviewMode()
        {
#if UNITY_6000_0_OR_NEWER
            return "Unity 6 完整预览";
#else
            return "Unity 2022 天空盒预览 / 纯色使用编辑器背景";
#endif
        }

#if UNITY_6000_0_OR_NEWER
        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (EditorApplication.isPlaying ||
                camera.cameraType != CameraType.SceneView ||
                s_CurrentEnvironmentVolumeAtCamera == null ||
                s_CurrentEnvironmentVolumeAtCamera.CameraEnvironmentSettings.BackgroundType != CameraEnvironmentBackgroundType.SolidColor ||
                s_EnvironmentSceneView == null ||
                camera != s_EnvironmentSceneView.camera)
            {
                return;
            }

            s_RenderingSceneViewCamera = camera;
            s_RenderingOriginalClearFlags = camera.clearFlags;
            s_RenderingOriginalBackgroundColor = camera.backgroundColor;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = s_CurrentEnvironmentVolumeAtCamera.CameraEnvironmentSettings.BackgroundColor;
        }

        private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != s_RenderingSceneViewCamera)
            {
                return;
            }

            camera.clearFlags = s_RenderingOriginalClearFlags;
            camera.backgroundColor = s_RenderingOriginalBackgroundColor;
            s_RenderingSceneViewCamera = null;
        }
#endif
    }
}

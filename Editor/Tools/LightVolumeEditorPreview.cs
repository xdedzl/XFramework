using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFramework;

namespace XFramework.Editor
{
    /// <summary>
    /// 编辑器预览光管理器。遵循与运行时 <see cref="LightVolumeManager"/> 相同的双模式逻辑：
    /// 1. 场景没有全局方向光时：摄像机进入 Volume → 创建临时光并应用 Volume 参数；离开 → 删除临时光
    /// 2. 场景有全局方向光时：摄像机进入 Volume → 覆盖全局光参数；离开 → 恢复原始参数
    /// 场景保存前自动恢复原始参数，避免误保存覆盖后的值。
    /// </summary>
    [InitializeOnLoad]
    internal static class LightVolumeEditorPreview
    {
        private const string PreviewLightName = "LightVolumeEditorPreview (Temp)";

        private static Light s_SceneMainLight;
        private static DirectionalLightSettings s_SceneOriginalSettings;
        private static Light s_PreviewLight;
        private static bool s_IsOverridingSceneLight;
        private static AreaLightVolume s_CurrentVolumeAtCamera;

        /// <summary>
        /// 编辑模式预览状态快照，供 Debug 窗口查询。
        /// </summary>
        public readonly struct EditModeSnapshot
        {
            public EditModeSnapshot(
                bool hasSceneMainLight,
                string sceneMainLightName,
                bool isOverridingSceneLight,
                bool hasPreviewLight,
                AreaLightVolume currentVolumeAtCamera,
                DirectionalLightSettings originalSettings)
            {
                HasSceneMainLight = hasSceneMainLight;
                SceneMainLightName = sceneMainLightName;
                IsOverridingSceneLight = isOverridingSceneLight;
                HasPreviewLight = hasPreviewLight;
                CurrentVolumeAtCamera = currentVolumeAtCamera;
                OriginalSettings = originalSettings;
            }

            public bool HasSceneMainLight { get; }
            public string SceneMainLightName { get; }
            public bool IsOverridingSceneLight { get; }
            public bool HasPreviewLight { get; }
            public AreaLightVolume CurrentVolumeAtCamera { get; }
            public DirectionalLightSettings OriginalSettings { get; }
        }

        static LightVolumeEditorPreview()
        {
            // 编译/域重载后静态字段被清空，但场景里可能残留上一轮创建的预览光对象，先清理
            CleanupOrphanPreviewLights();

            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            EditorSceneManager.sceneSaving += OnSceneSaving;
            SceneManager.sceneLoaded += OnSceneLoadedRuntime;
            SceneManager.sceneUnloaded += OnSceneUnloadedRuntime;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// 清理场景中所有同名的孤儿预览光对象。
        /// 用于编译/域重载后静态引用丢失的情况。
        /// </summary>
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
                InvalidateSceneMainLight();
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += () =>
            {
                InvalidateSceneMainLight();
                UpdatePreviewBySceneCamera();
            };
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            RestoreSceneLight();
            DestroyPreviewLight();
            InvalidateSceneMainLight();
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            // 保存前恢复场景主光，避免覆盖值写入场景文件
            RestoreSceneLight();
        }

        private static void OnSceneLoadedRuntime(Scene scene, LoadSceneMode mode)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += () =>
                {
                    InvalidateSceneMainLight();
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
                    InvalidateSceneMainLight();
                    UpdatePreviewBySceneCamera();
                };
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                RestoreSceneLight();
                DestroyPreviewLight();
                InvalidateSceneMainLight();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += UpdatePreviewBySceneCamera;
            }
        }

        private static void OnEditorUpdate()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            UpdatePreviewBySceneCamera();
        }

        private static void OnSelectionChanged()
        {
        }

        [MenuItem("TheWar/Tools/Rendering/Refresh Light Volume Preview", false, 1200)]
        private static void RefreshPreviewMenu()
        {
            UpdatePreviewBySceneCamera();
        }

        /// <summary>
        /// 根据 Scene 摄像机位置更新预览光，双模式：
        /// - 场景有全局光：进入 Volume 覆盖参数，离开恢复
        /// - 场景没有全局光：进入 Volume 创建临时光，离开删除
        /// </summary>
        private static void UpdatePreviewBySceneCamera()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            ResolveSceneMainLight();

            Vector3? cameraPos = GetSceneCameraPosition();
            if (!cameraPos.HasValue)
            {
                return;
            }

            AreaLightVolume volumeAtCamera = FindVolumeContainingPoint(cameraPos.Value);

            // 如果当前生效的 Volume 没变，不做任何操作
            if (volumeAtCamera == s_CurrentVolumeAtCamera)
            {
                return;
            }

            // 切换：先恢复上一个 Volume 和场景主光
            RestoreSceneLight();
            DestroyPreviewLight();
            s_CurrentVolumeAtCamera = volumeAtCamera;

            // 启用新 Volume
            if (volumeAtCamera != null)
            {
                if (s_SceneMainLight != null)
                {
                    // 场景有全局光，覆盖参数
                    EnsureSceneOriginalSettings();
                    volumeAtCamera.LightSettings.ApplyTo(s_SceneMainLight);
                    s_IsOverridingSceneLight = true;
                }
                else
                {
                    // 场景没有全局光，创建临时光并应用参数
                    EnsurePreviewLight();
                    volumeAtCamera.LightSettings.ApplyTo(s_PreviewLight);
                }
            }
        }

        /// <summary>
        /// 获取编辑模式预览状态快照，供 Debug 窗口查询。
        /// </summary>
        public static EditModeSnapshot GetEditModeSnapshot()
        {
            return new EditModeSnapshot(
                s_SceneMainLight != null,
                s_SceneMainLight != null ? s_SceneMainLight.name : "<None>",
                s_IsOverridingSceneLight,
                s_PreviewLight != null,
                s_CurrentVolumeAtCamera,
                s_SceneOriginalSettings);
        }

        /// <summary>
        /// 通知某个 Volume 的光设置发生变化。如果该 Volume 正在预览中，立即重新应用参数。
        /// </summary>
        internal static void NotifyVolumeSettingsChanged(AreaLightVolume volume)
        {
            if (volume == null || s_CurrentVolumeAtCamera != volume)
            {
                return;
            }

            if (s_SceneMainLight != null && s_IsOverridingSceneLight)
            {
                volume.LightSettings.ApplyTo(s_SceneMainLight);
            }
            else if (s_PreviewLight != null)
            {
                volume.LightSettings.ApplyTo(s_PreviewLight);
            }
        }

        private static void EnsurePreviewLight()
        {
            if (s_PreviewLight != null)
            {
                return;
            }

            // 先检查场景里是否已有同名对象（域重载后引用丢失但对象可能还在）
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

        /// <summary>
        /// 设置预览光 GameObject 和 Light 组件的 hideFlags：
        /// - DontSave: 不保存到场景，不序列化
        /// - NotEditable: 在 Inspector 中不可修改
        /// </summary>
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

        private static Vector3? GetSceneCameraPosition()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                return null;
            }

            return sceneView.camera.transform.position;
        }

        private static AreaLightVolume FindVolumeContainingPoint(Vector3 point)
        {
            AreaLightVolume bestVolume = null;
            int bestPriority = int.MinValue;

            AreaLightVolume[] volumes = Object.FindObjectsByType<AreaLightVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < volumes.Length; i++)
            {
                AreaLightVolume volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.HasLightSettings)
                {
                    continue;
                }

                Collider collider = volume.GetComponent<Collider>();
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Vector3 closest = collider.ClosestPoint(point);
                if ((closest - point).sqrMagnitude < 0.0001f)
                {
                    if (bestVolume == null || volume.Priority > bestPriority)
                    {
                        bestVolume = volume;
                        bestPriority = volume.Priority;
                    }
                }
            }

            return bestVolume;
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

        private static void InvalidateSceneMainLight()
        {
            s_SceneMainLight = null;
            s_SceneOriginalSettings = null;
            s_IsOverridingSceneLight = false;
            s_CurrentVolumeAtCamera = null;
        }
    }
}

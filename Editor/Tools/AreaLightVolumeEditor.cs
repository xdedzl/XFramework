using UnityEditor;
using UnityEngine;
using XFramework;

namespace XFramework.Editor
{
    /// <summary>
    /// <see cref="AreaLightVolume"/> 的自定义 Inspector。
    /// 监听光照和相机环境设置变化，如果该 Volume 正在生效则立即重新应用。
    /// </summary>
    [CustomEditor(typeof(AreaLightVolume))]
    internal class AreaLightVolumeEditor : UnityEditor.Editor
    {
        private AreaLightVolume m_Volume;
        private SerializedProperty m_PriorityProperty;
        private SerializedProperty m_OverrideLightSettingsProperty;
        private SerializedProperty m_LightSettingsProperty;
        private SerializedProperty m_OverrideEnvironmentSettingsProperty;
        private SerializedProperty m_CameraEnvironmentSettingsProperty;

        private void OnEnable()
        {
            m_Volume = (AreaLightVolume)target;
            m_PriorityProperty = serializedObject.FindProperty("priority");
            m_OverrideLightSettingsProperty = serializedObject.FindProperty("overrideLightSettings");
            m_LightSettingsProperty = serializedObject.FindProperty("lightSettings");
            m_OverrideEnvironmentSettingsProperty = serializedObject.FindProperty("overrideEnvironmentSettings");
            m_CameraEnvironmentSettingsProperty = serializedObject.FindProperty("cameraEnvironmentSettings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(m_PriorityProperty);
            EditorGUILayout.PropertyField(
                m_OverrideLightSettingsProperty,
                new GUIContent("覆盖全局光照", "进入该区域时是否覆盖全局方向光。"));
            if (m_OverrideLightSettingsProperty.boolValue)
            {
                EditorGUILayout.PropertyField(m_LightSettingsProperty, true);
            }

            EditorGUILayout.PropertyField(
                m_OverrideEnvironmentSettingsProperty,
                new GUIContent("覆盖相机环境", "进入该区域时是否覆盖主相机的天空盒或纯色背景。"));
            if (m_OverrideEnvironmentSettingsProperty.boolValue)
            {
                DrawCameraEnvironmentSettings();
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                NotifySettingsChanged();
            }
            else
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawCameraEnvironmentSettings()
        {
            SerializedProperty backgroundTypeProperty = m_CameraEnvironmentSettingsProperty.FindPropertyRelative("backgroundType");
            SerializedProperty skyboxMaterialProperty = m_CameraEnvironmentSettingsProperty.FindPropertyRelative("skyboxMaterial");
            SerializedProperty backgroundColorProperty = m_CameraEnvironmentSettingsProperty.FindPropertyRelative("backgroundColor");

            m_CameraEnvironmentSettingsProperty.isExpanded = EditorGUILayout.Foldout(
                m_CameraEnvironmentSettingsProperty.isExpanded,
                "相机环境设置",
                true);
            if (!m_CameraEnvironmentSettingsProperty.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(backgroundTypeProperty, new GUIContent("背景类型"));
                CameraEnvironmentBackgroundType backgroundType =
                    (CameraEnvironmentBackgroundType)backgroundTypeProperty.enumValueIndex;
                if (backgroundType == CameraEnvironmentBackgroundType.Skybox)
                {
                    EditorGUILayout.PropertyField(skyboxMaterialProperty, new GUIContent("天空盒材质"));
                }
                else
                {
                    EditorGUILayout.PropertyField(backgroundColorProperty, new GUIContent("背景颜色"));
#if !UNITY_6000_0_OR_NEWER
                    EditorGUILayout.HelpBox(
                        "Unity 2022 SceneView 使用编辑器背景色，准确颜色请在 Game View 查看。",
                        MessageType.Info);
#endif
                }
            }
        }

        private void NotifySettingsChanged()
        {
            // 编辑器预览（非运行模式）
            if (!EditorApplication.isPlaying)
            {
                LightVolumeEditorPreview.NotifyVolumeSettingsChanged(m_Volume);
                return;
            }

            // 运行时
            try
            {
                if (GameEntry.IsModuleLoaded<LightVolumeManager>())
                {
                    LightVolumeManager.Instance.NotifyVolumeSettingsChanged(m_Volume);
                }
            }
            catch (XFrameworkException)
            {
            }
        }
    }
}

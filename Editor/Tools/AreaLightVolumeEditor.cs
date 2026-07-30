using UnityEditor;
using XFramework;

namespace XFramework.Editor
{
    /// <summary>
    /// <see cref="AreaLightVolume"/> 的自定义 Inspector。
    /// 监听光设置变化，如果该 Volume 正在生效，立即重新应用到当前光。
    /// </summary>
    [CustomEditor(typeof(AreaLightVolume))]
    internal class AreaLightVolumeEditor : UnityEditor.Editor
    {
        private AreaLightVolume m_Volume;

        private void OnEnable()
        {
            m_Volume = (AreaLightVolume)target;
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                NotifySettingsChanged();
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

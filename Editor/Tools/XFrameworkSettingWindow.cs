using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public class XFrameworkSettingWindow : EditorWindow
    {
        private const string MenuPath = "XFramework/Setting";

        private Slider m_TimeScaleSlider;
        private Label m_PlayModeHint;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            XFrameworkSettingWindow window = GetWindow<XFrameworkSettingWindow>();
            window.titleContent = new GUIContent("Setting");
            window.minSize = new Vector2(320f, 150f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += RefreshTimeScale;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= RefreshTimeScale;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            m_TimeScaleSlider = new Slider("Time Scale", 0f, 3f)
            {
                showInputField = true,
                tooltip = "仅在播放模式下生效；退出播放模式后会恢复为 Unity 的默认行为。"
            };
            m_TimeScaleSlider.RegisterValueChangedCallback(evt => SetTimeScale(evt.newValue));
            root.Add(m_TimeScaleSlider);

            m_PlayModeHint = new Label();
            m_PlayModeHint.style.marginTop = 10f;
            m_PlayModeHint.style.color = new Color(0.72f, 0.72f, 0.72f);
            root.Add(m_PlayModeHint);

            RefreshView();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _)
        {
            RefreshView();
        }

        private void RefreshTimeScale()
        {
            if (EditorApplication.isPlaying && m_TimeScaleSlider != null && !Mathf.Approximately(m_TimeScaleSlider.value, Time.timeScale))
            {
                m_TimeScaleSlider.SetValueWithoutNotify(Time.timeScale);
            }
        }

        private void RefreshView()
        {
            bool isPlaying = EditorApplication.isPlaying;
            if (m_TimeScaleSlider != null)
            {
                m_TimeScaleSlider.SetEnabled(isPlaying);
                m_TimeScaleSlider.SetValueWithoutNotify(isPlaying ? Time.timeScale : 1f);
            }

            if (m_PlayModeHint != null)
            {
                m_PlayModeHint.text = isPlaying ? "当前设置仅在本次运行期间生效。" : "进入播放模式后可调整 Time Scale。";
            }
        }

        private void SetTimeScale(float value)
        {
            if (!EditorApplication.isPlaying)
            {
                RefreshView();
                return;
            }

            Time.timeScale = Mathf.Max(0f, value);
            m_TimeScaleSlider?.SetValueWithoutNotify(Time.timeScale);
        }
    }
}

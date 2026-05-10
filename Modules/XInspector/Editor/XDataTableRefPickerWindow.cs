using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    public sealed class XDataTableRefPickerWindow : EditorWindow
    {
        private string m_Title;
        private XDataTableRefMeta m_Meta;
        private Type m_OwnerFieldType;
        private Action<object> m_OnPick;
        private TextField m_SearchField;
        private ScrollView m_OptionsScrollView;

        public static void ShowWindow(
            string title,
            XDataTableRefMeta meta,
            Type ownerFieldType,
            Action<object> onPick,
            Rect? anchorRect = null)
        {
            XDataTableRefPickerWindow window = CreateInstance<XDataTableRefPickerWindow>();
            window.titleContent = new GUIContent("DataTable Ref Picker");
            window.minSize = new Vector2(300f, 420f);
            window.Init(title, meta, ownerFieldType, onPick, anchorRect);
            window.ShowUtility();
            window.Focus();
        }

        private void Init(string title, XDataTableRefMeta meta, Type ownerFieldType, Action<object> onPick, Rect? anchorRect)
        {
            m_Title = string.IsNullOrWhiteSpace(title) ? "选择引用" : $"选择 {title}";
            m_Meta = meta;
            m_OwnerFieldType = ownerFieldType;
            m_OnPick = onPick;
            if (anchorRect.HasValue)
            {
                Rect rect = anchorRect.Value;
                position = new Rect(rect.x + 80f, rect.y + 80f, 300f, 420f);
            }

            BuildUI();
        }

        public void CreateGUI()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            root.focusable = true;
            root.RegisterCallback<KeyDownEvent>(OnRootKeyDown);

            Label titleLabel = new(m_Title ?? "选择引用");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8f;
            root.Add(titleLabel);

            m_SearchField = new TextField("搜索");
            m_SearchField.isDelayed = true;
            m_SearchField.RegisterValueChangedCallback(_ => RebuildOptions());
            root.Add(m_SearchField);

            m_OptionsScrollView = new ScrollView();
            m_OptionsScrollView.style.flexGrow = 1f;
            m_OptionsScrollView.style.marginTop = 8f;
            root.Add(m_OptionsScrollView);

            RebuildOptions();
            root.schedule.Execute(() =>
            {
                m_SearchField?.Focus();
                root.Focus();
            });
        }

        private void RebuildOptions()
        {
            if (m_OptionsScrollView == null)
            {
                return;
            }

            m_OptionsScrollView.Clear();

            if (m_Meta == null)
            {
                m_OptionsScrollView.Add(new Label("当前没有可选项。"));
                return;
            }

            if (XDataTableRefResolver.SupportsEmptyReference(m_OwnerFieldType))
            {
                m_OptionsScrollView.Add(CreateOptionButton("None", () =>
                {
                    m_OnPick?.Invoke(XDataTableRefResolver.GetEmptyReferenceValue(m_OwnerFieldType));
                    Close();
                }));
            }

            IReadOnlyList<XDataTableRefOption> options = XDataTableRefResolver.GetOptions(m_Meta, m_SearchField?.value);
            foreach (XDataTableRefOption option in options)
            {
                m_OptionsScrollView.Add(CreateOptionButton(option.DisplayText, () =>
                {
                    m_OnPick?.Invoke(option.KeyValue);
                    Close();
                }));
            }
        }

        private static Button CreateOptionButton(string text, Action onClick)
        {
            Button button = new(onClick)
            {
                text = text
            };
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.marginBottom = 4f;
            button.style.whiteSpace = WhiteSpace.Normal;
            return button;
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            Close();
            evt.StopPropagation();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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
        private object m_SelectedValue;
        private TextField m_SearchField;
        private VisualElement m_TableTabsContainer;
        private ScrollView m_OptionsScrollView;
        private VisualElement m_SelectedOptionElement;
        private Type m_SelectedSourceTableType;

        public static void ShowWindow(
            string title,
            XDataTableRefMeta meta,
            Type ownerFieldType,
            Action<object> onPick,
            Rect? anchorRect = null,
            object selectedValue = null)
        {
            XDataTableRefPickerWindow window = CreateInstance<XDataTableRefPickerWindow>();
            window.titleContent = new GUIContent("DataTable Ref Picker");
            window.minSize = new Vector2(300f, 420f);
            window.Init(title, meta, ownerFieldType, onPick, anchorRect, selectedValue);
            window.ShowUtility();
            window.Focus();
        }

        private void Init(string title, XDataTableRefMeta meta, Type ownerFieldType, Action<object> onPick, Rect? anchorRect, object selectedValue)
        {
            m_Title = string.IsNullOrWhiteSpace(title) ? "选择引用" : $"选择 {title}";
            m_Meta = meta;
            m_OwnerFieldType = ownerFieldType;
            m_OnPick = onPick;
            m_SelectedValue = selectedValue;
            m_SelectedSourceTableType = null;
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

            if (ShouldShowTableTabs())
            {
                ScrollView tabsScrollView = new(ScrollViewMode.Horizontal);
                tabsScrollView.style.height = 34f;
                tabsScrollView.style.marginTop = 8f;
                tabsScrollView.style.marginBottom = 0f;
                tabsScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                tabsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
                m_TableTabsContainer = tabsScrollView.contentContainer;
                m_TableTabsContainer.style.flexDirection = FlexDirection.Row;
                root.Add(tabsScrollView);
                RebuildTableTabs();
            }
            else
            {
                m_TableTabsContainer = null;
                m_SelectedSourceTableType = null;
            }

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

        private void RebuildTableTabs()
        {
            if (m_TableTabsContainer == null)
            {
                return;
            }

            m_TableTabsContainer.Clear();
            m_TableTabsContainer.Add(CreateTableTabButton("ALL", null));

            foreach (Type sourceTableType in GetSourceTableTypes())
            {
                m_TableTabsContainer.Add(CreateTableTabButton(sourceTableType.Name, sourceTableType));
            }
        }

        private void RebuildOptions()
        {
            if (m_OptionsScrollView == null)
            {
                return;
            }

            m_OptionsScrollView.Clear();
            m_SelectedOptionElement = null;

            if (m_Meta == null)
            {
                m_OptionsScrollView.Add(new Label("当前没有可选项。"));
                return;
            }

            if (XDataTableRefResolver.SupportsEmptyReference(m_OwnerFieldType))
            {
                Button noneButton = CreateOptionButton("None", () =>
                {
                    m_OnPick?.Invoke(XDataTableRefResolver.GetEmptyReferenceValue(m_OwnerFieldType));
                    Close();
                });
                if (XDataTableRefResolver.IsEmptyReferenceValue(m_SelectedValue, m_OwnerFieldType))
                {
                    MarkSelectedOption(noneButton);
                }

                m_OptionsScrollView.Add(noneButton);
            }

            IReadOnlyList<XDataTableRefOption> options = XDataTableRefResolver.GetOptions(m_Meta, m_SearchField?.value);
            foreach (XDataTableRefOption option in options)
            {
                if (m_SelectedSourceTableType != null && option.SourceTableType != m_SelectedSourceTableType)
                {
                    continue;
                }

                Button optionButton = CreateOptionButton(option.DisplayText, () =>
                {
                    m_OnPick?.Invoke(option.KeyValue);
                    Close();
                });
                if (IsSelectedOption(option.KeyValue))
                {
                    MarkSelectedOption(optionButton);
                }

                m_OptionsScrollView.Add(optionButton);
            }

            ScheduleScrollToSelectedOption();
        }

        private bool ShouldShowTableTabs()
        {
            return m_Meta != null && m_Meta.IsUnionTable && GetSourceTableTypes().Count > 0;
        }

        private List<Type> GetSourceTableTypes()
        {
            if (m_Meta?.TableAssetTypes == null)
            {
                return new List<Type>();
            }

            return m_Meta.TableAssetTypes
                .Where(type => type != null)
                .Distinct()
                .ToList();
        }

        private Button CreateTableTabButton(string text, Type sourceTableType)
        {
            bool selected = m_SelectedSourceTableType == sourceTableType;
            Button button = new(() =>
            {
                m_SelectedSourceTableType = sourceTableType;
                RebuildTableTabs();
                RebuildOptions();
            })
            {
                text = text
            };
            button.tooltip = sourceTableType == null ? "显示全部子表" : $"只显示 {sourceTableType.Name}";
            button.style.height = 26f;
            button.style.minWidth = 58f;
            button.style.marginRight = 4f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal;
            button.style.backgroundColor = selected
                ? new Color(0.24f, 0.36f, 0.52f, 0.92f)
                : new Color(0.19f, 0.19f, 0.19f, 1f);
            button.style.borderBottomWidth = selected ? 2f : 0f;
            button.style.borderBottomColor = new Color(0.45f, 0.7f, 1f, 1f);
            return button;
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

        private bool IsSelectedOption(object keyValue)
        {
            if (XDataTableRefResolver.IsEmptyReferenceValue(m_SelectedValue, m_OwnerFieldType))
            {
                return false;
            }

            if (Equals(keyValue, m_SelectedValue))
            {
                return true;
            }

            return XDataTableRefResolver.TryConvertReferenceValue(m_OwnerFieldType, keyValue, out object convertedKey)
                   && XDataTableRefResolver.TryConvertReferenceValue(m_OwnerFieldType, m_SelectedValue, out object convertedSelected)
                   && Equals(convertedKey, convertedSelected);
        }

        private void MarkSelectedOption(VisualElement optionElement)
        {
            m_SelectedOptionElement = optionElement;
            optionElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            optionElement.style.backgroundColor = new Color(0.24f, 0.36f, 0.52f, 0.72f);
            optionElement.style.borderLeftWidth = 3f;
            optionElement.style.borderLeftColor = new Color(0.45f, 0.7f, 1f, 1f);
        }

        private void ScheduleScrollToSelectedOption()
        {
            if (m_SelectedOptionElement == null || m_OptionsScrollView == null)
            {
                return;
            }

            m_OptionsScrollView.schedule.Execute(() =>
            {
                if (m_SelectedOptionElement != null)
                {
                    m_OptionsScrollView?.ScrollTo(m_SelectedOptionElement);
                }
            });
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

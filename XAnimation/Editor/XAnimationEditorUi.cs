#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.Editor
{
    internal sealed class FoldoutCard
    {
        public VisualElement Root;
        public VisualElement Content;
        public Action<bool> SetExpanded;
        public Action RefreshState;
    }

    internal class RowVisualState
    {
        public Color BaseColor;
        public bool Hovered;
        public bool Playing;
        public float Progress;
        public VisualElement ProgressFill;
    }

    internal sealed class ClipRowVisualState : RowVisualState
    {
        public bool Flashing;
        public int FlashVersion;
    }

    internal static class XAnimationEditorUi
    {
        public const float SectionTitleFontSize = 12f;
        public const float BodyFontSize = 11f;
        public const float ClipIconButtonSize = 22f;

        public static readonly Color PaneBorder = new(0.30f, 0.30f, 0.32f, 1f);
        public static readonly Color AccentColor = new(0.30f, 0.55f, 0.95f, 1f);
        public static readonly Color DangerColor = new(0.75f, 0.25f, 0.25f, 1f);
        public static readonly Color TextMuted = new(0.60f, 0.60f, 0.62f, 1f);
        public static readonly Color TextNormal = new(0.85f, 0.85f, 0.87f, 1f);
        public static readonly Color SectionDivider = new(0.28f, 0.28f, 0.30f, 1f);
        public static readonly Color HoverBg = new(0.24f, 0.24f, 0.26f, 1f);
        public static readonly Color ListGroupBg = new(0.17f, 0.18f, 0.20f, 1f);
        public static readonly Color ListRowEvenBg = new(0.16f, 0.16f, 0.17f, 1f);
        public static readonly Color ListRowOddBg = new(0.19f, 0.19f, 0.20f, 1f);
        public static readonly Color ListHeaderBg = new(0.22f, 0.23f, 0.25f, 1f);
        public static readonly Color PlayingBg = new(0.20f, 0.35f, 0.55f, 0.65f);
        public static readonly Color ProgressFillBg = new(0.20f, 0.55f, 0.95f, 0.55f);

        public static VisualElement CreateCard(string titleText, VisualElement titleAction = null)
        {
            bool hasVisibleTitle = !string.IsNullOrWhiteSpace(titleText);
            VisualElement card = new();
            card.style.marginBottom = 2;
            SetPadding(card, 3);
            SetBorder(card, SectionDivider, 1, 3);
            card.style.backgroundColor = new Color(0.15f, 0.15f, 0.16f, 1f);

            VisualElement titleRow = Row();
            titleRow.style.marginBottom = 2;
            titleRow.style.paddingBottom = 2;
            titleRow.style.borderBottomWidth = 1;
            titleRow.style.borderBottomColor = SectionDivider;

            VisualElement accent = new();
            accent.style.width = 2;
            accent.style.height = 11;
            accent.style.backgroundColor = AccentColor;
            SetRadius(accent, 2);
            accent.style.marginRight = 4;
            titleRow.Add(accent);

            Label label = new(titleText);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = SectionTitleFontSize;
            label.style.color = TextNormal;
            label.style.flexShrink = 0;

            if (titleAction == null)
            {
                label.style.flexGrow = 1;
                titleRow.Add(label);
            }
            else if (hasVisibleTitle)
            {
                label.style.flexGrow = 1;
                label.style.minWidth = 0;
                titleRow.Add(label);
                titleAction.style.flexShrink = 0;
                titleAction.style.marginLeft = 8;
                titleRow.Add(titleAction);
            }
            else
            {
                VisualElement titleContent = Row();
                titleContent.style.flexGrow = 1;
                titleContent.style.minWidth = 0;
                label.style.flexGrow = 1;
                titleContent.Add(label);
                titleAction.style.flexShrink = 0;
                titleAction.style.marginLeft = 4;
                titleContent.Add(titleAction);
                titleRow.Add(titleContent);
            }

            card.Add(titleRow);
            return card;
        }

        public static FoldoutCard CreateFoldoutCard(
            string titleText,
            bool expanded,
            Action<bool> setExpanded,
            VisualElement titleAction = null)
        {
            VisualElement card = CreateCard(titleText, titleAction);
            VisualElement titleRow = card[0];
            Label label = titleRow.Q<Label>();
            VisualElement content = new();
            content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(content);

            void ApplyExpanded(bool value)
            {
                expanded = value;
                setExpanded?.Invoke(value);
                content.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                if (label != null)
                {
                    label.text = string.IsNullOrWhiteSpace(titleText)
                        ? value ? "▾" : "▸"
                        : value ? $"▾ {titleText}" : $"▸ {titleText}";
                }

                titleRow.style.marginBottom = value ? 2 : 0;
                titleRow.style.paddingBottom = value ? 2 : 0;
                titleRow.style.borderBottomWidth = value ? 1 : 0;
            }

            ApplyExpanded(expanded);
            titleRow.tooltip = string.IsNullOrWhiteSpace(titleText) ? "点击展开/收起分区。" : $"点击展开/收起 {titleText} 分区。";
            titleRow.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    return;
                }

                if (evt.target is VisualElement target)
                {
                    if (titleAction != null && (ReferenceEquals(target, titleAction) || titleAction.Contains(target)))
                    {
                        return;
                    }

                    for (VisualElement current = target; current != null && current != titleRow; current = current.hierarchy.parent)
                    {
                        if (current.ClassListContains("xanim-playback-overlay-drag-handle"))
                        {
                            return;
                        }
                    }
                }

                ApplyExpanded(!expanded);
                evt.StopPropagation();
            });

            return new FoldoutCard { Root = card, Content = content, SetExpanded = ApplyExpanded };
        }

        public static FoldoutCard CreateSectionFoldoutCard(
            string titleText,
            bool expanded,
            Action<bool> setExpanded,
            VisualElement titleAction = null,
            Func<bool> canToggle = null,
            string headerTooltip = null,
            bool allowActionAreaBackgroundToggle = false)
        {
            VisualElement root = CreateSubBox();
            VisualElement header = Row();
            header.style.marginBottom = 0;
            header.style.paddingBottom = 0;

            bool hasVisibleTitle = !string.IsNullOrWhiteSpace(titleText);
            Label toggleLabel = new();
            toggleLabel.style.color = TextNormal;
            toggleLabel.style.fontSize = BodyFontSize;
            toggleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            toggleLabel.style.flexShrink = 0;
            toggleLabel.style.width = 16;
            toggleLabel.style.minWidth = 16;
            toggleLabel.style.maxWidth = 16;
            toggleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            toggleLabel.style.marginRight = hasVisibleTitle ? 2 : 4;

            Label label = new();
            label.style.color = TextNormal;
            label.style.fontSize = BodyFontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexGrow = 1;
            header.Add(toggleLabel);
            if (hasVisibleTitle)
            {
                header.Add(label);
            }

            if (titleAction != null)
            {
                titleAction.style.flexShrink = 0;
                titleAction.style.flexGrow = hasVisibleTitle ? 0 : 1;
                titleAction.style.marginLeft = hasVisibleTitle ? 6 : 0;
                header.Add(titleAction);
            }

            VisualElement content = new();
            content.style.marginTop = 4;
            root.Add(header);
            root.Add(content);

            bool ShouldIgnoreToggleTarget(VisualElement target)
            {
                if (titleAction == null || target == null || !titleAction.Contains(target))
                {
                    return false;
                }

                if (!allowActionAreaBackgroundToggle)
                {
                    return true;
                }

                for (VisualElement current = target; current != null && current != titleAction; current = current.hierarchy.parent)
                {
                    if (current is Button || current is BindableElement)
                    {
                        return true;
                    }
                }

                return false;
            }

            void RefreshState()
            {
                bool toggleable = canToggle?.Invoke() ?? true;
                bool isExpanded = toggleable && expanded;
                toggleLabel.text = isExpanded ? "▾" : "▸";
                label.text = hasVisibleTitle ? titleText : string.Empty;
                toggleLabel.style.color = toggleable ? TextNormal : TextMuted;
                label.style.color = toggleable ? TextNormal : TextMuted;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                header.style.marginBottom = isExpanded ? 3 : 0;
            }

            void ApplyExpanded(bool value)
            {
                expanded = value;
                setExpanded?.Invoke(value);
                RefreshState();
            }

            RefreshState();
            string tooltip = string.IsNullOrWhiteSpace(headerTooltip) ? $"点击展开/收起 {titleText}。" : headerTooltip;
            header.tooltip = tooltip;
            toggleLabel.tooltip = tooltip;
            label.tooltip = tooltip;
            root.tooltip = tooltip;
            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0 ||
                    evt.target is VisualElement target && ShouldIgnoreToggleTarget(target) ||
                    !(canToggle?.Invoke() ?? true))
                {
                    return;
                }

                ApplyExpanded(!expanded);
                evt.StopPropagation();
            });

            return new FoldoutCard { Root = root, Content = content, SetExpanded = ApplyExpanded, RefreshState = RefreshState };
        }

        public static VisualElement CreateSubBox()
        {
            VisualElement box = new();
            box.style.marginTop = 3;
            SetPadding(box, 4);
            SetBorder(box, SectionDivider, 1, 3);
            box.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f, 1f);
            return box;
        }

        public static VisualElement CreateListGroup(float marginBottom = 3f, float marginLeft = 0f)
        {
            VisualElement group = new();
            group.style.marginBottom = marginBottom;
            group.style.marginLeft = marginLeft;
            SetPadding(group, 3);
            group.style.paddingBottom = 2;
            group.style.backgroundColor = ListGroupBg;
            SetBorder(group, SectionDivider, 1, 3);
            return group;
        }

        public static VisualElement CreateNestedListGroup()
        {
            VisualElement group = CreateListGroup(marginBottom: 0f, marginLeft: 4f);
            group.style.marginTop = 3;
            group.style.paddingLeft = 1;
            group.style.paddingRight = 3;
            group.style.paddingBottom = 3;
            group.style.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1f);
            return group;
        }

        public static VisualElement CreateListHeader(float marginBottom = 2f)
        {
            VisualElement header = Row();
            header.style.marginBottom = marginBottom;
            SetPadding(header, 2, 3);
            header.style.backgroundColor = ListHeaderBg;
            SetRadius(header, 3);
            return header;
        }

        public static Label CreateFoldoutGlyph(bool expanded)
        {
            Label label = new(expanded ? "▾" : "▸");
            label.style.width = 14;
            label.style.flexShrink = 0;
            label.style.color = TextMuted;
            label.style.fontSize = BodyFontSize;
            return label;
        }

        public static Label CreateSmallInfoLabel(string text)
        {
            Label label = new(text);
            label.style.color = TextMuted;
            label.style.fontSize = 10;
            label.style.flexShrink = 0;
            return label;
        }

        public static Label CreateBoldLabel(string text)
        {
            Label label = new(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = TextNormal;
            return label;
        }

        public static Label CreateSectionTitleLabel(string text)
        {
            Label label = CreateBoldLabel(text);
            label.style.flexGrow = 1;
            label.style.fontSize = BodyFontSize;
            return label;
        }

        public static VisualElement CreateInteractiveRowContainer(int rowIndex)
        {
            VisualElement container = new();
            container.style.position = Position.Relative;
            container.style.overflow = Overflow.Hidden;
            SetRadius(container, 2);
            container.style.backgroundColor = RowBaseColor(rowIndex);
            return container;
        }

        public static VisualElement CreateRowContainer(int rowIndex)
        {
            VisualElement container = CreateInteractiveRowContainer(rowIndex);
            container.style.marginBottom = 2;
            return container;
        }

        public static VisualElement CreateRowContent()
        {
            VisualElement row = Row();
            SetPadding(row, 3, 4);
            row.style.position = Position.Relative;
            return row;
        }

        public static VisualElement CreateRowProgressFill()
        {
            return CreateProgressFill(ProgressFillBg);
        }

        public static VisualElement CreateProgressFill(Color color)
        {
            VisualElement fill = new();
            fill.pickingMode = PickingMode.Ignore;
            fill.style.position = Position.Absolute;
            fill.style.left = 0f;
            fill.style.top = 0f;
            fill.style.bottom = 0f;
            fill.style.width = Length.Percent(0f);
            fill.style.backgroundColor = color;
            fill.style.visibility = Visibility.Hidden;
            return fill;
        }

        public static Color RowBaseColor(int rowIndex)
        {
            return rowIndex % 2 == 0 ? ListRowEvenBg : ListRowOddBg;
        }

        public static void ApplyRowVisualState(VisualElement row, RowVisualState state)
        {
            if (row == null || state == null)
            {
                return;
            }

            row.style.backgroundColor = state.Playing ? PlayingBg : state.Hovered ? HoverBg : state.BaseColor;
            ApplyRowProgressVisualState(state);
        }

        public static void ApplyRowProgressVisualState(RowVisualState state)
        {
            if (state?.ProgressFill == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(state.Progress);
            state.ProgressFill.style.width = Length.Percent(progress * 100f);
            state.ProgressFill.style.visibility = progress > 0f ? Visibility.Visible : Visibility.Hidden;
        }

        public static void AddEmptyLabel(VisualElement root, string text)
        {
            if (root == null)
            {
                return;
            }

            Label label = new(text);
            label.style.color = TextMuted;
            label.style.fontSize = BodyFontSize;
            label.style.marginLeft = 4;
            root.Add(label);
        }

        public static Toggle CreateHeaderApplyToggle(bool value, string tooltip)
        {
            Toggle toggle = new("Apply") { value = value };
            toggle.tooltip = tooltip;
            toggle.style.flexShrink = 0;
            toggle.style.unityFontStyleAndWeight = FontStyle.Normal;
            return toggle;
        }

        public static void ConfigureCompactPlaybackField(BaseField<float> field, float valueWidth)
        {
            ConfigureCompactPlaybackField(field, null, valueWidth);
        }

        public static void ConfigureCompactPlaybackField(BaseField<float> field, string labelText, float valueWidth)
        {
            field.label = string.Empty;
            ConfigureCompactPlaybackElement(field, valueWidth);
        }

        public static void ConfigureCompactPlaybackElement(VisualElement field, float valueWidth)
        {
            field.style.width = valueWidth;
            field.style.minWidth = valueWidth;
            field.style.maxWidth = valueWidth;
            field.style.flexShrink = 0;
            field.style.alignSelf = Align.Center;
        }

        public static VisualElement CreatePlaybackFieldContainer(string labelText, VisualElement field, float labelWidth)
        {
            VisualElement container = Row();
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            container.style.minWidth = 0;

            Label label = new(labelText);
            label.style.width = labelWidth;
            label.style.minWidth = labelWidth;
            label.style.maxWidth = labelWidth;
            label.style.flexShrink = 0;
            label.style.fontSize = 10;
            label.style.color = TextMuted;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.marginRight = 6;
            container.Add(label);
            container.Add(field);
            return container;
        }

        public static VisualElement CreatePlaybackToggleRow(string labelText, Toggle toggle, float labelWidth)
        {
            toggle.label = string.Empty;
            toggle.style.flexShrink = 0;
            toggle.style.marginLeft = 0;
            return CreatePlaybackFieldContainer(labelText, toggle, labelWidth);
        }

        public static VisualElement CreatePlaybackFieldPairRow(
            string leftLabel,
            VisualElement leftField,
            string rightLabel,
            VisualElement rightField,
            float labelWidth,
            float valueWidth)
        {
            VisualElement row = Row();
            row.style.marginTop = 2;
            row.style.marginBottom = 2;
            row.style.minWidth = 0;

            ConfigureCompactPlaybackElement(leftField, valueWidth);
            VisualElement leftContainer = CreatePlaybackFieldContainer(leftLabel, leftField, labelWidth);
            leftContainer.style.marginTop = 0;
            leftContainer.style.marginBottom = 0;
            row.Add(leftContainer);

            ConfigureCompactPlaybackElement(rightField, valueWidth);
            VisualElement rightContainer = CreatePlaybackFieldContainer(rightLabel, rightField, labelWidth);
            rightContainer.style.marginTop = 0;
            rightContainer.style.marginBottom = 0;
            rightContainer.style.marginLeft = 10;
            row.Add(rightContainer);
            return row;
        }

        public static void ApplyClipIconButtonStyle(Button button, Color? bgColor = null, float size = ClipIconButtonSize)
        {
            button.style.backgroundColor = bgColor ?? ListHeaderBg;
            button.style.color = bgColor.HasValue ? Color.white : TextNormal;
            SetBorder(button, PaneBorder, 1, 3);
            SetPadding(button, 0);
            button.style.fontSize = 12;
            SetFixedSize(button, size, size);
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
        }

        public static void ApplyDropdownFieldStyle(DropdownField field, float height = ClipIconButtonSize)
        {
            if (field == null)
            {
                return;
            }

            field.style.minHeight = height;
            field.style.height = height;
            field.style.maxHeight = height;
            field.style.backgroundColor = Color.clear;
            field.style.borderTopWidth = 0;
            field.style.borderBottomWidth = 0;
            field.style.borderLeftWidth = 0;
            field.style.borderRightWidth = 0;

            if (field.labelElement != null)
            {
                field.labelElement.style.color = TextMuted;
                field.labelElement.style.fontSize = BodyFontSize;
                if (string.IsNullOrWhiteSpace(field.label))
                {
                    field.labelElement.style.display = DisplayStyle.None;
                }
            }

            void ApplyInnerStyle()
            {
                VisualElement input = field.Q<VisualElement>(className: "unity-base-field__input");
                if (input != null)
                {
                    input.style.backgroundColor = ListHeaderBg;
                    SetBorder(input, PaneBorder, 1, 3);
                    input.style.minHeight = height;
                    input.style.height = height;
                    input.style.maxHeight = height;
                    input.style.paddingLeft = 6;
                    input.style.paddingRight = 4;
                }

                TextElement text = field.Q<TextElement>(className: "unity-popup-field__text");
                if (text != null)
                {
                    text.style.color = TextNormal;
                    text.style.fontSize = BodyFontSize;
                    text.style.unityTextAlign = TextAnchor.MiddleLeft;
                }

                VisualElement arrow = input?.Q<VisualElement>(className: "unity-base-popup-field__arrow");
                if (arrow != null)
                {
                    arrow.style.marginLeft = 2;
                    arrow.style.marginRight = 2;
                }
            }

            ApplyInnerStyle();
            field.RegisterCallback<AttachToPanelEvent>(_ => ApplyInnerStyle());
        }

        public static void ApplyIconButtonStyle(Button button, bool isPlaying)
        {
            button.text = isPlaying ? "■" : "▶";
            button.style.width = 28;
            button.style.minWidth = 28;
            button.style.height = 22;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.color = Color.white;
            button.style.backgroundColor = isPlaying ? DangerColor : AccentColor;
        }

        public static void ApplyTrashButtonIcon(Button button)
        {
            Texture icon = EditorGUIUtility.IconContent("TreeEditor.Trash").image ??
                           EditorGUIUtility.IconContent("d_TreeEditor.Trash").image;
            if (icon == null)
            {
                button.text = "⌫";
                return;
            }

            button.text = string.Empty;
            button.Clear();
            Image image = new() { image = icon };
            image.tintColor = TextNormal;
            image.style.width = 13;
            image.style.height = 13;
            image.style.alignSelf = Align.Center;
            image.style.flexShrink = 0;
            button.Add(image);
        }

        public static void ConfigureEditableNameLabel(EditableLabel label, float width)
        {
            label.style.width = width;
            label.style.flexShrink = 0;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = BodyFontSize;
            label.style.color = TextNormal;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            TextElement textElement = label.Q<TextElement>();
            if (textElement != null)
            {
                textElement.style.fontSize = BodyFontSize;
                textElement.style.color = TextNormal;
                textElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            TextField textField = label.Q<TextField>();
            if (textField == null)
            {
                return;
            }

            textField.style.marginTop = 0;
            textField.style.marginBottom = 0;
            textField.style.fontSize = BodyFontSize;
            VisualElement input = textField.Q("unity-text-input");
            if (input != null)
            {
                input.style.fontSize = BodyFontSize;
            }
        }

        public static void SetBorder(VisualElement element, Color color, float width = 1f, float radius = 0f)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            if (radius > 0f)
            {
                SetRadius(element, radius);
            }
        }

        public static void SetPadding(VisualElement element, float all)
        {
            SetPadding(element, all, all);
        }

        public static void SetPadding(VisualElement element, float vertical, float horizontal)
        {
            element.style.paddingLeft = horizontal;
            element.style.paddingRight = horizontal;
            element.style.paddingTop = vertical;
            element.style.paddingBottom = vertical;
        }

        public static void SetMargin(VisualElement element, float all)
        {
            element.style.marginLeft = all;
            element.style.marginRight = all;
            element.style.marginTop = all;
            element.style.marginBottom = all;
        }

        public static void SetMargin(VisualElement element, float top, float right, float bottom, float left)
        {
            element.style.marginTop = top;
            element.style.marginRight = right;
            element.style.marginBottom = bottom;
            element.style.marginLeft = left;
        }

        public static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        public static void SetFixedSize(VisualElement element, float width, float height)
        {
            element.style.width = width;
            element.style.minWidth = width;
            element.style.maxWidth = width;
            element.style.height = height;
            element.style.minHeight = height;
            element.style.maxHeight = height;
        }

        private static VisualElement Row()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }
    }
}
#endif

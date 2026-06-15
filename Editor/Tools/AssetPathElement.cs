using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using XFramework.UI;

namespace XFramework.Editor
{
    [CustomPropertyDrawer(typeof(AssetPathAttribute))]
    public class AssetPathElement : XInspectorElement
    {
        private const float MinPreviewHeight = 48f;
        private const float MaxPreviewHeight = 320f;

        private readonly VisualElement m_Title;
        private readonly VisualElement m_PreviewContent;
        private readonly UnityEditor.UIElements.ObjectField m_ObjectField;
        private readonly Image m_Preview;
        private AssetPathAttribute m_Attribute;
        private bool m_CanTogglePreview;
        private bool m_PreviewStateInitialized;
        private float m_PreviewAspectRatio = 1f;

        public AssetPathElement()
        {
            AddToClassList("asset-path-element");
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Stretch;

            Remove(variableNameText);

            m_Title = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                },
                focusable = true,
                tabIndex = 0
            };

            m_ObjectField = new UnityEditor.UIElements.ObjectField();
            m_ObjectField.AddToClassList("inspector-input");
            m_ObjectField.style.flexGrow = 1f;
            m_ObjectField.allowSceneObjects = false;
            m_ObjectField.RegisterValueChangedCallback(OnValueChanged);
            m_ObjectField.RegisterCallback<MouseDownEvent>(OnObjectFieldMouseDown);
            m_ObjectField.RegisterCallback<KeyDownEvent>(OnObjectFieldKeyDown);

            m_PreviewContent = new VisualElement();

            m_Preview = new Image();
            m_Preview.scaleMode = ScaleMode.ScaleToFit;
            m_Preview.style.width = Length.Percent(100f);
            m_Preview.style.alignSelf = Align.Stretch;
            m_Preview.style.maxHeight = MaxPreviewHeight;
            m_Preview.RegisterCallback<GeometryChangedEvent>(OnPreviewGeometryChanged);

            m_Title.RegisterCallback<MouseDownEvent>(OnTitleMouseDown);
            m_Title.RegisterCallback<KeyDownEvent>(OnTitleKeyDown);
            m_Title.Add(variableNameText);
            m_Title.Add(m_ObjectField);
            m_PreviewContent.Add(m_Preview);

            Add(m_Title);
            SetPreviewToggleActive(false);
        }

        protected override void OnBound()
        {
            base.OnBound();
            m_Attribute = BoundMemberInfo?.GetCustomAttribute<AssetPathAttribute>();
            m_ObjectField.objectType = m_Attribute?.targetType ?? typeof(Object);
            m_PreviewStateInitialized = false;
            RefreshPreview();
        }

        public override void Refresh()
        {
            Object asset = AssetDatabase.LoadAssetAtPath(Value as string, m_ObjectField.objectType ?? typeof(Object));
            m_ObjectField.SetValueWithoutNotify(asset);
            RefreshPreview();
        }

        private void OnValueChanged(ChangeEvent<Object> evt)
        {
            Object asset = evt.newValue;
            if (asset == null)
            {
                Value = string.Empty;
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning("只支持 Project 资源，不支持场景对象。");
                    Refresh();
                    return;
                }

                Value = path;
            }

            RefreshPreview();
        }

        private void RefreshPreview()
        {
            Object previewAsset = GetPreviewAsset();
            if (previewAsset == null)
            {
                m_PreviewStateInitialized = false;
                SetPreviewToggleActive(false);
                Collapse();
                ClearPreview();
                return;
            }

            SetPreviewAsset(previewAsset);
            SetPreviewToggleActive(true);
            if (!m_PreviewStateInitialized)
            {
                m_PreviewStateInitialized = true;
                Expand();
            }

            UpdatePreviewHeight();
        }

        private void Expand()
        {
            if (m_PreviewContent.parent != this)
            {
                Add(m_PreviewContent);
            }
        }

        private void Collapse()
        {
            if (m_PreviewContent.parent == this)
            {
                Remove(m_PreviewContent);
            }
        }

        private void SetPreviewToggleActive(bool value)
        {
            m_CanTogglePreview = value;
        }

        private void OnTitleMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || !m_CanTogglePreview || evt.target is not VisualElement target)
            {
                return;
            }

            if (target.GetFirstAncestorOfType<UnityEditor.UIElements.ObjectField>() != null
                || target.GetFirstAncestorOfType<Button>() != null)
            {
                return;
            }

            m_Title.Focus();
            TogglePreview();
            evt.StopPropagation();
        }

        private void OnTitleKeyDown(KeyDownEvent evt)
        {
            if (!m_CanTogglePreview)
            {
                return;
            }

            if (evt.keyCode == KeyCode.LeftArrow)
            {
                Collapse();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.RightArrow)
            {
                Expand();
                evt.StopPropagation();
            }
        }

        private void TogglePreview()
        {
            if (m_PreviewContent.parent == this)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        private static void OnObjectFieldMouseDown(MouseDownEvent evt)
        {
            evt.StopPropagation();
        }

        private static void OnObjectFieldKeyDown(KeyDownEvent evt)
        {
            evt.StopPropagation();
        }

        private Object GetPreviewAsset()
        {
            return m_ObjectField.value is Texture or Sprite ? m_ObjectField.value : null;
        }

        private void SetPreviewAsset(Object previewAsset)
        {
            m_Preview.image = null;
            m_Preview.sprite = null;

            if (previewAsset is Sprite sprite)
            {
                m_Preview.sprite = sprite;
                m_PreviewAspectRatio = sprite.rect.width > 0f ? sprite.rect.height / sprite.rect.width : 1f;
                return;
            }

            Texture texture = (Texture)previewAsset;
            m_Preview.image = texture;
            m_PreviewAspectRatio = texture.width > 0 ? (float)texture.height / texture.width : 1f;
        }

        private void ClearPreview()
        {
            m_Preview.image = null;
            m_Preview.sprite = null;
        }

        private void OnPreviewGeometryChanged(GeometryChangedEvent evt)
        {
            if (m_PreviewContent.parent == this && !Mathf.Approximately(evt.oldRect.width, evt.newRect.width))
            {
                UpdatePreviewHeight();
            }
        }

        private void UpdatePreviewHeight()
        {
            float width = m_Preview.contentRect.width;
            if (width <= 0f || float.IsNaN(width))
            {
                m_Preview.style.height = MinPreviewHeight;
                return;
            }

            m_Preview.style.height = Mathf.Clamp(width * m_PreviewAspectRatio, MinPreviewHeight, MaxPreviewHeight);
        }
    }
}

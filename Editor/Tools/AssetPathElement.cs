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
        private readonly VisualElement m_TitleRow;
        private readonly UnityEditor.UIElements.ObjectField m_ObjectField;
        private readonly Image m_Preview;
        private AssetPathAttribute m_Attribute;
        private bool m_ShowPreview;

        public AssetPathElement()
        {
            AddToClassList("asset-path-element");

            m_TitleRow = new VisualElement();
            m_TitleRow.style.flexDirection = FlexDirection.Row;
            m_TitleRow.style.alignItems = Align.Center;
            m_TitleRow.style.flexGrow = 1f;
            style.flexDirection = FlexDirection.Column;
            style.alignItems = Align.Stretch;

            m_ObjectField = new UnityEditor.UIElements.ObjectField();
            m_ObjectField.AddToClassList("inspector-input");
            m_ObjectField.style.flexGrow = 1f;
            m_ObjectField.allowSceneObjects = false;
            m_ObjectField.RegisterValueChangedCallback(OnValueChanged);

            m_Preview = new Image();
            m_Preview.scaleMode = ScaleMode.StretchToFill;

            Remove(variableNameText);
            m_TitleRow.Add(variableNameText);
            m_TitleRow.Add(m_ObjectField);
            Add(m_TitleRow);

            variableNameText.RegisterCallback<MouseDownEvent>(OnLabelMouseDown);
        }

        protected override void OnBound()
        {
            base.OnBound();
            m_Attribute = BoundMemberInfo?.GetCustomAttribute<AssetPathAttribute>();
            m_ObjectField.objectType = m_Attribute?.targetType ?? typeof(Object);
            RefreshPreview();
        }

        public override void Refresh()
        {
            base.Refresh();
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

        private void OnLabelMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0 || evt.clickCount < 2 || !SupportsTexturePreview())
            {
                return;
            }

            m_ShowPreview = !m_ShowPreview;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (!SupportsTexturePreview())
            {
                if (Contains(m_Preview))
                {
                    Remove(m_Preview);
                }

                return;
            }

            Texture texture = m_ObjectField.value as Texture;
            m_Preview.image = texture;

            if (!m_ShowPreview || texture == null)
            {
                if (Contains(m_Preview))
                {
                    Remove(m_Preview);
                }

                return;
            }

            if (!Contains(m_Preview))
            {
                Add(m_Preview);
            }

            float ratio = texture.width > 0 ? (float)texture.height / texture.width : 1f;
            m_Preview.style.height = m_Preview.layout.width * ratio;
        }

        private bool SupportsTexturePreview()
        {
            return m_ObjectField.objectType != null && typeof(Texture).IsAssignableFrom(m_ObjectField.objectType);
        }
    }
}

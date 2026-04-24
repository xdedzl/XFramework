using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace XFramework.UI
{
    public abstract class InspectorElement : VisualElement
    {
        public delegate object Getter();
        public delegate void Setter(object value);

        private Getter getter;
        private Setter setter;

        private object m_value;
        private Type m_boundVariableType;
        private Inspector m_inspector;
        private int m_depth;

        private string variableName;
        private string customDisplayName;
        protected readonly TextElement variableNameText;

        protected InspectorElement()
        {
            AddToClassList("inspector-element");
            
            variableNameText = new TextElement();
            variableNameText.AddToClassList("inspector-label");
            Add(variableNameText);
        }

        protected object Value
        {
            get
            {
                return m_value;
            }
            set
            {
                setter(value);
                m_value = value;
            }
        }

        protected Type BoundVariableType => m_boundVariableType;

        public Inspector Inspector
        {
            protected get => m_inspector;
            set
            {
                if (m_inspector != value)
                {
                    m_inspector = value;
                }
            }
        }

        public int Depth
        {
            get => m_depth;
            set
            {
                m_depth = value;
                OnDepthChange(value);
            }
        }

        protected string Name
        {
            get => variableName;
            set
            {
                variableName = value;
                UpdateVariableNameText();
            }
        }

        /// <summary>
        /// 绑定UI
        /// </summary>
        /// <param name="parent">UI</param>
        /// <param name="member">成员</param>
        /// <param name="variableName">变量名称</param>
        public virtual void BindTo(InspectorElement parent, MemberInfo member, string propertyName)
        {
            string variableName = propertyName;
            string displayName = GetCustomDisplayName(member);

            if (member is FieldInfo field)
            {
                variableName ??= field.Name;

                if (field.FieldType.IsValueType || field.FieldType == typeof(string))
                    BindTo(field.FieldType, variableName, () => field.GetValue(parent.Value), (value) =>
                    {
                        field.SetValue(parent.Value, value);
                        parent.Value = parent.Value;         // 这一步是为了将struct的值不断向上一级传递(string 虽然是值类型，但经常会存在struct里)
                    }, displayName);
                else
                    BindTo(field.FieldType, variableName, () => field.GetValue(parent.Value), (value) =>
                    {
                        field.SetValue(parent.Value, value);
                    }, displayName);
            }
            else if (member is PropertyInfo property)
            {
                variableName ??= property.Name;

                if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                    BindTo(property.PropertyType, variableName, () => property.GetValue(parent.Value, null), (value) =>
                    {
                        property.SetValue(parent.Value, value, null);
                        parent.Value = parent.Value;
                    }, displayName);
                else
                    BindTo(property.PropertyType, variableName, () => property.GetValue(parent.Value, null), (value) =>
                    {
                        property.SetValue(parent.Value, value, null);
                    }, displayName);
            }
            else
                throw new ArgumentException("Member can either be a field or a property");
        }

        /// <summary>
        /// 绑定UI
        /// </summary>
        /// <param name="variableType">变量类型</param>
        /// <param name="variableName">变量名称</param>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        public virtual void BindTo(Type variableType, string variableName, Getter getter, Setter setter)
        {
            BindTo(variableType, variableName, getter, setter, null);
        }

        private void BindTo(Type variableType, string variableName, Getter getter, Setter setter, string displayName)
        {
            m_boundVariableType = variableType;
            customDisplayName = displayName;
            Name = variableName;

            this.getter = getter;
            this.setter = setter;

            OnBound();
        }

        /// <summary>
        /// UI刷新
        /// </summary>
        public virtual void Refresh()
        {

        }

        /// <summary>
        /// 数据绑定时调用
        /// </summary>
        protected virtual void OnBound()
        {
            try
            {
                m_value = getter();
            }
            catch
            {
                if (BoundVariableType.IsValueType)
                    m_value = Activator.CreateInstance(BoundVariableType);
                else
                    m_value = null;
            }
        }

        /// <summary>
        /// 数据解绑时调用
        /// </summary>
        protected virtual void OnUnBound()
        {
            m_value = null;
        }

        protected virtual void OnDepthChange(int depth)
        {
            if (variableNameText != null)
                variableNameText.style.translate = new Vector2(Inspector.TabSize * Depth, 0f);
        }

        private void UpdateVariableNameText()
        {
            if (variableNameText != null)
                variableNameText.text = string.IsNullOrEmpty(customDisplayName) ? FormatDisplayName(variableName) : customDisplayName;
        }

        private static string GetCustomDisplayName(MemberInfo member)
        {
            return member?.GetCustomAttribute<DisplayNameAttribute>()?.displayName;
        }

        private static string FormatDisplayName(string value)
        {
            var displayName = value;
            if (!string.IsNullOrEmpty(displayName) && displayName.StartsWith("m_"))
                displayName = displayName[2..];
            if (!string.IsNullOrEmpty(displayName) && char.IsLetter(displayName[0]))
                displayName = char.ToUpper(displayName[0]) + displayName[1..];
            return displayName;
        }
    }
}

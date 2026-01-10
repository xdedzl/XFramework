using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XFramework.Resource;

namespace XFramework.UI.Bind
{
    public abstract class ComponentBindBase<TValue, TComponent> where TComponent : Component
    {
        private TValue m_Value;
        private readonly List<TComponent> m_Components = new();
        
        public TValue Value
        {
            get => m_Value;
            set
            {
                this.m_Value = value;
                foreach (var c in m_Components)
                {
                    RefreshComponent(c, value);
                }
            }
        }
        
        protected abstract void RefreshComponent(TComponent component, TValue value);
        
        public void Bind(TComponent component)
        {
            m_Components.Add(component);
            RefreshComponent(component, m_Value);
        }
        
        public void Unbind(TComponent component)
        {
            m_Components.Remove(component);
        }
    }
    
    public class CBindText:ComponentBindBase<string, Text>
    {
        protected override void RefreshComponent(Text component, string value)
        {
            component.text = value;
        }
    }
    
    public class CBindTextMeshProUGUI:ComponentBindBase<string, TextMeshProUGUI>
    {
        protected override void RefreshComponent(TextMeshProUGUI component, string value)
        {
            component.text = value;
        }
    }
    
    public class CBindImage:ComponentBindBase<string, Image>
    {
        protected override void RefreshComponent(Image component, string value)
        {
            component.sprite = ResourceManager.Instance.Load<Sprite>(value);
        }
    }
}
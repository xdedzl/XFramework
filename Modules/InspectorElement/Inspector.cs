using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

using System.Linq;

namespace XFramework.UI
{
    public class Inspector : VisualElement
    {
        public const int TabSize = 16;

        /// <summary>
        /// 默认UI
        /// </summary>
        private readonly Dictionary<Type, Type> m_DefaultTypeToDrawer = new ();
        private readonly Dictionary<ISupport, Type> m_OtherDrawer = new ();

        private object m_TargetObj;

        public object TargetData
        {
            get
            {
                if(m_TargetObj is IDataContainer container)
                    return container.Data;
                else
                    return m_TargetObj;
            }
        }

        public Inspector(bool useDefaultStyle = true)
        {
            if (useDefaultStyle)
            {
                style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.3f));
                style.marginBottom = 5;
                style.marginLeft = 5;
                style.marginRight = 5;
                style.marginTop = 5;
                style.paddingLeft = 15;
                style.borderBottomLeftRadius = 5;
                style.borderBottomRightRadius = 5;
                style.borderTopRightRadius = 5;
                style.borderTopLeftRadius = 5;
            }
            
            AddToClassList("inspector");
            Type[] types = GetSonTypes(typeof(InspectorElement));
            foreach (var type in types)
            {
                var supportType = type.GetCustomAttribute<DefaultSportTypesAttribute>();
                if (supportType != null)
                {
                    foreach (var item in supportType.types)
                    {
                        m_DefaultTypeToDrawer.Add(item, type);
                    }
                }
                else
                {
                    var supportHelper = type.GetCustomAttribute<SupportHelperAttribute>();
                    if(supportHelper != null)
                    {
                        m_OtherDrawer.Add(supportHelper.support, type);
                    }
                }
            }
        }

        /// <summary>
        /// 绑定数据
        /// </summary>
        /// <param name="obj"></param>
        public void Bind(object obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("Null Obj!");
                return;
            }
            if (obj.GetType().IsValueType)
            {
                Debug.LogError("Can't bind a value type!, please use a StructContainer!");
                return;
            }

            this.Clear();
            ObjectElement uiItem = CreateDrawerForType(typeof(ObjectElement), -1) as ObjectElement;
            uiItem.Expand();
            uiItem.SetArrowActive(false);
            this.Add(uiItem); 
            if (uiItem != null)
            {
                m_TargetObj = obj;
                uiItem.BindTo(obj.GetType(), string.Empty, () => m_TargetObj, (value) => m_TargetObj = value);
                uiItem.Refresh();
            }
            else
            {
                m_TargetObj = null;
            }
        }

        public void ExpandFirstLevelElements()
        {
            var rootElement = Children().OfType<ExpandableElement>().FirstOrDefault();
            if (rootElement == null)
            {
                return;
            }

            rootElement.Expand();
            foreach (var child in rootElement.GetChildElements().OfType<ExpandableElement>())
            {
                child.Expand();
            }
        }

        /// <summary>
        /// 通过成员变量类型获取UIItem
        /// </summary>
        public InspectorElement CreateDrawerForMemberType(Type memberType, int depth)
        {
            if (m_DefaultTypeToDrawer.TryGetValue(memberType, out Type elementType))
            {
                return CreateDrawerForType(elementType, depth);
            }
            else
            {
                foreach (var item in m_OtherDrawer)
                {
                    if (item.Key.Support(memberType))
                    {
                        return CreateDrawerForType(item.Value, depth);
                    }
                }

                return CreateDrawerForType(typeof(ObjectElement), depth);
            }
        }

        /// <summary>
        /// 通过UI类型获取UIItem
        /// </summary>
        /// <param name="elementType"></param>
        /// <param name="drawerParent"></param>
        /// <returns></returns>
        public InspectorElement CreateDrawerForType(Type elementType, int depth, params object[] args)
        {
            InspectorElement element = Activator.CreateInstance(elementType, args) as InspectorElement;
            element.Inspector = this;
            element.Depth = depth;
            return element;
        }

        /// <summary>
        /// 创建一个数组元素自定义的ArrayElement
        /// </summary>
        /// <param name="customerAttribute"></param>
        /// <param name="depth"></param>
        /// <returns></returns>
        public InspectorElement CreateCustomerArrayElement(CustomerElementAttribute customerAttribute, int depth)
        {
            InspectorElement element = Activator.CreateInstance(typeof(ArrayElement), customerAttribute) as InspectorElement;
            element.Inspector = this;
            element.Depth = depth;
            return element;
        }

        private Type[] GetSonTypes(Type typeBase)
        {
            List<Type> typeNames = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeBase))
                    {
                        typeNames.Add(type);
                    }
                }
            }
            return typeNames.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

using System.Linq;

namespace XFramework.UI
{
    public class XInspector : VisualElement
    {
        public const int TabSize = 16;

        /// <summary>
        /// 默认UI
        /// </summary>
        private readonly Dictionary<Type, Type> m_DefaultTypeToDrawer = new ();
        private readonly Dictionary<Type, Type> m_PropertyAttributeToDrawer = new();
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

        public XInspector(bool useDefaultStyle = true)
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
            Type[] types = GetSonTypes(typeof(XInspectorElement));
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

                Type propertyAttributeType = GetPropertyAttributeType(type);
                if (propertyAttributeType != null)
                {
                    m_PropertyAttributeToDrawer[propertyAttributeType] = type;
                }

                if (supportType == null)
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
            var rootElement = Children().OfType<IExpandableElement>().FirstOrDefault();
            if (rootElement == null)
            {
                return;
            }

            ExpandElementTree(rootElement);
        }

        private static void ExpandElementTree(IExpandableElement element)
        {
            element.Expand();
            foreach (VisualElement child in element.GetChildElements())
            {
                if (child is IExpandableElement childExpandable)
                {
                    ExpandElementTree(childExpandable);
                }
            }
        }

        /// <summary>
        /// 通过成员变量类型获取UIItem
        /// </summary>
        public XInspectorElement CreateDrawerForMemberType(Type memberType, int depth)
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
        public XInspectorElement CreateDrawerForType(Type elementType, int depth, params object[] args)
        {
            XInspectorElement element = Activator.CreateInstance(elementType, args) as XInspectorElement;
            element.XInspector = this;
            element.Depth = depth;
            ApplyDefaultLayout(element);
            return element;
        }

        public XInspectorElement CreateArrayPropertyElement(PropertyAttribute itemPropertyAttribute, int depth)
        {
            XInspectorElement element = Activator.CreateInstance(typeof(ArrayElement), itemPropertyAttribute) as XInspectorElement;
            element.XInspector = this;
            element.Depth = depth;
            ApplyDefaultLayout(element);
            return element;
        }

        public Type GetDrawerForPropertyAttribute(Type propertyAttributeType)
        {
            if (propertyAttributeType == null)
            {
                return null;
            }

            return m_PropertyAttributeToDrawer.TryGetValue(propertyAttributeType, out Type drawerType)
                ? drawerType
                : null;
        }

        public Type GetDrawerForPropertyAttribute(MemberInfo member)
        {
            PropertyAttribute propertyAttribute = GetPropertyAttribute(member);
            return GetDrawerForPropertyAttribute(propertyAttribute?.GetType());
        }

        public PropertyAttribute GetPropertyAttribute(MemberInfo member)
        {
            if (member == null)
            {
                return null;
            }

#if UNITY_EDITOR
            object[] attributes = member.GetCustomAttributes(typeof(UnityEngine.PropertyAttribute), true);
            foreach (object attribute in attributes)
            {
                if (attribute is not PropertyAttribute propertyAttribute)
                {
                    continue;
                }

                if (m_PropertyAttributeToDrawer.ContainsKey(propertyAttribute.GetType()))
                {
                    return propertyAttribute;
                }
            }
#endif

            return null;
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

        private static Type GetPropertyAttributeType(Type inspectorElementType)
        {
#if UNITY_EDITOR
            foreach (object attribute in inspectorElementType.GetCustomAttributes(false))
            {
                if (attribute == null)
                {
                    continue;
                }

                Type attributeType = attribute.GetType();
                if (attributeType.FullName != "UnityEditor.CustomPropertyDrawer")
                {
                    continue;
                }

                FieldInfo targetTypeField =
                    attributeType.GetField("m_Type", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? attributeType.GetField("m_Type", BindingFlags.Instance | BindingFlags.Public);
                Type targetType = targetTypeField?.GetValue(attribute) as Type;
                if (targetType != null && typeof(UnityEngine.PropertyAttribute).IsAssignableFrom(targetType))
                {
                    return targetType;
                }
            }
#endif
            return null;
        }

        private static void ApplyDefaultLayout(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            ApplyDefaultLayoutToElement(root);
            foreach (VisualElement element in root.Query<VisualElement>().ToList())
            {
                ApplyDefaultLayoutToElement(element);
            }
        }

        private static void ApplyDefaultLayoutToElement(VisualElement element)
        {
            if (element.ClassListContains("inspector-element"))
            {
                element.style.width = Length.Percent(100);
                element.style.alignSelf = Align.Stretch;
            }

            if (element.ClassListContains("inspector-label"))
            {
                element.style.width = Length.Percent(40);
                element.style.minWidth = 120f;
                element.style.flexShrink = 0f;
                element.style.whiteSpace = WhiteSpace.NoWrap;
                element.style.paddingLeft = 5f;
            }

            if (element.ClassListContains("inspector-input"))
            {
                element.style.width = Length.Percent(60);
                element.style.minWidth = 160f;
                element.style.flexGrow = 1f;
            }
        }
    }
}

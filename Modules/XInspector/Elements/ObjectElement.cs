using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace XFramework.UI
{
    public class ObjectElement : ExpandableElement
    {
        protected override void CreateElements()
        {
            base.CreateElements();

            // 共有变量且未添加ItemIgnore特性
            foreach (MemberInfo member in GetMembers(BoundVariableType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField | BindingFlags.GetProperty))
            {
                if (member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
                {
                    if (Attribute.IsDefined(member, typeof(XInspectorIgnoreAttribute)))
                        continue;

                    var nameAttribute = member.GetCustomAttribute<ElementPropertyAttribute>();
                    string propertyName = nameAttribute != null && !string.IsNullOrEmpty(nameAttribute.propertyName) ? nameAttribute.propertyName : member.Name;
                    var element = CreateItemForMember(member, Depth + 1);
                    element.BindTo(this, member, propertyName);
                    element.Refresh();
                    elementsContent.Add(element);
                }
            }

            // 非公有变量添加ItemProperty特性
            foreach (MemberInfo member in GetMembers(BoundVariableType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.GetProperty))
            {
                if (member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
                {
                    var nameAttribute = member.GetCustomAttribute<ElementPropertyAttribute>();

                    if (nameAttribute != null)
                    {
                        string propertyName = !string.IsNullOrEmpty(nameAttribute.propertyName) ? nameAttribute.propertyName : member.Name;
                        var element = CreateItemForMember(member, Depth + 1);
                        element.BindTo(this, member, propertyName);
                        element.Refresh();
                        elementsContent.Add(element);
                    }
                }
            }
        }

        /// <summary>
        /// 通过成员变量信息获取UIItem
        /// </summary>
        /// <param name="member"></param>
        /// <param name="parentElement"></param>
        /// <returns></returns>
        private XInspectorElement CreateItemForMember(MemberInfo member, int depth)
        {
            if (Attribute.IsDefined(member, typeof(XInspectorIgnoreAttribute)))
            {
                return null;
            }
            
            Type variableType = member is FieldInfo ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
            ArrayItemPropertyAttribute arrayItemPropertyAttribute = member.GetCustomAttribute<ArrayItemPropertyAttribute>();
            if (arrayItemPropertyAttribute != null && IsArrayItemPropertyTarget(variableType))
            {
                return XInspector.CreateArrayItemPropertyElement(arrayItemPropertyAttribute, depth);
            }

            Type propertyDrawerType = XInspector.GetDrawerForPropertyAttribute(member);
            if (propertyDrawerType != null)
            {
                return XInspector.CreateDrawerForType(propertyDrawerType, depth);
            }

            return XInspector.CreateDrawerForMemberType(variableType, depth);
        }

        private static bool IsArrayItemPropertyTarget(Type type)
        {
            return (type.IsArray && type.GetArrayRank() == 1)
                   || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
        }

        /// <summary>
        /// 对获得FieldInfos排序
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private List<MemberInfo> GetMembers(Type type, BindingFlags bindingFlags)
        {
            List<MemberInfo> result = new List<MemberInfo>();

            do
            {
                var temp = new List<MemberInfo>(type.GetMembers(bindingFlags | BindingFlags.DeclaredOnly));
                temp.AddRange(result);
                result = temp;
                type = type.BaseType;
            }
            while (type != typeof(System.Object) && type != typeof(System.ValueType));

            var aa = new List<MemberInfo>();

            foreach (var item in result)
            {
                if(item is PropertyInfo property)
                {
                    if (property.CanWrite && property.CanRead)
                        aa.Add(item);
                }
                else if(item is FieldInfo field)
                {
                    if (field.IsInitOnly || field.IsLiteral)
                        continue;
                    aa.Add(item);
                }
            }

            return aa;
        }
    }
}

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
                    if (Attribute.IsDefined(member, typeof(ElementIgnoreAttribute)))
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
        private InspectorElement CreateItemForMember(MemberInfo member, int depth)
        {
            if (Attribute.IsDefined(member, typeof(ElementIgnoreAttribute)))
            {
                return null;
            }

            CustomerElementAttribute attribute = member.GetCustomAttribute<CustomerElementAttribute>();

            if (attribute != null && attribute.type != null)
            {
                if (attribute is ArrayCustomerElementAttribute)
                    return Inspector.CreateCustomerArrayElement(attribute, depth);
                else
                    return Inspector.CreateDrawerForType(attribute.type, depth, attribute.args);
            }
            else
            {
                Type variableType = member is FieldInfo ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
                return Inspector.CreateDrawerForMemberType(variableType, depth);
            }
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
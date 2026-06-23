using System;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;

namespace XFramework.UI
{
    /// <summary>
    /// Marks a PanelBase field to be filled from the panel's XUIBase lookup table.
    /// This is a UI object reference helper, not a data binding attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class UIRefAttribute : Attribute
    {
        public UIRefAttribute()
        {
        }

        public UIRefAttribute(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }

    /// <summary>
    /// Marks a PanelBase method to be bound to an XUIBase event source during panel initialization.
    /// </summary>
    [MeansImplicitUse(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.Itself)]
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class UIListenerAttribute : Attribute
    {
        public UIListenerAttribute()
        {
        }

        public UIListenerAttribute(string key)
        {
            Key = key;
        }

        public UIListenerAttribute(string key, string eventName)
        {
            Key = key;
            EventName = eventName;
        }

        public string Key { get; }
        public string EventName { get; }
    }

    internal static class UIAutoBinder
    {
        public static void Bind(object owner, ComponentFindHelper<XUIBase> componentFindHelper, Type stopBaseType, string ownerKind)
        {
            InjectUIRefs(owner, componentFindHelper, stopBaseType, ownerKind);
            BindUIListeners(owner, componentFindHelper, stopBaseType, ownerKind);
        }

        private static void InjectUIRefs(object owner, ComponentFindHelper<XUIBase> componentFindHelper, Type stopBaseType, string ownerKind)
        {
            Type ownerType = owner.GetType();
            for (Type type = ownerType; type != null && type != stopBaseType; type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    UIRefAttribute attribute = field.GetCustomAttribute<UIRefAttribute>();
                    if (attribute == null)
                    {
                        continue;
                    }

                    InjectUIRefField(owner, ownerType, ownerKind, componentFindHelper, field, attribute);
                }
            }
        }

        private static void InjectUIRefField(object owner, Type ownerType, string ownerKind, ComponentFindHelper<XUIBase> componentFindHelper, FieldInfo field, UIRefAttribute attribute)
        {
            if (field.IsStatic || field.IsInitOnly)
            {
                throw new XFrameworkException(
                    $"[UIRef] {ownerType.Name}.{field.Name} cannot be static or readonly.");
            }

            string key = GetUIRefKey(field, attribute);
            XUIBase uiRef = GetUIRefComponent(ownerType, ownerKind, componentFindHelper, field, key);
            object value = GetUIRefValue(ownerType, ownerKind, field, key, uiRef);
            field.SetValue(owner, value);
        }

        private static XUIBase GetUIRefComponent(Type ownerType, string ownerKind, ComponentFindHelper<XUIBase> componentFindHelper, FieldInfo field, string key)
        {
            try
            {
                return componentFindHelper[key];
            }
            catch (Exception exception)
            {
                throw new XFrameworkException(
                    $"[UIRef] Failed to find UI reference. {ownerKind}: {ownerType.Name}, Field: {field.Name}, Key: {key}, Expected: {field.FieldType.Name}. {exception.Message}");
            }
        }

        private static object GetUIRefValue(Type ownerType, string ownerKind, FieldInfo field, string key, XUIBase uiRef)
        {
            Type fieldType = field.FieldType;
            if (fieldType.IsAssignableFrom(uiRef.GetType()))
            {
                return uiRef;
            }

            if (fieldType == typeof(GameObject))
            {
                return uiRef.gameObject;
            }

            if (typeof(Component).IsAssignableFrom(fieldType))
            {
                Component component = uiRef.GetComponent(fieldType);
                if (component != null)
                {
                    return component;
                }
            }

            throw new XFrameworkException(
                $"[UIRef] UI reference type mismatch. {ownerKind}: {ownerType.Name}, Field: {field.Name}, Key: {key}, Expected: {fieldType.Name}, Actual: {uiRef.GetType().Name}.");
        }

        private static string GetUIRefKey(FieldInfo field, UIRefAttribute attribute)
        {
            if (!string.IsNullOrWhiteSpace(attribute.Key))
            {
                return attribute.Key;
            }

            string key = field.Name;
            if (key.StartsWith("m_", StringComparison.Ordinal))
            {
                key = key.Substring(2);
            }
            else if (key.StartsWith("_", StringComparison.Ordinal))
            {
                key = key.Substring(1);
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new XFrameworkException($"[UIRef] Cannot infer UI reference key from field {field.Name}.");
            }

            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }

        private static void BindUIListeners(object owner, ComponentFindHelper<XUIBase> componentFindHelper, Type stopBaseType, string ownerKind)
        {
            Type ownerType = owner.GetType();
            for (Type type = ownerType; type != null && type != stopBaseType; type = type.BaseType)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (MethodInfo method in methods)
                {
                    UIListenerAttribute attribute = method.GetCustomAttribute<UIListenerAttribute>(true);
                    if (attribute == null)
                    {
                        continue;
                    }

                    BindUIListenerMethod(owner, ownerType, ownerKind, componentFindHelper, method, attribute);
                }
            }
        }

        private static void BindUIListenerMethod(object owner, Type ownerType, string ownerKind, ComponentFindHelper<XUIBase> componentFindHelper, MethodInfo method, UIListenerAttribute attribute)
        {
            if (method.IsStatic)
            {
                throw new XFrameworkException(
                    $"[UIListener] {ownerType.Name}.{method.Name} cannot be static.");
            }

            string key = GetUIListenerKey(method, attribute);
            XUIBase uiRef = GetUIListenerComponent(ownerType, ownerKind, componentFindHelper, method, key);
            if (uiRef is not IUIEventSource eventSource)
            {
                throw new XFrameworkException(
                    $"[UIListener] UI reference does not support listener binding. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, Actual: {uiRef.GetType().Name}.");
            }

            string eventName = attribute.EventName;
            Type listenerType = GetUIListenerType(ownerType, ownerKind, method, key, eventName, uiRef, eventSource);
            ValidateUIListenerMethod(ownerType, ownerKind, method, key, eventName, listenerType);
            Delegate listener = Delegate.CreateDelegate(listenerType, owner, method);
            AddUIListener(ownerType, ownerKind, method, key, eventName, uiRef, eventSource, listener);
        }

        private static Type GetUIListenerType(Type ownerType, string ownerKind, MethodInfo method, string key, string eventName, XUIBase uiRef, IUIEventSource eventSource)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return eventSource.ListenerType;
            }

            if (uiRef is not IUIMultiEventSource multiEventSource)
            {
                throw new XFrameworkException(
                    $"[UIListener] UI reference does not support named listener binding. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {eventName}, Actual: {uiRef.GetType().Name}.");
            }

            Type listenerType = multiEventSource.GetListenerType(eventName);
            if (listenerType == null)
            {
                throw new XFrameworkException(
                    $"[UIListener] Unsupported UI listener event. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {eventName}, Actual: {uiRef.GetType().Name}.");
            }

            return listenerType;
        }

        private static void AddUIListener(Type ownerType, string ownerKind, MethodInfo method, string key, string eventName, XUIBase uiRef, IUIEventSource eventSource, Delegate listener)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                eventSource.AddListener(listener);
                return;
            }

            if (uiRef is not IUIMultiEventSource multiEventSource)
            {
                throw new XFrameworkException(
                    $"[UIListener] UI reference does not support named listener binding. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {eventName}, Actual: {uiRef.GetType().Name}.");
            }

            multiEventSource.AddListener(eventName, listener);
        }

        private static XUIBase GetUIListenerComponent(Type ownerType, string ownerKind, ComponentFindHelper<XUIBase> componentFindHelper, MethodInfo method, string key)
        {
            try
            {
                return componentFindHelper[key];
            }
            catch (Exception exception)
            {
                throw new XFrameworkException(
                    $"[UIListener] Failed to find UI listener target. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}. {exception.Message}");
            }
        }

        private static void ValidateUIListenerMethod(Type ownerType, string ownerKind, MethodInfo method, string key, string eventName, Type listenerType)
        {
            if (listenerType == null || !typeof(Delegate).IsAssignableFrom(listenerType))
            {
                throw new XFrameworkException(
                    $"[UIListener] Invalid listener type. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, ListenerType: {listenerType?.Name ?? "null"}.");
            }

            MethodInfo invokeMethod = listenerType.GetMethod("Invoke");
            if (invokeMethod == null)
            {
                throw new XFrameworkException(
                    $"[UIListener] Invalid listener type. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, ListenerType: {listenerType.Name}.");
            }

            if (method.ReturnType != invokeMethod.ReturnType)
            {
                throw new XFrameworkException(
                    $"[UIListener] Method return type mismatch. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, Expected: {invokeMethod.ReturnType.Name}, Actual: {method.ReturnType.Name}.");
            }

            ParameterInfo[] methodParameters = method.GetParameters();
            ParameterInfo[] listenerParameters = invokeMethod.GetParameters();
            if (methodParameters.Length != listenerParameters.Length)
            {
                throw new XFrameworkException(
                    $"[UIListener] Method parameter count mismatch. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, Expected: {listenerParameters.Length}, Actual: {methodParameters.Length}.");
            }

            for (int i = 0; i < methodParameters.Length; i++)
            {
                Type expectedType = listenerParameters[i].ParameterType;
                Type actualType = methodParameters[i].ParameterType;
                if (actualType != expectedType)
                {
                    throw new XFrameworkException(
                        $"[UIListener] Method parameter type mismatch. {ownerKind}: {ownerType.Name}, Method: {method.Name}, Key: {key}, EventName: {FormatUIListenerEventName(eventName)}, Parameter: {i}, Expected: {expectedType.Name}, Actual: {actualType.Name}.");
                }
            }
        }

        private static string FormatUIListenerEventName(string eventName)
        {
            return string.IsNullOrEmpty(eventName) ? "Default" : eventName;
        }

        private static string GetUIListenerKey(MethodInfo method, UIListenerAttribute attribute)
        {
            if (!string.IsNullOrWhiteSpace(attribute.Key))
            {
                return attribute.Key;
            }

            string key = method.Name;
            if (key.StartsWith("On", StringComparison.Ordinal) && key.Length > 2)
            {
                key = key.Substring(2);
            }

            if (key.EndsWith("Click", StringComparison.Ordinal) && key.Length > "Click".Length)
            {
                key = key.Substring(0, key.Length - "Click".Length);
            }
            else if (key.EndsWith("Changed", StringComparison.Ordinal) && key.Length > "Changed".Length)
            {
                key = key.Substring(0, key.Length - "Changed".Length);
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new XFrameworkException($"[UIListener] Cannot infer UI listener key from method {method.Name}.");
            }

            return char.ToUpperInvariant(key[0]) + key.Substring(1);
        }
    }
}
